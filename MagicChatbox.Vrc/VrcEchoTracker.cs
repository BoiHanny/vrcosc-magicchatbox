using System.Diagnostics;
using MagicChatbox.Vocabulary;

namespace MagicChatbox.Vrc;

/// <summary>How a dispatched write ended.</summary>
public enum VrcEchoStatus
{
    /// <summary>VRChat echoed the value we asked for, while the avatar we asked it of was still loaded.</summary>
    Matched = 0,

    /// <summary>No matching echo arrived in time. The write may still have landed; we cannot claim it did.</summary>
    TimedOut = 1,

    /// <summary>The avatar changed while we waited. The write is void, and so is any echo that follows.</summary>
    AvatarChanged = 2,

    /// <summary>The dispatch never reached the socket, or the caller cancelled.</summary>
    Cancelled = 3,
}

/// <summary>One write waiting for its echo.</summary>
/// <param name="OperationId">The dispatching <see cref="Correlation"/>'s id, so the ledger lines up.</param>
/// <param name="Key">The kernel key the echo will arrive on.</param>
/// <param name="Expected">The value that confirms it.</param>
/// <param name="AvatarEpoch">The epoch at dispatch. An echo under a newer one cannot confirm this.</param>
/// <param name="RegisteredTicks">
/// <see cref="Stopwatch.GetTimestamp"/> at registration, which is the start of the round trip.
/// </param>
/// <remarks>
/// <see cref="RegisteredTicks"/> is the whole of the host-side change behind
/// <see cref="VrcEchoTimings"/>: correlation was already registering a start and reaching a definite
/// outcome, and the elapsed time between the two was simply never taken. A monotonic stamp rather than a
/// wall clock, because this is a duration and a clock adjustment mid-measurement would produce a negative
/// latency.
/// </remarks>
public readonly record struct VrcPendingEcho(
    Guid OperationId, SignalKey Key, SignalValue Expected, long AvatarEpoch, long RegisteredTicks);

/// <summary>
/// How long VRChat is taking to acknowledge our writes, and how many it never acknowledged.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is worth having.</b> In a packed public instance VRChat's frame time collapses and players
/// ask "is it me or the instance?" with nothing but vibes to go on. The application is already holding the
/// evidence: it registers an echo wait for every avatar-parameter write and gets a definite outcome for
/// each one. Timing that is one subtraction, and it turns a feeling into a number a person can watch.
/// </para>
/// <para>
/// <b>Matched and timed out are counted separately and must never be merged.</b> A write that never came
/// back is not a slow write — it is a different fact (<see cref="VrcEchoStatus.TimedOut"/>), and folding a
/// two-second timeout into the window as if it were a two-second latency would put a fabricated number on
/// a graph. <see cref="Record"/> is internal and its only caller in this assembly is the match site, so
/// there is no path by which a timeout can become a sample.
/// </para>
/// <para>
/// <b>A median of sixteen, not a mean.</b> One write that landed during a frame hitch is not the client's
/// health, and a mean of sixteen lets a single 900 ms outlier double the reading. The window is small
/// enough to follow a change within a few seconds of writing at ordinary rates, and the samples are kept
/// in a fixed ring so measuring costs no allocation on the confirm path.
/// </para>
/// <para>
/// <b>Passive, and that is a limitation rather than a virtue.</b> There is a reading only while something
/// is writing — a person, a rule, or Carry-over. No synthetic probe exists because there is no harmless
/// guaranteed-writable parameter to probe with, and adding traffic to measure congestion on the one wire
/// this application must not abuse is the wrong trade. Whoever publishes these numbers is responsible for
/// saying "nothing written recently" instead of showing an old one as if it were current.
/// </para>
/// </remarks>
public sealed class VrcEchoTimings
{
    /// <summary>How many matched echoes the median is taken over.</summary>
    public const int WindowSize = 16;

    private readonly double[] _samples = new double[WindowSize];
    private readonly Lock _gate = new();

    private int _count;
    private int _next;
    private long _matched;
    private long _timedOut;

    /// <summary>Writes VRChat echoed back. The denominator of everything here.</summary>
    public long Matched => Interlocked.Read(ref _matched);

