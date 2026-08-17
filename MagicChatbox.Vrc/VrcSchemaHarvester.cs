using System.Threading.Channels;
using MagicChatbox.Osc.Query;
using MagicChatbox.Vocabulary;

namespace MagicChatbox.Vrc;

/// <summary>Why a harvest was requested. Recorded so a diagnostic can tell a swap from a reconnect.</summary>
internal enum VrcHarvestTrigger
{
    /// <summary>A peer handshake completed. The first enumeration of a session.</summary>
    PeerConnected,

    /// <summary>The avatar changed, so the previous enumeration describes an avatar nobody is wearing.</summary>
    AvatarChanged,
}

/// <summary>
/// Asks the peer what exists, and hands the answer to <see cref="IVrcSchemaSink"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the half that was missing.</b> <c>OscQueryClient</c> could already fetch and flatten a peer's
/// tree, and <c>AvatarSchema.DeclareFromOscQuery</c> could already consume one — but nothing connected
/// them, because the handshake discarded the peer's HTTP endpoint the moment it had read <c>HOST_INFO</c>.
/// The application therefore learned a parameter only when it happened to change, which on a real avatar
/// meant knowing about 39 of 218.
/// </para>
/// <para>
/// <b>It never advances the epoch.</b> That is deliberate and load-bearing.
/// <see cref="VrcAvatarEpoch.AdvanceToAvatar"/> raises <c>Invalidated</c>, and every consumer of that event
/// is written against the guarantee that it arrives on the OSC receive loop — the same thread that appends
/// observations, which is what makes "evict before the next flush" a fact rather than a hope. Raising it
/// from this class's thread pool task would quietly delete that guarantee. So a harvest only ever
/// <i>reads</i> the epoch: it declares and seeds, and lets <c>/avatar/change</c> remain the single writer.
/// </para>
/// <para>
/// <b>Requests coalesce.</b> The channel holds one pending request, because two harvests queued back to
/// back would ask the same question twice and the second answer would win anyway. A peer re-advertising
/// after a VRChat restart, or a re-queued handshake, produces another <c>PeerConnected</c> — so the whole
/// path has to be idempotent, and is.
/// </para>
/// </remarks>
internal sealed class VrcSchemaHarvester : IDisposable
{
    private const string ParameterPrefix = "/avatar/parameters/";

    /// <summary>How many times a run of failures is re-asked before the harvester gives up on it.</summary>
    /// <remarks>
    /// Bounded rather than endless because the two ways a peer stops answering are not the same problem. A
    /// dropped fetch against a peer that is still there resolves within a couple of seconds; VRChat having
    /// closed does not resolve at all, and re-asking it every thirty seconds for the rest of the session is
    /// a background task that can never succeed. The recovery for the second case is the handshake, which
    /// raises <c>PeerConnected</c> when a peer comes back and starts the count again from zero.
    /// </remarks>
    internal const int MaxRetryAttempts = 6;

    /// <summary>
    /// How many times a peer's tree is re-asked while it still names the avatar being taken off.
    /// </summary>
    /// <remarks>
    /// Higher than the failure ceiling and on a much shorter delay, because this is not a peer that has
    /// gone away -- it answered, promptly, with an account that is merely a moment stale. It catches up
    /// well inside a second in practice; the ceiling exists only so a client that never updates its tree
    /// cannot be polled for the rest of the session.
    /// </remarks>
    internal const int MaxLagAttempts = 12;

    private static readonly TimeSpan LagRetryDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan FirstRetryDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    private readonly OscQueryService _query;
    private readonly VrcAvatarEpoch _epoch;
    private readonly IVrcSchemaSink _sink;
    private readonly Channel<VrcHarvestTrigger> _requests;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    private long _requested;
    private long _completed;
    private long _abandoned;
    private long _failed;
    private long _retried;
    private long _lagged;
    private int _consecutiveFailures;
    private int _consecutiveLags;
    private bool _disposed;

    internal VrcSchemaHarvester(
        OscQueryService query,
        VrcAvatarEpoch epoch,
        IVrcSchemaSink sink,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(epoch);
        ArgumentNullException.ThrowIfNull(sink);

        _query = query;
        _epoch = epoch;
        _sink = sink;
        _delay = delay ?? Task.Delay;

        // DropWrite with capacity 1: a queued request has not been acted on yet, so a second one would
        // ask an identical question. Never DropOldest — the pending request is already the newest.
        _requests = Channel.CreateBounded<VrcHarvestTrigger>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

        _query.PeerConnected += OnPeerConnected;
        _epoch.Invalidated += OnAvatarInvalidated;
    }

    /// <summary>Harvests asked for.</summary>
    internal long Requested => Interlocked.Read(ref _requested);

    /// <summary>Harvests delivered to the sink.</summary>
    internal long Completed => Interlocked.Read(ref _completed);

    /// <summary>Harvests thrown away because the avatar changed while the request was in flight.</summary>
    internal long Abandoned => Interlocked.Read(ref _abandoned);

