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

    private readonly OscQueryService _query;
    private readonly VrcAvatarEpoch _epoch;
    private readonly IVrcSchemaSink _sink;
    private readonly Channel<VrcHarvestTrigger> _requests;

    private long _requested;
    private long _completed;
    private long _abandoned;
    private long _failed;
    private bool _disposed;

    internal VrcSchemaHarvester(OscQueryService query, VrcAvatarEpoch epoch, IVrcSchemaSink sink)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(epoch);
        ArgumentNullException.ThrowIfNull(sink);

        _query = query;
        _epoch = epoch;
        _sink = sink;

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
    /// </remarks>
    private void Request(VrcHarvestTrigger trigger)
    {
        if (_disposed)
        {
            return;
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
            // the handshake loop. The next trigger re-asks.
            Interlocked.Increment(ref _failed);
            return;
        }

        if (snapshot is not { } tree)
        {
            Interlocked.Increment(ref _failed);
            return;
        }

        if (!_epoch.IsCurrent(epochAtRequest))
        {
            Interlocked.Increment(ref _abandoned);
            return;
        }

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
}
