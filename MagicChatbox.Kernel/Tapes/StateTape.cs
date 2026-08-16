using System.Collections.Immutable;
using MagicChatbox.Vocabulary;

namespace MagicChatbox.Kernel;

/// <summary>
/// Fan-out for state changes: a copy-on-write subscriber array, grant-filtered in the kernel.
/// </summary>
/// <remarks>
/// <para>
/// v2 called <c>OnNext</c> synchronously on the writer's thread and filtered per key with a linear scan
/// over an <c>ImmutableArray&lt;Subscription&gt;</c> on <i>every publish</i> — O(subscribers) at 600+
/// events/sec, with a slow subscriber directly stalling the writer. Here the array is swapped only on
/// subscribe and unsubscribe (a page load, a module load — rare), every sink is a kernel-owned mailbox,
/// and a publish is a dictionary upsert or a <c>TryWrite</c>.
/// </para>
/// <para>
/// <b>Publication happens outside every stripe lock, and the reason is lock hold time, not
/// re-entrancy.</b> Fan-out to N sinks inside a stripe lock would serialize every writer hashing to
/// that stripe behind N mailbox writes plus N <c>try</c>/<c>catch</c> frames, turning a ~100 ns critical
/// section into a ~5 µs one — a fiftyfold increase in the contention window on the hot path. That
/// dispatch also cannot re-enter the store is a secondary benefit, and saying it first is how the
/// comment gets deleted by the next reader who notices the sinks are all kernel-owned.
/// </para>
/// <para>
/// The design also called for a per-key subscriber index. It is deliberately not here: every grant in
/// this system is a prefix pattern rather than an exact key, so a per-key dictionary would index
/// nothing, and the subscriber count is on the order of ten. Add it when a profile shows the linear
/// scan costing something — not before.
/// </para>
/// </remarks>
public sealed class StateTape : IStateTape
{
    private readonly Lock _subscriberGate = new();
    private readonly KernelHealth _health;
    private readonly IOccurrenceRecorder? _recorder;
    private readonly TimeProvider _time;

    private ImmutableArray<Subscription> _subscribers = ImmutableArray<Subscription>.Empty;

    /// <summary>Creates a tape. The recorder receives the <c>SinkEjected</c> occurrence on ejection (D12).</summary>
    public StateTape(KernelHealth health, IOccurrenceRecorder? recorder = null, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(health);

        _health = health;
        _recorder = recorder;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <summary>How many sinks are currently in the fan-out.</summary>
    public int SubscriberCount => _subscribers.Length;

    /// <inheritdoc />
    public IDisposable Subscribe(IStateSink sink, GrantSet grants)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(grants);

        var subscription = new Subscription(this, sink, grants);
        lock (_subscriberGate)
        {
            _subscribers = _subscribers.Add(subscription);
        }

        return subscription;
    }

    /// <summary>Delivers one change to every sink whose grant admits the key.</summary>
    internal void Publish(in SignalChanged e)
    {
        var subscribers = _subscribers;
        List<Subscription>? ejected = null;

        foreach (var subscription in subscribers)
        {
            if (subscription.IsDisposed || !subscription.Grants.CanRead(e.Key))
            {
                continue;
            }

            try
            {
                subscription.Sink.OnSignalChanged(in e);
                subscription.ResetFailures();
            }
            catch (Exception ex)
            {
                // A throwing sink must not starve the sinks after it in the loop. v2 wrapped this too;
                // what v2 did not do is ever remove the sink, so a permanently broken one threw on
                // every publish forever.
                if (subscription.RecordFailure(ex))
                {
                    (ejected ??= []).Add(subscription);
                }
            }
        }

        if (ejected is null)
        {
            return;
        }

        foreach (var subscription in ejected)
        {
            Eject(subscription);
        }
    }

    private void Eject(Subscription subscription)
    {
        subscription.Dispose();

        var ejection = new SinkEjection(
            subscription.Sink.GetType().Name,
            nameof(StateTape),
            subscription.LastFailure,
            _time.GetTimestamp());

        _health.RecordEjection(in ejection);

        _recorder?.Record(
            OccurrenceKind.SinkEjected,
            KernelActor.Kernel,
            Correlation.For("kernel.sink.ejected"),
            KernelHealth.EjectionReason,
            $"{ejection.Sink} threw {Subscription.FailureLimit} times running on the state tape and was " +
            $"removed from the fan-out. Last error: {ejection.Detail}");
    }

    private void Remove(Subscription subscription)
    {
        lock (_subscriberGate)
        {
            _subscribers = _subscribers.Remove(subscription);
        }
    }

    private sealed class Subscription(StateTape owner, IStateSink sink, GrantSet grants) : IDisposable
    {
        public const int FailureLimit = 3;

        private int _consecutiveFailures;
        private volatile bool _disposed;

        public IStateSink Sink { get; } = sink;

        public GrantSet Grants { get; } = grants;

        public string? LastFailure { get; private set; }

        public bool IsDisposed => _disposed;

        public void ResetFailures() => _consecutiveFailures = 0;

        /// <summary>True when this failure was the one that crossed the ejection threshold.</summary>
        public bool RecordFailure(Exception ex)
        {
            LastFailure = ex.Message;
            return ++_consecutiveFailures == FailureLimit;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            owner.Remove(this);
        }
    }
}