    /// <summary>Harvests where the peer did not answer.</summary>
    internal long Failed => Interlocked.Read(ref _failed);

    /// <summary>Requests re-asked by the backoff after a failure, rather than by an event.</summary>
    internal long Retried => Interlocked.Read(ref _retried);

    /// <summary>Answers refused because the peer's tree still named the previous avatar.</summary>
    internal long Lagged => Interlocked.Read(ref _lagged);

    /// <remarks>
    /// True only when both accounts are readable and disagree. Either one being empty is ordinary -- a
    /// peer need not report an avatar at all, and the epoch has none before the first change -- and
    /// refusing on that would mean never accepting a schema on a client that does not publish one.
    /// </remarks>
    internal static bool TreeLagsBehind(VrcAvatarEpoch epoch, string? treeAvatarId)
    {
        if (string.IsNullOrEmpty(treeAvatarId))
        {
            return false;
        }

        string wearing = epoch.CurrentAvatarId;

        return wearing.Length > 0 && !string.Equals(treeAvatarId, wearing, StringComparison.Ordinal);
    }

    private bool LagsBehindTheEpoch(string? treeAvatarId) => TreeLagsBehind(_epoch, treeAvatarId);

    /// <summary>How long the run of failures ending at <paramref name="attempt"/> waits before re-asking.</summary>
    /// <remarks>
    /// Doubling from one second to a thirty-second ceiling. The first step is short because the common
    /// failure is a peer that was mid-restart and is already back; the ceiling exists because past a few
    /// seconds the cause is no longer transient and the only thing more frequent polling buys is noise.
    /// </remarks>
    internal static TimeSpan DelayFor(int attempt)
    {
        int step = attempt < 1 ? 1 : attempt;
        double seconds = FirstRetryDelay.TotalSeconds * Math.Pow(2, step - 1);
        return seconds >= MaxRetryDelay.TotalSeconds ? MaxRetryDelay : TimeSpan.FromSeconds(seconds);
    }

    /// <summary>Drains harvest requests until cancelled.</summary>
    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var trigger in _requests.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                await HarvestOnceAsync(trigger, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _query.PeerConnected -= OnPeerConnected;
        _epoch.Invalidated -= OnAvatarInvalidated;
        _requests.Writer.TryComplete();
    }

    /// <summary>Translates one OSCQuery leaf's declared type tag into a kind, or refuses it.</summary>
    /// <remarks>
    /// Refused rather than guessed, for the same reason the ingress refuses an unknown tag: a guessed kind
    /// installs a descriptor that coercion then fights on every message, and the resulting mismatch reads
    /// as a wire fault rather than the schema fault it is.
    /// </remarks>
    internal static bool TryKindOf(string? oscType, out SignalKind kind)
    {
        kind = default;
        if (oscType is not { Length: 1 })
        {
            return false;
        }

        switch (oscType[0])
        {
            case 'T':
            case 'F':
                kind = SignalKind.Bool;
                return true;
            case 'i':
                kind = SignalKind.Int;
                return true;
            case 'f':
                kind = SignalKind.Float;
                return true;
            default:
                return false;
        }
    }

    /// <summary>Converts a JSON-decoded OSCQuery value into the declared kind, or null when there is none.</summary>
    internal static SignalValue? ValueOf(object? raw, SignalKind kind) => raw switch
    {
        null => null,
        bool b => kind == SignalKind.Bool ? SignalValue.Bool(b) : Numeric(b ? 1d : 0d, kind),
        int i => Numeric(i, kind),
        long l => Numeric(l, kind),
        double d => Numeric(d, kind),
        float f => Numeric(f, kind),
        _ => null,
    };

    private static SignalValue? Numeric(double value, SignalKind kind) => kind switch
    {
        // Truncation toward zero, matching the coercion the store applies. Pinned here so a seeded Int
        // and an observed Int can never disagree about 0.9.
        SignalKind.Int => SignalValue.Int((long)value),
        SignalKind.Float => SignalValue.Float((float)value),
        SignalKind.Bool => SignalValue.Bool(value != 0d),
        _ => null,
    };

    private void OnPeerConnected(System.Net.IPEndPoint _) => Request(VrcHarvestTrigger.PeerConnected);

    private void OnAvatarInvalidated(VrcAvatarInvalidated _) => Request(VrcHarvestTrigger.AvatarChanged);

    /// <remarks>
    /// Called from the mDNS/handshake loop and from the OSC receive loop. Does no work beyond a
    /// non-blocking channel write — the receive loop must not wait on an HTTP request.
    /// <para>
    /// An event-driven request also clears the failure budget, and that is what makes the ceiling on
    /// retries safe. A run of failures against a peer that has gone away spends the budget once; a peer
    /// coming back, or the wearer changing avatar, is new information about a different situation and
    /// starts again from zero rather than inheriting the exhausted count.
    /// </para>
    /// </remarks>
    private void Request(VrcHarvestTrigger trigger, bool resetBudget = true)
    {
        if (_disposed)
        {
            return;
        }

        if (resetBudget)
        {
            Interlocked.Exchange(ref _consecutiveFailures, 0);
            Interlocked.Exchange(ref _consecutiveLags, 0);
        }

        if (_requests.Writer.TryWrite(trigger))
        {
            Interlocked.Increment(ref _requested);
        }
    }

