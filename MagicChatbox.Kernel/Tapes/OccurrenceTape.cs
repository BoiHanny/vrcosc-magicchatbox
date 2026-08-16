using System.Collections.Immutable;
using MagicChatbox.Vocabulary;

namespace MagicChatbox.Kernel;

/// <summary>
/// Fan-out and replay for the occurrence tape, and the recorder every producer writes through.
/// </summary>
/// <remarks>
/// Same fan-out discipline as <see cref="StateTape"/>: a copy-on-write subscriber array, grant filtered
/// in the kernel, every invocation wrapped, three consecutive throws and the sink is ejected.
/// <para>
/// The replay ring holds the last <see cref="RingCapacity"/> occurrences so an SSE client reconnecting
/// with a <c>Last-Event-ID</c> can catch up. It is a reconnect convenience and not a durability
/// guarantee: a client that falls further behind finds a gap in <c>Seq</c> and must reload from the
/// durable ledger, and making the ring deep enough to hide that would only postpone building the path
/// the frontend needs anyway.
/// </para>
/// </remarks>
public sealed class OccurrenceTape : IOccurrenceTape, IOccurrenceRecorder
{
    /// <summary>256, matching the per-subscriber channel bound.</summary>
    public const int RingCapacity = 256;

    private readonly Lock _subscriberGate = new();
    private readonly Lock _ringGate = new();
    private readonly Occurrence?[] _ring = new Occurrence?[RingCapacity];
    private readonly KernelSequence _sequence;
    private readonly KernelHealth _health;
    private readonly TimeProvider _time;

    /// <summary>
    /// The <c>(UtcNow, Timestamp)</c> pair every occurrence's tick count is measured against.
    /// </summary>
    /// <remarks>
    /// Read off the same <see cref="TimeProvider"/> that stamps <see cref="Occurrence.Timestamp"/>, one
    /// line apart, which is the whole of what makes <see cref="ProjectUtc"/> correct. Two clocks — a wall
    /// clock from here and a tick count from somewhere else — would be a projection whose error is the
    /// unknown offset between them, and it would look right on the developer's machine.
    /// </remarks>
    private readonly DateTimeOffset _anchorUtc;
    private readonly long _anchorTimestamp;

    private ImmutableArray<Subscription> _subscribers = ImmutableArray<Subscription>.Empty;
    private int _ringNext;

    [ThreadStatic]
    private static bool _reportingEjection;

    /// <summary>Creates a tape sharing the kernel's sequence counter and health.</summary>
    public OccurrenceTape(KernelSequence sequence, KernelHealth health, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        ArgumentNullException.ThrowIfNull(health);

        _sequence = sequence;
        _health = health;
        _time = timeProvider ?? TimeProvider.System;
        _anchorUtc = _time.GetUtcNow();
        _anchorTimestamp = _time.GetTimestamp();
    }

    /// <summary>How many sinks are currently in the fan-out.</summary>
    public int SubscriberCount => _subscribers.Length;

    /// <inheritdoc />
    public DateTimeOffset ProjectUtc(long timestamp) =>
        _anchorUtc + _time.GetElapsedTime(_anchorTimestamp, timestamp);

    /// <inheritdoc />
    public IDisposable Subscribe(IOccurrenceSink sink, GrantSet grants, OccurrenceFilter filter)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(grants);

        var subscription = new Subscription(this, sink, grants, filter);
        lock (_subscriberGate)
        {
            _subscribers = _subscribers.Add(subscription);
        }

        return subscription;
    }

    /// <inheritdoc />
    public IReadOnlyList<Occurrence> Replay(long afterSeq, int max = RingCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(max, 1);

        var result = new List<Occurrence>(Math.Min(max, RingCapacity));
        lock (_ringGate)
        {
            // Oldest first: the ring is written forward, so the slot after the write cursor is the
            // oldest surviving entry.
            for (var i = 0; i < RingCapacity && result.Count < max; i++)
            {
                if (_ring[(_ringNext + i) % RingCapacity] is { } occurrence && occurrence.Seq > afterSeq)
                {
                    result.Add(occurrence);
                }
            }
        }

        return result;
    }

    /// <inheritdoc />
    public void Record(
        OccurrenceKind kind,
        in KernelActor actor,
        in Correlation correlation,
        ReasonCode reason,
        string? detail,
        ImmutableArray<SignalTransition> transitions = default)
    {
        var occurrence = new Occurrence(
            _sequence.Next(),
            kind,
            actor,
            correlation,
            _time.GetTimestamp(),
            transitions.IsDefault ? ImmutableArray<SignalTransition>.Empty : transitions,
            reason,
            detail);

        Publish(occurrence);
    }

    private void Publish(Occurrence occurrence)
    {
        lock (_ringGate)
        {
            _ring[_ringNext] = occurrence;
            _ringNext = (_ringNext + 1) % RingCapacity;
        }

        var subscribers = _subscribers;
        List<Subscription>? ejected = null;

        foreach (var subscription in subscribers)
        {
            if (subscription.IsDisposed
                || !subscription.Filter.Admits(occurrence)
                || !Admits(subscription.Grants, occurrence))
            {
                continue;
            }

            try
            {
                subscription.Sink.OnOccurrence(occurrence);
                subscription.ResetFailures();
            }
            catch (Exception ex)
            {
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

    /// <summary>
    /// A keyless occurrence — a whole-source failure, a sink ejection — reaches every subscriber. It
    /// names no key, so there is no key to withhold, and it is exactly the kind of thing a restricted
    /// subscriber most needs to see.
    /// </summary>
    private static bool Admits(GrantSet grants, Occurrence occurrence)
    {
        if (occurrence.Transitions.IsDefaultOrEmpty)
        {
            return true;
        }

        foreach (var transition in occurrence.Transitions)
        {
            if (grants.CanRead(transition.Key))
            {
                return true;
            }
        }

        return false;
    }

    private void Eject(Subscription subscription)
    {
        subscription.Dispose();

        var ejection = new SinkEjection(
            subscription.Sink.GetType().Name,
            nameof(OccurrenceTape),
            subscription.LastFailure,
            _time.GetTimestamp());

        _health.RecordEjection(in ejection);

        if (_reportingEjection)
        {
            // The SinkEjected occurrence goes on this same tape, so a sink that throws while being told
            // about an ejection would recurse. One level is all that is useful: the ejection is already
            // recorded in health, which is what the status bar reads.
            return;
        }

        _reportingEjection = true;
        try
        {
            Record(
                OccurrenceKind.SinkEjected,
                KernelActor.Kernel,
                Correlation.For("kernel.sink.ejected"),
                KernelHealth.EjectionReason,
                $"{ejection.Sink} threw {Subscription.FailureLimit} times running on the occurrence tape " +
                $"and was removed from the fan-out. Last error: {ejection.Detail}");
        }
        finally
        {
            _reportingEjection = false;
        }
    }

    private void Remove(Subscription subscription)
    {
        lock (_subscriberGate)
        {
            _subscribers = _subscribers.Remove(subscription);
        }
    }

    private sealed class Subscription(
        OccurrenceTape owner,
        IOccurrenceSink sink,
        GrantSet grants,
        OccurrenceFilter filter) : IDisposable
    {
        public const int FailureLimit = 3;

        private int _consecutiveFailures;
        private volatile bool _disposed;

        public IOccurrenceSink Sink { get; } = sink;

        public GrantSet Grants { get; } = grants;

        public OccurrenceFilter Filter { get; } = filter;

        public string? LastFailure { get; private set; }

        public bool IsDisposed => _disposed;

        public void ResetFailures() => _consecutiveFailures = 0;

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
