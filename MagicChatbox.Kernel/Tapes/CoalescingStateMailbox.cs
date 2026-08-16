using System.Runtime.InteropServices;
using MagicChatbox.Vocabulary;

namespace MagicChatbox.Kernel;

/// <summary>
/// Latest-wins per key, by version, with all-or-nothing transaction grouping at the drain.
/// </summary>
/// <remarks>
/// <para>
/// The mailbox behind the SSE state channel and the composer. Coalescing here is lossless by
/// definition — for a state fact, the latest value is the whole truth — and a consumer that needs every
/// sample of a continuous key subscribes a <see cref="LosslessStateMailbox"/> instead.
/// </para>
/// <para>
/// <b>Latest-wins is BY VERSION, not by arrival.</b> Two threads writing the same key can reach a sink
/// in the opposite order to their versions; an inverted arrival is discarded here and never becomes
/// resident, so a slider cannot end up permanently showing the older of two values.
/// </para>
/// <para><b>D1 — the transaction-grouping livelock, and the three parts of its fix.</b></para>
/// <para>
/// The superseded design withheld a transaction's keys until the member carrying <c>IsBatchEnd</c>
/// arrived, while also overwriting slots latest-wins. Those two rules collide: an ordinary ungrouped
/// write to the key that happened to be the batch's last member destroys the only slot that could ever
/// close the group, and the other thirty-six keys are withheld forever. No exception, no log line, no
/// counter — the composer just silently freezes on that source. That is a real pattern in the one
/// module that exists: MediaPilot ticks <c>position_seconds</c> every second, independently of the
/// snapshot batch it also belongs to.
/// </para>
/// <list type="number">
/// <item><description>
/// Transaction bookkeeping is a <b>side table updated on arrival</b>, never re-derived from the slots.
/// The slots are latest-wins storage; group membership is accounting. Conflating them was the root
/// cause.
/// </description></item>
/// <item><description>
/// <b>Group size is stamped by the writer</b>, so closure depends on a count of distinct member keys
/// seen and not on any one slot surviving.
/// </description></item>
/// <item><description>
/// <b>A bounded withhold.</b> After <see cref="MaxWithheldDrains"/> drains a still-open transaction is
/// force-released and <see cref="ForcedReleases"/> is incremented. A forced release can produce one
/// torn frame; a livelock produces permanent silence. A visible, counted, rare tear beats an invisible
/// freeze.
/// </description></item>
/// </list>
/// </remarks>
public sealed class CoalescingStateMailbox : IStateSink
{
    /// <summary>Roughly 132 ms at the 33 ms drain cadence — below the threshold anybody can perceive.</summary>
    public const int MaxWithheldDrains = 4;

    private readonly Lock _gate = new();
    private readonly Dictionary<SignalKey, SignalChanged> _slots = [];
    private readonly Dictionary<Guid, TransactionState> _open = [];
    private readonly IOccurrenceRecorder? _recorder;

    private long _forcedReleases;

    /// <summary>
    /// Creates a mailbox. The recorder, when supplied, receives a <c>WriteFailed</c> occurrence for
    /// every forced release, so the safety valve firing is visible on the Audit screen rather than only
    /// in a counter somebody has to think to look at.
    /// </summary>
    public CoalescingStateMailbox(IOccurrenceRecorder? recorder = null) => _recorder = recorder;

    /// <summary>Keys currently withheld or waiting for the next drain.</summary>
    public int PendingKeys
    {
        get
        {
            lock (_gate)
            {
                return _slots.Count;
            }
        }
    }

    /// <summary>
    /// How many transactions the bounded withhold has force-released. Alarm on any non-zero value: it
    /// means a writer stamped a size it did not publish, or a member's write was rejected after its
    /// siblings' were accepted.
    /// </summary>
    public long ForcedReleases
    {
        get
        {
            lock (_gate)
            {
                return _forcedReleases;
            }
        }
    }

    /// <summary>Transactions seen but not yet complete.</summary>
    public int OpenTransactions
    {
        get
        {
            lock (_gate)
            {
                return _open.Count;
            }
        }
    }

    /// <inheritdoc />
    public void OnSignalChanged(in SignalChanged e)
    {
        lock (_gate)
        {
            // An inverted arrival is discarded, never resident — but its membership still counts, or
            // the ordering inversion would cost the group a member and stall it until the withhold.
            if (!_slots.TryGetValue(e.Key, out var existing) || e.Version > existing.Version)
            {
                _slots[e.Key] = e;
            }

            TrackMembership(in e);
        }
    }