    private async Task HarvestOnceAsync(VrcHarvestTrigger trigger, CancellationToken cancellationToken)
    {
        // Captured BEFORE the request goes out. Anything that changes while it is in flight makes the
        // answer describe an avatar nobody is wearing.
        var epochAtRequest = _epoch.Current;

        OscQuerySnapshot? snapshot;
        try
        {
            snapshot = await _query.TryFetchPeerSnapshotAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // A peer that vanished mid-fetch is an ordinary event, not an error worth propagating into
            // the handshake loop.
            Fail(trigger, cancellationToken);
            return;
        }

        if (snapshot is not { } tree)
        {
            Fail(trigger, cancellationToken);
            return;
        }

        // The peer answered, so whatever run of failures preceded this is over even though this particular
        // answer is about to be thrown away.
        Interlocked.Exchange(ref _consecutiveFailures, 0);

        if (!_epoch.IsCurrent(epochAtRequest))
        {
            // Not re-asked here: the epoch only moves when /avatar/change arrives, and that event has
            // already queued a request of its own.
            Interlocked.Increment(ref _abandoned);
            return;
        }

        // The tree is a SECOND account of which avatar is loaded, and it does not change at the same
        // moment the first one does: /avatar/change arrives over OSC and we ask immediately, while the
        // peer's HTTP tree still answers with the avatar being taken off. Delivering that would install
        // the previous avatar's parameters under the new avatar's epoch, and refusing it outright would
        // leave the new avatar with no schema at all for as long as it is worn -- so it is re-asked.
        if (LagsBehindTheEpoch(tree.AvatarId))
        {
            Interlocked.Increment(ref _lagged);

            int attempt = Interlocked.Increment(ref _consecutiveLags);
            if (attempt <= MaxLagAttempts)
            {
                _ = RetryAfterAsync(trigger, LagRetryDelay, cancellationToken);
            }

            return;
        }

        Interlocked.Exchange(ref _consecutiveLags, 0);

        var parameters = new List<VrcParameterDeclaration>(tree.Parameters.Count);
        foreach (var entry in tree.Parameters)
        {
            if (!entry.Path.StartsWith(ParameterPrefix, StringComparison.Ordinal)
                || entry.Path.Length == ParameterPrefix.Length
                || !TryKindOf(entry.OscType, out var kind))
            {
                continue;
            }

            parameters.Add(new VrcParameterDeclaration(
                entry.Path[ParameterPrefix.Length..],
                kind,
                ValueOf(entry.Value, kind),
                (entry.Access & OscQueryAccess.Write) != 0));
        }

        var fixedReadings = new List<VrcFixedReading>(tree.AvatarLeaves.Count);
        foreach (var entry in tree.AvatarLeaves)
        {
            if (VrcAvatarKeys.TryFixedKeyFor(entry.Path) is not { } keyText
                || !TryKindOf(entry.OscType, out var kind)
                || !SignalKey.TryIntern(keyText, out var key))
            {
                continue;
            }

            if (ValueOf(entry.Value, kind) is { } value)
            {
                fixedReadings.Add(new VrcFixedReading(key, value));
            }
        }

        _sink.OnSchemaHarvested(new VrcAvatarSchemaHarvest(tree.AvatarId, epochAtRequest, parameters, fixedReadings));
        Interlocked.Increment(ref _completed);
    }

    /// <summary>Counts a failure and re-asks the same question after a backoff.</summary>
    /// <remarks>
    /// Without this the class asks once per event and stops. Nothing else re-triggers a harvest — an avatar
    /// change or a peer handshake, neither of which a person performs on request — so a single dropped fetch
    /// left the schema empty for as long as somebody kept wearing the avatar they had on, and every surface
    /// built over the schema reported that avatar as having no parameters at all.
    /// </remarks>
    private void Fail(VrcHarvestTrigger trigger, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _failed);

        int attempt = Interlocked.Increment(ref _consecutiveFailures);
        if (attempt > MaxRetryAttempts)
        {
            return;
        }

        _ = RetryAfterAsync(trigger, DelayFor(attempt), cancellationToken);
    }

    /// <remarks>
    /// Deliberately not awaited by the drain loop. Waiting there would hold the single reader for the whole
    /// backoff, so an avatar change arriving during it would sit unread behind a delay that exists for an
    /// entirely unrelated peer problem.
    /// </remarks>
    private async Task RetryAfterAsync(VrcHarvestTrigger trigger, TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await _delay(delay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (_disposed || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        Interlocked.Increment(ref _retried);
        Request(trigger, resetBudget: false);
    }
}