    /// <summary>
    /// Writes that never came back in time. <b>Never rendered as a latency.</b>
    /// </summary>
    /// <remarks>
    /// A sustained non-zero count against a live client is its own diagnosis — the writes are not landing
    /// — and it is a different sentence from "the client is slow". See the type remarks.
    /// </remarks>
    public long TimedOut => Interlocked.Read(ref _timedOut);

    /// <summary>How many samples the window currently holds, up to <see cref="WindowSize"/>.</summary>
    public int SampleCount
    {
        get
        {
            lock (_gate)
            {
                return _count;
            }
        }
    }

    /// <summary>The rolling median round trip, or false when nothing has been measured yet.</summary>
    /// <remarks>
    /// False rather than zero, because zero is a plausible-looking latency and "we have not measured" is
    /// not a fast client. The caller has an empty state for it and this is what makes reaching it possible.
    /// </remarks>
    public bool TryMedianMilliseconds(out double milliseconds)
    {
        Span<double> window = stackalloc double[WindowSize];
        int count;

        lock (_gate)
        {
            count = _count;
            if (count == 0)
            {
                milliseconds = 0d;
                return false;
            }

            _samples.AsSpan(0, count).CopyTo(window);
        }

        window = window[..count];
        window.Sort();

        // The even case averages the two middle samples rather than taking the upper one, so a window
        // straddling a step change reads as the middle of it instead of jumping a whole sample early.
        milliseconds = (count & 1) == 1
            ? window[count / 2]
            : (window[(count / 2) - 1] + window[count / 2]) / 2d;

        return true;
    }

    /// <summary>Records one matched echo's round trip. Called on the OSC receive loop.</summary>
    internal void Record(double milliseconds)
    {
        // A negative or non-finite elapsed time cannot come from Stopwatch, but it can come from a caller
        // that registered a wait with a stamp from somewhere else. It is dropped rather than clamped: a
        // zero in the window is a reading, and this is not one.
        if (!double.IsFinite(milliseconds) || milliseconds < 0d)
        {
            return;
        }

        lock (_gate)
        {
            _samples[_next] = milliseconds;
            _next = (_next + 1) % WindowSize;

            if (_count < WindowSize)
            {
                _count++;
            }
        }

        Interlocked.Increment(ref _matched);
    }

    /// <summary>Counts one write that was never acknowledged. Contributes no sample.</summary>
    internal void RecordTimeout() => Interlocked.Increment(ref _timedOut);
}

/// <summary>
/// A registered write and the outcome it is waiting for.
/// </summary>
/// <remarks>
/// The handle exists so that "how did this write end?" can be answered <b>after</b> the write has
/// already ended. A tracker keyed only by id has to guess for a caller that asks late — and the
/// plausible guess, "the avatar is still current so it must have matched", is exactly the false
/// confirmation echo correlation exists to prevent.
/// </remarks>
public sealed class VrcEchoWait
{
    internal VrcEchoWait(VrcPendingEcho pending)
    {
        Pending = pending;
    }

    /// <summary>What was registered, and against which avatar epoch.</summary>
    public VrcPendingEcho Pending { get; }

    /// <summary>Completes exactly once, with whichever outcome came first.</summary>
    public Task<VrcEchoStatus> Completion => Source.Task;

    /// <summary>True once the outcome is known.</summary>
    public bool IsSettled => Source.Task.IsCompleted;