    /// <summary>
    /// Moves every releasable change into <paramref name="buffer"/> and returns how many were written.
    /// </summary>
    /// <remarks>
    /// A transaction is released whole or not at all. If <paramref name="buffer"/> is too small for
    /// everything pending, the remainder stays for the next drain — but a released transaction's
    /// bookkeeping is retired regardless, so a short buffer cannot re-block a group it already cleared.
    /// </remarks>
    public int DrainInto(Span<SignalChanged> buffer)
    {
        List<Guid>? forced = null;
        int drained;

        lock (_gate)
        {
            HashSet<Guid>? blocked = null;
            List<Guid>? finished = null;

            foreach (var (id, state) in _open)
            {
                if (state.IsClosed)
                {
                    (finished ??= []).Add(id);
                    continue;
                }

                if (++state.DrainsWithheld >= MaxWithheldDrains)
                {
                    _forcedReleases++;
                    (finished ??= []).Add(id);
                    (forced ??= []).Add(id);
                    continue;
                }

                (blocked ??= []).Add(id);
            }

            var n = 0;
            foreach (var (_, change) in _slots)
            {
                if (blocked is not null && change.InTransaction && blocked.Contains(change.TransactionId))
                {
                    continue;
                }

                if (n == buffer.Length)
                {
                    break;
                }

                buffer[n++] = change;
            }

            for (var i = 0; i < n; i++)
            {
                _slots.Remove(buffer[i].Key);
            }

            if (finished is not null)
            {
                foreach (var id in finished)
                {
                    _open.Remove(id);
                }
            }

            drained = n;
        }

        // Outside the lock: a recorder is arbitrary code and must never run under a mailbox's gate.
        // The counter is what a test asserts on; the occurrence is what a user sees.
        if (forced is not null)
        {
            ReportForced(forced, drained);
        }

        return drained;
    }

    private void ReportForced(List<Guid> forced, int drained)
    {
        if (_recorder is null)
        {
            return;
        }

        foreach (var id in forced)
        {
            _recorder.Record(
                OccurrenceKind.WriteFailed,
                KernelActor.Kernel,
                new Correlation(Guid.NewGuid(), id, "kernel.transaction.forced_release"),
                ReasonCode.SourceFaulted,
                $"Transaction {id} was force-released after {MaxWithheldDrains} drains with members " +
                $"still missing; {drained} change(s) went out possibly torn.");
        }
    }

    /// <summary>Caller holds <see cref="_gate"/>.</summary>
    private void TrackMembership(in SignalChanged e)
    {
        if (!e.InTransaction)
        {
            // D1(ii), corrected. An earlier revision credited an ungrouped write to EVERY open
            // transaction, reasoning that a newer value supersedes the group's member. The reasoning is
            // sound and the implementation was not: `Seen` is counted against `Size`, so unrelated
            // ungrouped writes inflated a foreign transaction's count until it tripped closed and a
            // genuinely partial group was released — D1's own failure, reintroduced by D1's fix.
            //
            // Doing it correctly needs the writer to stamp the full member key set, allocating a set
            // per batch on the write path to buy an optimisation rather than a correctness property.
            // So ungrouped writes touch transaction bookkeeping not at all, and a member that never
            // arrives is released by the bounded withhold, which is the designed safety valve for
            // exactly this case. Four drains of staleness nobody can perceive beats a partial group,
            // which renders a new title beside an old artist and ships it to VRChat as a visible lie.
            return;
        }

        // The out parameter is `exists`, and it is FALSE on the call that adds the entry. The design's
        // pseudo-code names it `isNew` and stamps the size under `if (isNew)`, which stamps it on every
        // arrival EXCEPT the first — leaving Size at zero, so `Seen.Count >= Size` is true immediately
        // and the very first member releases a group of thirty-seven. That is D1's failure with the
        // sign flipped, and it is silent: the group drains, torn, and nothing counts it.
        ref var state = ref CollectionsMarshal.GetValueRefOrAddDefault(_open, e.TransactionId, out var exists);
        if (!exists || state is null)
        {
            state = new TransactionState { Size = e.TransactionSize };
        }

        state.Seen.Add(e.Key);
    }

    private sealed class TransactionState
    {
        public int DrainsWithheld;

        public required ushort Size { get; init; }

        public HashSet<SignalKey> Seen { get; } = [];

        public bool IsClosed => Seen.Count >= Size;
    }
}