    internal TaskCompletionSource<VrcEchoStatus> Source { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}

/// <summary>
/// Correlates a write we dispatched with the echo VRChat sends back.
/// </summary>
/// <remarks>
/// <para>
/// <b>P7 — there is no protocol-level way to detect our own echo.</b> OSC carries no sender identity, no
/// message id and no correlation field. When <c>/avatar/parameters/Hue</c> arrives there is nothing in
/// the packet saying whether VRChat generated it or whether it is the echo of the value we sent 80 ms
/// ago. Correlation is therefore <i>structurally required</i> for one-shot Discrete writes: without it a
/// write that should fire a trigger once fires it twice, and a toggle can oscillate. Continuous
/// parameters need none of this — the store's dedupe already collapses them.
/// </para>
/// <para>
/// Ported from v2's <c>Features/Transport/OSC/OscEchoCorrelator.cs</c>, keeping its shape: capture the
/// avatar epoch before sending; complete when a change arrives whose value matches <b>and</b> whose
/// epoch is still current; time out otherwise.
/// </para>
/// <para>
/// <b>The epoch guard is the part that matters.</b> An avatar change invalidates every outstanding wait,
/// so a value that happens to match on the <i>new</i> avatar can never be mistaken for the echo of a
/// write to the <i>old</i> one. This tracker goes one step further than v2 and completes those waits as
/// <see cref="VrcEchoStatus.AvatarChanged"/> the moment the epoch advances, rather than leaving them to
/// expire: the caller learns what actually happened instead of being told it timed out.
/// </para>
/// <para>
/// <b>Against the reference implementation.</b> VRCOSC offers
/// <c>SendParameterAndWait(name, value, blockEvents = false, …)</c>
/// (<c>VRCOSC.App/SDK/Modules/Module.cs:896</c>) where <c>blockEvents</c> — the flag that actually
/// prevents the loop — is <b>unset by default</b>. A module author who does not know to pass
/// <c>true</c> gets the loop. That is a footgun, not a solved problem, and it is deliberately not copied.
/// </para>
/// </remarks>
public sealed class VrcEchoTracker : IDisposable
{
    /// <summary>
    /// How long to wait before calling a write unconfirmed.
    /// </summary>
    /// <remarks>
    /// A local echo returns in tens of milliseconds; two seconds is slack for a frame-hitching client,
    /// not a considered latency budget. It is the interval behind
    /// <see cref="ReasonCode.EgressAckTimeout"/>.
    /// </remarks>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

    private readonly Dictionary<Guid, VrcEchoWait> _pending = [];
    private readonly Lock _gate = new();
    private readonly VrcAvatarEpoch _epoch;
    private readonly TimeSpan _timeout;

    private int _pendingCount;
    private bool _disposed;

    /// <param name="avatarEpoch">The epoch every pending write is keyed to.</param>
    /// <param name="timeout">Defaults to <see cref="DefaultTimeout"/>.</param>
    public VrcEchoTracker(VrcAvatarEpoch avatarEpoch, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(avatarEpoch);

        var value = timeout ?? DefaultTimeout;
        if (value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), value, "The echo timeout must be positive.");
        }

        _epoch = avatarEpoch;
        _timeout = value;
        _epoch.Invalidated += OnAvatarInvalidated;
    }

    /// <summary>
    /// How long the client is taking to acknowledge our writes, and how many it never did.
    /// </summary>
    /// <remarks>
    /// Owned here because this is the only object that holds both ends of the measurement: the moment a
    /// write was registered and the moment its echo matched. A second component timing the same thing
    /// would have to observe the wire twice and would get a different answer.
    /// </remarks>
    public VrcEchoTimings Timings { get; } = new();

    /// <summary>How many writes are currently waiting for an echo.</summary>
    /// <remarks>
    /// Read without the lock on purpose: it is the ingress path's fast exit, and at face-tracking rates
    /// the common case is zero. A stale read costs one unnecessary dictionary probe and nothing else.
    /// </remarks>
    public int PendingCount => Volatile.Read(ref _pendingCount);

    /// <summary>
    /// Registers a write, capturing the current avatar epoch. Call this <b>before</b> dispatch.
    /// </summary>
    /// <remarks>
    /// Before, not after, because the echo can arrive while the <c>await</c> on the socket send is still
    /// unwinding. Registering afterwards is a race whose symptom is an occasional unexplained timeout on
    /// an otherwise healthy connection.
    /// </remarks>
    public VrcEchoWait Register(Guid operationId, SignalKey key, SignalValue expected)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // The stamp is taken here rather than at the socket for the reason this method is called before
        // dispatch at all: the echo can arrive while the send is still unwinding, so the only start time
        // that cannot be later than its own end is the one taken before the write leaves.
        var wait = new VrcEchoWait(
            new VrcPendingEcho(operationId, key, expected, _epoch.Capture(), Stopwatch.GetTimestamp()));

        lock (_gate)
        {
            _pending[operationId] = wait;
            _pendingCount = _pending.Count;
        }

        return wait;
    }

    /// <summary>
    /// Offers an observation to every waiting write. True when it confirmed at least one.
    /// </summary>
    /// <remarks>
    /// Called from the OSC receive loop for every avatar-parameter observation, so the no-pending case
    /// must cost nothing: it is one volatile read and a return.
    /// </remarks>
    public bool TryConfirm(SignalKey key, in SignalValue value)
    {
        if (Volatile.Read(ref _pendingCount) == 0)
        {
            return false;
        }

        var epoch = _epoch.Current;
        List<VrcEchoWait>? matched = null;

        lock (_gate)
        {
            foreach (var wait in _pending.Values)
            {
                // The epoch check is what makes this better than a value comparison alone: an echo that
                // matches by value on a NEW avatar must never confirm a write made to the old one.
                if (wait.Pending.AvatarEpoch != epoch
                    || !wait.Pending.Key.Equals(key)
                    || wait.Pending.Expected != value)
                {
                    continue;
                }

                matched ??= [];
                matched.Add(wait);
            }

            if (matched is null)
            {
                return false;
            }

            foreach (var wait in matched)
            {
                _pending.Remove(wait.Pending.OperationId);
            }

            _pendingCount = _pending.Count;
        }

        // Outside the lock, with the rest of the completion work. One stamp for the whole batch: a bundle
        // that confirms four waits confirmed them in the same datagram, and reading the clock four times
        // would report four slightly different round trips for one arrival.
        var confirmedAt = Stopwatch.GetTimestamp();

        foreach (var wait in matched)
        {
            Timings.Record(
                Stopwatch.GetElapsedTime(wait.Pending.RegisteredTicks, confirmedAt).TotalMilliseconds);

            wait.Source.TrySetResult(VrcEchoStatus.Matched);
        }

        return true;
    }

    /// <summary>Abandons a registration whose dispatch never reached the socket.</summary>
    public void Cancel(Guid operationId)
    {
        VrcEchoWait? wait;
        lock (_gate)
        {
            if (!_pending.Remove(operationId, out wait))
            {
                return;
            }

            _pendingCount = _pending.Count;
        }

        wait.Source.TrySetResult(VrcEchoStatus.Cancelled);
    }

    /// <summary>Waits for <paramref name="wait"/> to be confirmed, invalidated, or to expire.</summary>
    /// <remarks>
    /// Separate from <see cref="Register"/> so the dispatcher never blocks: §12.5 wants the intent on the
    /// occurrence tape immediately (<c>CommandDispatched</c>) and the outcome whenever it arrives
    /// (<c>CommandCompleted</c> / <c>CommandFailed</c>). A caller that does not care simply never awaits.
    /// </remarks>
    public async Task<VrcEchoStatus> WaitAsync(VrcEchoWait wait, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(wait);

        try
        {
            return await wait.Completion.WaitAsync(_timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            Forget(wait.Pending.OperationId);

            // Counted, and contributing no sample. See VrcEchoTimings: a write that never came back is a
            // different fact from a slow one, and the timeout interval is not a measurement of anything.
            Timings.RecordTimeout();
            wait.Source.TrySetResult(VrcEchoStatus.TimedOut);
            return await wait.Completion.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Forget(wait.Pending.OperationId);
            wait.Source.TrySetResult(VrcEchoStatus.Cancelled);
            return await wait.Completion.ConfigureAwait(false);
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
        _epoch.Invalidated -= OnAvatarInvalidated;
        CompleteAll(VrcEchoStatus.Cancelled);
    }

    private void OnAvatarInvalidated(VrcAvatarInvalidated change) => CompleteAll(VrcEchoStatus.AvatarChanged);

    private void CompleteAll(VrcEchoStatus status)
    {
        VrcEchoWait[] waits;
        lock (_gate)
        {
            if (_pending.Count == 0)
            {
                return;
            }

            waits = [.. _pending.Values];
            _pending.Clear();
            _pendingCount = 0;
        }

        foreach (var wait in waits)
        {
            wait.Source.TrySetResult(status);
        }
    }

    private void Forget(Guid operationId)
    {
        lock (_gate)
        {
            if (_pending.Remove(operationId))
            {
                _pendingCount = _pending.Count;
            }
        }
    }
}
