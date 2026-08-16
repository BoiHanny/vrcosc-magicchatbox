using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics.Metrics;
using MagicChatbox.Vocabulary;

namespace MagicChatbox.Kernel;

/// <summary>
/// Every fact the application knows, one cell per key, with a write path nobody can route around.
/// </summary>
/// <remarks>
/// <para>
/// <b>Concurrency.</b> 64 power-of-two per-key stripe locks cover only read-compare-version-write.
/// <b>Version and Seq are both stamped inside the stripe lock</b>, so same-key writes receive sequence
/// numbers in write order — v2 stamped the version under the stripe lock and the sequence under a
/// different gate afterwards, which is why two threads writing one key could reach the publisher
/// inverted (audit F-010). <b>Publish happens outside every lock</b>, because fan-out inside a stripe
/// would turn a ~100 ns critical section into a ~5 µs one on the hot path.
/// </para>
/// <para>
/// <b>D9 — batch atomicity is PUBLISH atomicity, not STORE atomicity.</b> Stripes are taken
/// hand-over-hand and released before the next is acquired, so a concurrent <see cref="TryRead"/> or
/// <see cref="Snapshot()"/> executed while a batch is mid-apply <b>can observe a partial group</b>. That
/// is true, intentional and cheap. The guarantee consumers actually get — and the only one the composer
/// needs — is that the coalescing drain never delivers a partial transaction. Buying real store
/// atomicity costs a global reader-writer lock on the 2,700/sec ingress path. <b>Do not "fix" this by
/// holding all stripes.</b>
/// </para>
/// <para>
/// <b>The kernel owns no timer.</b> <see cref="SweepStaleness"/> is called from the host's existing
/// 33 ms tick. Nothing here starts a thread, schedules a callback or allocates a <c>Task</c>.
/// </para>
/// </remarks>
public sealed class SignalStore : IDisposable
{
    private const int RejectionThrottleCapacity = 1024;

    private static readonly KeyValuePair<string, object?> AcceptedTag = new("status", "Accepted");
    private static readonly KeyValuePair<string, object?> NoChangeTag = new("status", "NoChange");
    private static readonly KeyValuePair<string, object?> RejectedTag = new("status", "Rejected");
    private static readonly ConcurrentDictionary<ReasonCode, KeyValuePair<string, object?>> ReasonTags = new();
    private static readonly SignalNamespace[] Namespaces = Enum.GetValues<SignalNamespace>();

    private readonly ConcurrentDictionary<SignalKey, Cell> _cells = new();
    private readonly ConcurrentDictionary<SignalKey, long> _lastRejectionReport = new();
    private readonly Lock[] _stripes;
    private readonly int _stripeMask;
    private readonly TimeProvider _time;
    private readonly FrozenDictionary<SignalNamespace, int> _caps;
    private readonly int[] _cellsByNamespace = new int[Namespaces.Length];
    private readonly long _rejectionWindowTicks;

    private readonly Lock _staleGate = new();
    private SignalKey[] _staleTracked = [];
    private long[] _staleTicks = [];
    private int _staleRegistryVersion = -1;

    private long _accepted;
    private long _noChange;
    private long _rejected;
    private long _observationsAccepted;
    private long _observationsRejected;
    private long _nonFiniteRejected;
    private long _textOnObservePathRejected;
    private long _namespaceCapRejected;
    private long _kindMismatchRejected;
    private long _unknownKeyRejected;
    private long _stalenessFlips;
    private long _cellsRemoved;

    /// <summary>
    /// Builds a store and the tapes that go with it.
    /// </summary>
    /// <param name="options">Test-facing knobs. Every default is the production value.</param>
    /// <param name="registry">
    /// An existing registry, when the host built one already. When omitted the store builds one wired to
    /// its own occurrence tape, so an illegal descriptor reaches the Audit screen without further
    /// assembly.
    /// </param>
    public SignalStore(SignalStoreOptions? options = null, DescriptorRegistry? registry = null)
    {
        var opts = options ?? new SignalStoreOptions();
        if (opts.StripeCount <= 0 || (opts.StripeCount & (opts.StripeCount - 1)) != 0)
        {
            throw new ArgumentException("StripeCount must be a power of two.", nameof(options));
        }

        _time = opts.TimeProvider;
        _caps = opts.NamespaceCellCaps;
        _rejectionWindowTicks = (long)(opts.RejectionReportIntervalMs / 1000.0 * _time.TimestampFrequency);

        _stripes = new Lock[opts.StripeCount];
        for (var i = 0; i < _stripes.Length; i++)
        {
            _stripes[i] = new Lock();
        }

        _stripeMask = opts.StripeCount - 1;

        Sequence = new KernelSequence();
        Health = new KernelHealth();
        OccurrenceTape = new OccurrenceTape(Sequence, Health, _time);
        StateTape = new StateTape(Health, OccurrenceTape, _time);
        Descriptors = registry ?? new DescriptorRegistry(OccurrenceTape);
        Metrics = new KernelMetrics(opts.MeterName, this, Health);
    }

    /// <summary>The descriptor registry this store reads on every write.</summary>
    public DescriptorRegistry Descriptors { get; }

    /// <summary>The state tape. Subscribe a kernel-owned mailbox to it.</summary>
    public StateTape StateTape { get; }

    /// <summary>The occurrence tape, which is also the recorder Core and Vrc write command lifecycle through.</summary>
    public OccurrenceTape OccurrenceTape { get; }

    /// <summary>The shared sequence counter behind every <c>Seq</c>.</summary>
    public KernelSequence Sequence { get; }

    /// <summary>Sink ejections and the resulting degraded state (D12).</summary>
    public KernelHealth Health { get; }

    /// <summary>The meter. Disposed with the store.</summary>
    public KernelMetrics Metrics { get; }

    /// <summary>How many cells exist right now.</summary>
    public int CellCount => _cells.Count;

    /// <summary>Everything the store counts, as one reading.</summary>
    public SignalStoreCounters Counters => new(
        Interlocked.Read(ref _accepted),
        Interlocked.Read(ref _noChange),
        Interlocked.Read(ref _rejected),
        Interlocked.Read(ref _observationsAccepted),
        Interlocked.Read(ref _observationsRejected),
        Interlocked.Read(ref _nonFiniteRejected),
        Interlocked.Read(ref _textOnObservePathRejected),
        Interlocked.Read(ref _namespaceCapRejected),
        Interlocked.Read(ref _kindMismatchRejected),
        Interlocked.Read(ref _unknownKeyRejected),
        Interlocked.Read(ref _stalenessFlips),
        Interlocked.Read(ref _cellsRemoved));

    /// <summary>Reads one cell. Lock-free.</summary>
    public bool TryRead(SignalKey key, out Cell cell) => _cells.TryGetValue(key, out cell);

    /// <summary>Every cell, stamped with the sequence number it was taken at.</summary>
    /// <remarks>May observe a partially-applied batch — see the type remarks (D9).</remarks>
    public StateSnapshot Snapshot() => new(Sequence.Current, _cells.ToImmutableDictionary());

    /// <summary>Every cell the grant permits reading.</summary>
    public StateSnapshot Snapshot(GrantSet grants)
    {
        ArgumentNullException.ThrowIfNull(grants);

        if (grants.IsUnrestricted && !grants.IsRevoked)
        {
            return Snapshot();
        }

        var builder = ImmutableDictionary.CreateBuilder<SignalKey, Cell>();
        foreach (var (key, cell) in _cells)
        {
            if (grants.CanRead(key))
            {
                builder[key] = cell;
            }
        }

        return new StateSnapshot(Sequence.Current, builder.ToImmutable());
    }

    /// <summary>
    /// The hot path: readings reported by a source, roughly 2,700 a second at face-tracking load.
    /// </summary>
    /// <remarks>
    /// Policy is short-circuited entirely — this is the transport reporting what VRChat said, and there
    /// is no decision to make about a fact that already happened. Three boundary checks remain, and each
    /// one closes a named hole:
    /// <list type="bullet">
    /// <item><description>
    /// <b>D7.</b> A <c>Text</c> descriptor is rejected <i>at runtime</i>, not merely at registration. The
    /// registry is flat and key-owned rather than source-owned, so a higher-ranked upsert can install a
    /// Text descriptor over an observe-path key and no registration guard would ever fire. This branch
    /// costs nothing: the method already switches on kind to coerce.
    /// </description></item>
    /// <item><description>
    /// <b>D4.</b> A non-finite float is rejected. <c>Math.Abs(NaN - NaN) &lt; 1e-6</c> is false, so an
    /// accepted NaN never dedupes: it bumps the version and publishes on <i>every single observation</i>,
    /// defeating the throttle and rendering as <c>NaN°C</c> in the chatbox. Both failure modes this
    /// design exists to prevent, through one property of IEEE-754.
    /// </description></item>
    /// <item><description>
    /// A value the conversion matrix will not coerce to the declared kind is rejected rather than stored
    /// under a type nobody declared.
    /// </description></item>
    /// </list>
    /// <para>Steady state on an accepted continuous reading: zero heap allocations.</para>
    /// </remarks>
    /// <returns>How many readings were accepted.</returns>
    public int Observe(ReadOnlySpan<Observation> batch, in KernelActor actor) =>
        ApplyObservations(batch, in actor, onlyIfAbsent: false, cause: "transport.observe");

    /// <summary>
    /// Fills in cells that have never been written, from an enumeration rather than from the wire.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A seed never overwrites a cell that exists.</b> This is the whole safety argument, and it is
    /// deliberately cruder than the obvious alternative. The tempting rule — "overwrite only if the seed
    /// is newer than the cell" — cannot be implemented against <see cref="Cell.Timestamp"/>, because that
    /// field records when the key last <i>changed</i>, not when the wire last <i>confirmed</i> it: a
    /// deduping write returns before the timestamp is stamped. So a parameter that is constantly
    /// re-reported but never changes keeps its original timestamp forever and would lose every such
    /// comparison. Since OSCQuery returns compiled defaults for a value the client has not computed yet,
    /// that rule would silently replace a correct live reading with a default, and the skip counter would
    /// read zero the whole time.
    /// </para>
    /// <para>
    /// The division of labour is the protocol's own: <b>OSCQuery enumerates, the wire values.</b> Seeding
    /// answers "what exists, and what was it when we asked" for keys nothing has reported yet — which is
    /// the entire gap between a parameter the user owns and a parameter the application knows about.
    /// </para>
    /// <para>
    /// Every boundary check <see cref="Observe"/> makes is made here too, by construction — same
    /// descriptor lookup, same text fence, same finiteness rule, same coercion.
    /// </para>
    /// </remarks>
    /// <returns>How many cells were created. Submitted minus returned is how many the wire already owned.</returns>
    public int Seed(ReadOnlySpan<Observation> batch, in KernelActor actor) =>
        ApplyObservations(batch, in actor, onlyIfAbsent: true, cause: "transport.oscquery.harvest");

    private int ApplyObservations(
        ReadOnlySpan<Observation> batch,
        in KernelActor actor,
        bool onlyIfAbsent,
        string cause)
    {
        var effective = Descriptors.Effective;
        var accepted = 0;

        for (var i = 0; i < batch.Length; i++)
        {
            var key = batch[i].Key;
            var value = batch[i].Value;

            if (!effective.TryGetValue(key, out var descriptor))
            {
                RejectObservation(key, ReasonCode.UnknownKey, in actor);
                continue;
            }

            if (descriptor.Kind == SignalKind.Text)
            {
                RejectObservation(key, ReasonCode.TextOnObservePath, in actor);
                continue;
            }

            if (!value.IsFinite())
            {
                RejectObservation(key, ReasonCode.NonFiniteValue, in actor);
                continue;
            }

            if (value.Kind != descriptor.Kind)
            {
                if (!value.TryConvertTo(descriptor.Kind, out var coerced))
                {
                    RejectObservation(key, ReasonCode.KindMismatch, in actor);
                    continue;
                }

                value = coerced;
            }

            SignalChanged change;
            bool changed;
            ReasonCode failure;

            using (_stripes[key.Hash & _stripeMask].EnterScope())
            {
                changed = TryApplyLocked(
                    key, value, in actor, cause: null, Availability.Live, ReasonCode.Ok,
                    out change, out _, out failure, onlyIfAbsent);
            }

            if (changed)
            {
                accepted++;
                Interlocked.Increment(ref _observationsAccepted);
                Metrics.Mutations.Add(1, StatusTag(MutationStatus.Accepted));
                PublishChange(in change, descriptor.Temperament, cause);
                continue;
            }

            if (failure == ReasonCode.Ok)
            {
                Interlocked.Increment(ref _noChange);
                continue;
            }

            RejectObservation(key, failure, in actor);
        }

        return accepted;
    }

    /// <summary>
    /// One write. Synchronous, stripe-locked, published outside the lock.
    /// </summary>
    /// <remarks>
    /// There is no <c>MutateAsync</c>. v2's own comment says the synchronous form "is the real shape of
    /// the operation", and an async wrapper over a ~100 ns critical section buys a state machine
    /// allocation and a scheduling hop to hide nothing.
    /// </remarks>
    public MutationResult Mutate(in Mutation mutation, in KernelActor actor, in Correlation correlation, GrantSet grants)
    {
        ArgumentNullException.ThrowIfNull(grants);

        var decision = Authorize(in mutation, in actor, grants, out var descriptor, out var value);
        if (!decision.Allowed)
        {
            RecordRejection(mutation.Key, in actor, in correlation, decision);
            return MutationResult.Rejected(mutation.Key, decision.Reason);
        }

        SignalChanged change;
        bool changed;
        ReasonCode failure;

        using (_stripes[mutation.Key.Hash & _stripeMask].EnterScope())
        {
            changed = TryApplyLocked(
                mutation.Key, value, in actor, correlation.Cause, Availability.Live, ReasonCode.Ok,
                out change, out _, out failure);
        }

        if (changed)
        {
            Interlocked.Increment(ref _accepted);
            Metrics.Mutations.Add(1, StatusTag(MutationStatus.Accepted));
            PublishChange(in change, descriptor.Temperament, in correlation);
            return new MutationResult(MutationStatus.Accepted, mutation.Key, value, change.Version, ReasonCode.Ok);
        }

        if (failure != ReasonCode.Ok)
        {
            RecordRejection(mutation.Key, in actor, in correlation, WriteDecision.Deny(failure));
            return MutationResult.Rejected(mutation.Key, failure);
        }

        Interlocked.Increment(ref _noChange);
        Metrics.Mutations.Add(1, StatusTag(MutationStatus.NoChange));
        _cells.TryGetValue(mutation.Key, out var current);
        return new MutationResult(MutationStatus.NoChange, mutation.Key, value, current.Version, ReasonCode.Ok);
    }

    /// <summary>
    /// A batch, authorized all-or-nothing and published as one transaction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Phase A authorizes every member and touches nothing.</b> Authorization is pure — it reads the
    /// descriptor registry and the grant set and never reads a cell — so it can run entirely before any
    /// lock is taken, which is what removes the lock-cycle question without a global lock. If any member
    /// is refused, nothing is applied and the result names the offending key with its own reason.
    /// </para>
    /// <para>
    /// <b>Phase B applies hand-over-hand, one acquire per DISTINCT stripe (D2).</b> With 64 stripes and a
    /// 37-key batch the pigeonhole principle guarantees collisions — the odds of 37 keys landing on 37
    /// distinct stripes are under 0.01%. Stripe indices are therefore sorted and <b>deduplicated before
    /// anything is acquired</b>. Hand-over-hand rather than hold-all keeps the maximum
    /// simultaneously-held lock count at one, which makes lock-order analysis trivial, and hold-all
    /// would only buy the cross-key snapshot isolation this design explicitly does not offer (D9).
    /// </para>
    /// <para>
    /// <b>A correction to D2's stated premise, verified rather than assumed.</b> The design says
    /// <see cref="Lock"/> is not reentrant and that acquiring per mutation therefore self-deadlocks the
    /// writer thread. <b>That is false on .NET 10</b>: <c>System.Threading.Lock</c> counts recursion the
    /// way <c>Monitor</c> does, and a nested <c>EnterScope</c> on the same thread succeeds. The
    /// deduplication is kept anyway, and its real justifications are the ones that survive: it turns 37
    /// acquire/release pairs into at most 64, it makes the lock discipline legible instead of relying on
    /// a recursion counter, and it is what lets the inner loop hold one stripe across every key that
    /// hashes to it. The premise is corrected here rather than left in place, because a rationale that
    /// turns out to be false is how a correct guard gets deleted by the next reader who checks it.
    /// </para>
    /// <para>
    /// <b>Phase C publishes outside every lock</b>: one <see cref="SignalChanged"/> per changed key, all
    /// sharing a transaction id, plus one <c>BatchApplied</c> occurrence carrying every transition. The
    /// ledger sees one row with an N-key diff and revert is one action.
    /// </para>
    /// <para>
    /// <b>TransactionSize counts the members actually PUBLISHED, not the members submitted — a
    /// correction to the design's pseudo-code, and it matters.</b> MediaPilot re-publishes its whole
    /// 37-key snapshot on every SMTC event, and most of those keys are unchanged, so they dedupe and
    /// never reach a sink. Stamping <c>TransactionSize = batch.Length</c> would make the coalescing
    /// mailbox wait for members that were never going to arrive, and every single snapshot would sit out
    /// the full bounded withhold before being force-released. The size is therefore stamped in Phase C,
    /// once the set of published members is known.
    /// </para>
    /// </remarks>
    public BatchResult Mutate(
        ReadOnlySpan<Mutation> batch, in KernelActor actor, in Correlation correlation, GrantSet grants)
    {
        ArgumentNullException.ThrowIfNull(grants);

        if (batch.Length == 0)
        {
            return new BatchResult(MutationStatus.NoChange, ImmutableArray<SignalTransition>.Empty, default, ReasonCode.Ok);
        }

        if (batch.Length > ushort.MaxValue)
        {
            throw new ArgumentException(
                $"A transaction may carry at most {ushort.MaxValue} members.", nameof(batch));
        }

        // ── Phase A: authorize ALL. Pure. No cell is read; no lock is taken. ──────────────────────
        var prepared = new PreparedMutation[batch.Length];
        for (var i = 0; i < batch.Length; i++)
        {
            var decision = Authorize(in batch[i], in actor, grants, out var descriptor, out var value);
            if (!decision.Allowed)
            {
                RecordRejection(batch[i].Key, in actor, in correlation, decision);
                return BatchResult.Rejected(batch[i].Key, decision.Reason);
            }

            prepared[i] = new PreparedMutation(batch[i].Key, value, descriptor.Temperament);
        }

        // ── Phase B: apply, hand-over-hand, one acquire per distinct stripe. ──────────────────────
        Span<int> stripes = batch.Length <= 64 ? stackalloc int[batch.Length] : new int[batch.Length];
        for (var i = 0; i < prepared.Length; i++)
        {
            stripes[i] = prepared[i].Key.Hash & _stripeMask;
        }

        stripes.Sort();
        var distinct = Deduplicate(stripes);

        var staged = new List<SignalChanged>(prepared.Length);
        var stagedTemperaments = new List<Temperament>(prepared.Length);
        var transitions = ImmutableArray.CreateBuilder<SignalTransition>(prepared.Length);

        for (var s = 0; s < distinct; s++)
        {
            using (_stripes[stripes[s]].EnterScope())
            {
                for (var i = 0; i < prepared.Length; i++)
                {
                    if ((prepared[i].Key.Hash & _stripeMask) != stripes[s])
                    {
                        continue;
                    }

                    if (TryApplyLocked(
                            prepared[i].Key, prepared[i].Value, in actor, correlation.Cause,
                            Availability.Live, ReasonCode.Ok,
                            out var change, out var transition, out var failure))
                    {
                        staged.Add(change);
                        stagedTemperaments.Add(prepared[i].Temperament);
                        transitions.Add(transition);
                        continue;
                    }

                    if (failure != ReasonCode.Ok)
                    {
                        // The namespace cell cap is the one rejection Phase A cannot pre-authorize,
                        // because it depends on whether a cell already exists — which is state, and a
                        // pure phase may not read it. Checking it in Phase A would also be unsound:
                        // another writer can mint the cell between the phases. So a batch that hits a
                        // cap applies partially, counted and recorded. That is acceptable because
                        // reaching a cap means something is generating keys rather than naming them,
                        // which is a catastrophic condition, not a routine one.
                        RecordCapExhaustion(prepared[i].Key, in actor, in correlation, failure);
                    }
                }
            }
        }

        // ── Phase C: publish, outside every lock. ─────────────────────────────────────────────────
        if (staged.Count == 0)
        {
            Interlocked.Add(ref _noChange, prepared.Length);
            return new BatchResult(MutationStatus.NoChange, ImmutableArray<SignalTransition>.Empty, default, ReasonCode.Ok);
        }

        var transactionId = correlation.TransactionId != Guid.Empty ? correlation.TransactionId : Guid.NewGuid();
        var grouped = correlation with { TransactionId = transactionId };
        var size = (ushort)staged.Count;

        for (var i = 0; i < staged.Count; i++)
        {
            var change = staged[i] with
            {
                TransactionId = transactionId,
                TransactionSize = size,
                TransactionIndex = (ushort)i,
            };

            Interlocked.Increment(ref _accepted);
            PublishChange(in change, stagedTemperaments[i], in grouped);
        }

        Metrics.Mutations.Add(staged.Count, StatusTag(MutationStatus.Accepted));

        var applied = transitions.ToImmutable();
        OccurrenceTape.Record(
            OccurrenceKind.BatchApplied, in actor, in grouped, ReasonCode.Ok, null, applied);

        return new BatchResult(MutationStatus.Accepted, applied, default, ReasonCode.Ok);
    }

    /// <summary>
    /// Sets availability without touching the value, and publishes when it changed.
    /// </summary>
    /// <remarks>
    /// <b>It still publishes.</b> A source going Live to Unavailable without changing its last value must
    /// publish, or a disconnected Spotify keeps rendering its last track until the track changes — which
    /// it never will, because the source is disconnected. That is precisely the chatbox-lies failure this
    /// design exists to remove, so the dedupe comparison is over the triple
    /// <c>(Value, Availability, Reason)</c> rather than over the value alone. The extra traffic is bounded
    /// by connection events, which are rare.
    /// <para>
    /// The cell is minted if it does not exist. "Spotify is not connected" has to be renderable before
    /// Spotify has ever played anything, and a cell whose availability is not Live carries no claim about
    /// its value.
    /// </para>
    /// </remarks>
    public void SetAvailability(
        SignalKey key, Availability availability, ReasonCode reason, in KernelActor actor, string? detail = null)
    {
        SignalChanged change;
        bool changed;

        using (_stripes[key.Hash & _stripeMask].EnterScope())
        {
            var value = _cells.TryGetValue(key, out var cell) ? cell.Value : default;
            changed = TryApplyLocked(
                key, value, in actor, detail, availability, reason, out change, out _, out _);
        }

        if (!changed)
        {
            return;
        }

        // An undescribed key falls back to Discrete, which is the default of the out parameter and the
        // fail-safe direction: an availability edge is exactly the kind of thing nothing may drop.
        Descriptors.TryGet(key, out var descriptor);
        PublishChange(in change, descriptor.Temperament, "kernel.availability");
    }

    /// <summary>
    /// A source reporting that it cannot produce values right now.
    /// </summary>
    /// <remarks>
    /// Every affected cell becomes <see cref="Availability.Unavailable"/> with the given reason and
    /// <b>keeps its last good value</b>, and one <c>WriteFailed</c> occurrence carries the detail. The
    /// failure then propagates in three directions on its own: composition suppresses every dependent
    /// segment, the Sources screen shows the reason, and the audit tape gets one row. <b>No
    /// <c>"ERROR:…"</c> string is ever written into a value.</b>
    /// </remarks>
    /// <param name="key">The single key that failed, or null for the whole source.</param>
    /// <param name="reason">Why.</param>
    /// <param name="detail">The diagnostic tail — an exception message. Never parsed.</param>
    /// <param name="actor">The failing source.</param>
    /// <param name="owned">
    /// The source's own grant. When <paramref name="key"/> is null this decides which cells are affected,
    /// which is exactly why sources hold a real prefix grant rather than <see cref="GrantSet.Unrestricted"/>.
    /// </param>
    public void ReportFailure(
        SignalKey? key, ReasonCode reason, string? detail, in KernelActor actor, GrantSet owned)
    {
        ArgumentNullException.ThrowIfNull(owned);

        var affected = ImmutableArray.CreateBuilder<SignalTransition>();

        if (key is { } single)
        {
            SetAvailability(single, Availability.Unavailable, reason, in actor, detail);
            if (_cells.TryGetValue(single, out var cell))
            {
                affected.Add(new SignalTransition(single, cell.Value, cell.Value, cell.Version));
            }
        }
        else
        {
            foreach (var (cellKey, _) in _cells)
            {
                if (!owned.CanWrite(cellKey))
                {
                    continue;
                }

                SetAvailability(cellKey, Availability.Unavailable, reason, in actor, detail);
                if (_cells.TryGetValue(cellKey, out var cell))
                {
                    affected.Add(new SignalTransition(cellKey, cell.Value, cell.Value, cell.Version));
                }
            }
        }

        OccurrenceTape.Record(
            OccurrenceKind.WriteFailed,
            in actor,
            Correlation.For("source.failure"),
            reason,
            detail,
            affected.ToImmutable());
    }

    /// <summary>
    /// Flips cells whose declared freshness window has elapsed from Live to Stale.
    /// </summary>
    /// <remarks>
    /// <b>D8 — a flat array scan, deliberately.</b> The superseded design kept a min-ordered deadline
    /// list. That is a pessimization, not an optimization: <i>every accepted observation resets a
    /// deadline</i>, so an ordered structure needs an O(log n) mutation under a non-stripe lock at
    /// ~2,700 writes/sec — a new global contention point on the busiest path in the application, bought
    /// to speed up a call that is not on that path at all. A flat scan over the 20–200 keys that actually
    /// declare <c>StaleAfterMs</c> costs roughly 200 ns against a 20 µs budget. <b>Do not "optimize" this
    /// into a priority queue.</b>
    /// <para>
    /// Called from the host's 33 ms tick with a monotonic timestamp. The kernel owns no timer.
    /// </para>
    /// </remarks>
    /// <returns>How many cells flipped.</returns>
    public int SweepStaleness(long nowTicks)
    {
        EnsureStaleTable();

        var tracked = _staleTracked;
        var deadlines = _staleTicks;
        List<SignalChanged>? staged = null;

        for (var i = 0; i < tracked.Length; i++)
        {
            var key = tracked[i];

            using (_stripes[key.Hash & _stripeMask].EnterScope())
            {
                if (!_cells.TryGetValue(key, out var cell)
                    || cell.Availability != Availability.Live
                    || nowTicks - cell.Timestamp <= deadlines[i])
                {
                    continue;
                }

                if (TryApplyLocked(
                        key, cell.Value, KernelActor.Kernel, "kernel.staleness",
                        Availability.Stale, ReasonCode.Stale, out var change, out _, out _))
                {
                    (staged ??= []).Add(change);
                }
            }
        }

        if (staged is null)
        {
            return 0;
        }

        Interlocked.Add(ref _stalenessFlips, staged.Count);
        Metrics.StalenessFlips.Add(staged.Count);

        foreach (var change in staged)
        {
            Descriptors.TryGet(change.Key, out var descriptor);
            PublishChange(in change, descriptor.Temperament, "kernel.staleness");
        }

        return staged.Count;
    }

    /// <summary>
    /// Evicts every cell matching a <see cref="KeyPattern"/> and records one <c>SignalRemoved</c>.
    /// </summary>
    /// <remarks>
    /// Only the store can evict a cell, which is the entire reason removal lives in the kernel rather
    /// than above it. Without it, <c>avatar.*</c> cells from the previous avatar stay readable and
    /// renderable after a swap, and <c>module.*</c> cells outlive the module that produced them.
    /// <para>
    /// The source registry composes this with a source's owned prefix; the store deliberately knows
    /// nothing about source ids, only about keys.
    /// </para>
    /// <para>
    /// One occurrence carries every removed key rather than one occurrence per key: an avatar swap
    /// evicts hundreds of parameters and that is one event in the user's understanding, not hundreds.
    /// Removal has no state-tape shape — a consumer keeping its own map learns of eviction here.
    /// </para>
    /// </remarks>
    /// <returns>How many cells were evicted.</returns>
    public int RemoveMatching(string pattern, in KernelActor actor, in Correlation correlation)
    {
        ArgumentException.ThrowIfNullOrEmpty(pattern);

        var removed = ImmutableArray.CreateBuilder<SignalTransition>();

        foreach (var (key, _) in _cells)
        {
            if (!KeyPattern.Matches(pattern, key))
            {
                continue;
            }

            using (_stripes[key.Hash & _stripeMask].EnterScope())
            {
                if (!_cells.TryRemove(key, out var cell))
                {
                    continue;
                }

                Interlocked.Decrement(ref _cellsByNamespace[(int)key.Namespace]);
                removed.Add(new SignalTransition(key, cell.Value, default, cell.Version));
            }
        }

        if (removed.Count == 0)
        {
            return 0;
        }

        Interlocked.Add(ref _cellsRemoved, removed.Count);
        OccurrenceTape.Record(
            OccurrenceKind.SignalRemoved, in actor, in correlation, ReasonCode.SourceDisabled, pattern,
            removed.ToImmutable());

        return removed.Count;
    }

    /// <summary>
    /// The barrier a test uses instead of sleeping.
    /// </summary>
    /// <remarks>
    /// Publication is synchronous on the writer's thread today, so this costs nothing and no kernel test
    /// needs it to pass. It exists because the contract — <b>no kernel test ever polls or sleeps</b> —
    /// has to survive a future in which publication is deferred, and a test suite written against a
    /// barrier that exists keeps working, while one written against an implementation detail does not.
    /// </remarks>
    public void FlushTapes()
    {
        // Intentionally empty. See the remarks: this is a contract, not an omission.
    }

    /// <inheritdoc />
    public void Dispose() => Metrics.Dispose();

    internal IEnumerable<Measurement<long>> ObserveCellsByNamespace()
    {
        foreach (var ns in Namespaces)
        {
            yield return new Measurement<long>(
                Volatile.Read(ref _cellsByNamespace[(int)ns]),
                new KeyValuePair<string, object?>("namespace", ns.ToString()));
        }
    }

    internal IEnumerable<Measurement<long>> ObserveCellsByAvailability()
    {
        var counts = new long[4];
        foreach (var (_, cell) in _cells)
        {
            counts[(int)cell.Availability]++;
        }

        for (var i = 0; i < counts.Length; i++)
        {
            yield return new Measurement<long>(
                counts[i], new KeyValuePair<string, object?>("availability", ((Availability)i).ToString()));
        }
    }

    /// <summary>Sorted in, deduplicated in place, new length out.</summary>
    private static int Deduplicate(Span<int> sorted)
    {
        if (sorted.Length <= 1)
        {
            return sorted.Length;
        }

        var write = 1;
        for (var read = 1; read < sorted.Length; read++)
        {
            if (sorted[read] != sorted[write - 1])
            {
                sorted[write++] = sorted[read];
            }
        }

        return write;
    }

    /// <summary>
    /// Metric tags are cached, not formatted.
    /// </summary>
    /// <remarks>
    /// <c>status.ToString()</c> allocates a string on every call, and this one is on the 2,700/sec path.
    /// The boxed <c>object?</c> in the tag would allocate a second time. Both are exactly the kind of
    /// quiet hot-path allocation the design's budget exists to keep out, and both cost nothing to avoid.
    /// </remarks>
    private static KeyValuePair<string, object?> StatusTag(MutationStatus status) => status switch
    {
        MutationStatus.Accepted => AcceptedTag,
        MutationStatus.NoChange => NoChangeTag,
        _ => RejectedTag,
    };

    private static KeyValuePair<string, object?> ReasonTag(ReasonCode reason) =>
        ReasonTags.GetOrAdd(reason, static r => new KeyValuePair<string, object?>("reason", r.ToString()));

    /// <summary>
    /// The kernel's one authorization call site, plus the two value-shaped checks that need the value
    /// and therefore cannot live in the pure policy function.
    /// </summary>
    private WriteDecision Authorize(
        in Mutation mutation,
        in KernelActor actor,
        GrantSet grants,
        out SignalDescriptor descriptor,
        out SignalValue value)
    {
        value = mutation.Value;
        var found = Descriptors.TryGet(mutation.Key, out descriptor);

        if (mutation.Origin != MutationOrigin.Observation)
        {
            SignalDescriptor? maybe = found ? descriptor : null;
            var decision = WritePolicy.Evaluate(mutation.Key, in actor, in maybe, grants);
            if (!decision.Allowed)
            {
                return decision;
            }
        }
        else if (!found)
        {
            return WriteDecision.Deny(ReasonCode.UnknownKey, $"'{mutation.Key}' has no descriptor.");
        }

        // D4, on the Mutate path as well as the Observe path: a NaN that gets in never dedupes again.
        if (!value.IsFinite())
        {
            return WriteDecision.Deny(
                ReasonCode.NonFiniteValue, $"'{mutation.Key}' received a non-finite reading.");
        }

        if (value.Kind == descriptor.Kind)
        {
            return WriteDecision.Ok;
        }

        if (!value.TryConvertTo(descriptor.Kind, out var coerced))
        {
            return WriteDecision.Deny(
                ReasonCode.KindMismatch,
                $"'{mutation.Key}' is {descriptor.Kind}; a {value.Kind} value does not convert to it.");
        }

        value = coerced;
        return WriteDecision.Ok;
    }

    /// <summary>
    /// The one place a cell is written. <b>The caller holds the key's stripe lock.</b>
    /// </summary>
    /// <remarks>
    /// Version and sequence are both stamped here, inside that lock, which is what makes per-key
    /// <c>Seq</c> agree with per-key <c>Version</c>. The change is handed back for the caller to publish
    /// <i>after</i> releasing the stripe.
    /// </remarks>
    /// <returns>True when the cell changed. False with <paramref name="failure"/> at <c>Ok</c> means the
    /// write deduped; false with a reason means it was refused.</returns>
    private bool TryApplyLocked(
        SignalKey key,
        SignalValue value,
        in KernelActor actor,
        string? cause,
        Availability availability,
        ReasonCode reason,
        out SignalChanged change,
        out SignalTransition transition,
        out ReasonCode failure,
        bool onlyIfAbsent = false)
    {
        change = default;
        transition = default;
        failure = ReasonCode.Ok;

        var exists = _cells.TryGetValue(key, out var cell);

        // A seed never overwrites. See Seed's remarks: the enumerated value is a snapshot from before
        // the request, and the wire is the authority for anything it has already spoken about.
        if (onlyIfAbsent && exists)
        {
            return false;
        }

        if (!exists && !TryReserveNamespaceSlot(key.Namespace))
        {
            failure = ReasonCode.NamespaceCellCapReached;
            return false;
        }

        if (exists
            && cell.Availability == availability
            && cell.Reason == reason
            && SignalValue.NearlyEquals(cell.Value, value))
        {
            return false;
        }

        var before = exists ? cell.Value : default;
        var version = exists ? cell.Version + 1 : 1L;
        var seq = Sequence.Next();
        var timestamp = _time.GetTimestamp();

        _cells[key] = new Cell(key, value, version, timestamp, actor, cause, availability, reason);

        change = new SignalChanged(
            key, value, before, availability, version, seq, timestamp, actor,
            TransactionId: Guid.Empty, TransactionSize: 0, TransactionIndex: 0);
        transition = new SignalTransition(key, before, value, version);
        return true;
    }

    private bool TryReserveNamespaceSlot(SignalNamespace signalNamespace)
    {
        var cap = _caps.TryGetValue(signalNamespace, out var configured) ? configured : int.MaxValue;

        while (true)
        {
            var current = Volatile.Read(ref _cellsByNamespace[(int)signalNamespace]);
            if (current >= cap)
            {
                return false;
            }

            if (Interlocked.CompareExchange(
                    ref _cellsByNamespace[(int)signalNamespace], current + 1, current) == current)
            {
                return true;
            }
        }
    }

    /// <summary>
    /// The hot-path publish. The correlation is built only in the discrete branch, so a continuous
    /// reading never pays for a <see cref="Guid"/> it has no tape to carry it to.
    /// </summary>
    private void PublishChange(in SignalChanged change, Temperament temperament, string cause)
    {
        StateTape.Publish(in change);

        if (temperament == Temperament.Discrete)
        {
            RecordDiscrete(in change, Correlation.For(cause));
        }
    }

    private void PublishChange(in SignalChanged change, Temperament temperament, in Correlation correlation)
    {
        StateTape.Publish(in change);

        if (temperament == Temperament.Discrete)
        {
            RecordDiscrete(in change, in correlation);
        }
    }

    /// <remarks>
    /// Only <see cref="Temperament.Discrete"/> changes get here. That is the whole game: the 2,700/sec
    /// firehose is structurally incapable of reaching the occurrence tape, so the audit ledger, the
    /// trigger engine and the SSE occurrence channel are all sized for human-rate traffic rather than
    /// machine-rate traffic.
    /// </remarks>
    private void RecordDiscrete(in SignalChanged change, in Correlation correlation) =>
        OccurrenceTape.Record(
            OccurrenceKind.DiscreteSignalChanged,
            change.Actor,
            in correlation,
            ReasonCode.Ok,
            null,
            [new SignalTransition(change.Key, change.Before, change.After, change.Version)]);

    private void RejectObservation(SignalKey key, ReasonCode reason, in KernelActor actor)
    {
        Interlocked.Increment(ref _observationsRejected);
        CountRejection(reason);
        Metrics.Rejections.Add(1, ReasonTag(reason));

        if (!TryEnterRejectionWindow(key))
        {
            return;
        }

        OccurrenceTape.Record(
            OccurrenceKind.WriteFailed,
            in actor,
            Correlation.For("transport.observe"),
            reason,
            $"'{key}' was refused on the observation path.",
            [new SignalTransition(key, default, default, 0L)]);
    }

    private void RecordRejection(
        SignalKey key, in KernelActor actor, in Correlation correlation, in WriteDecision decision)
    {
        Interlocked.Increment(ref _rejected);
        CountRejection(decision.Reason);
        Metrics.Mutations.Add(1, StatusTag(MutationStatus.Rejected));
        Metrics.Rejections.Add(1, ReasonTag(decision.Reason));

        if (!TryEnterRejectionWindow(key))
        {
            return;
        }

        OccurrenceTape.Record(
            OccurrenceKind.WriteRejected,
            in actor,
            in correlation,
            decision.Reason,
            decision.Message,
            [new SignalTransition(key, default, default, 0L)]);
    }

    private void RecordCapExhaustion(
        SignalKey key, in KernelActor actor, in Correlation correlation, ReasonCode reason) =>
        RecordRejection(key, in actor, in correlation, WriteDecision.Deny(
            reason, $"'{key.Namespace}' has reached its cell cap; '{key}' was not created."));

    private void CountRejection(ReasonCode reason)
    {
        switch (reason)
        {
            case ReasonCode.NonFiniteValue:
                Interlocked.Increment(ref _nonFiniteRejected);
                Metrics.NonFiniteRejected.Add(1);
                break;
            case ReasonCode.TextOnObservePath:
                Interlocked.Increment(ref _textOnObservePathRejected);
                break;
            case ReasonCode.NamespaceCellCapReached:
                Interlocked.Increment(ref _namespaceCapRejected);
                break;
            case ReasonCode.KindMismatch:
                Interlocked.Increment(ref _kindMismatchRejected);
                break;
            case ReasonCode.UnknownKey:
                Interlocked.Increment(ref _unknownKeyRejected);
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// One rejection narration per key per window. The counter always moves; only the tape is spared.
    /// </summary>
    private bool TryEnterRejectionWindow(SignalKey key)
    {
        var now = _time.GetTimestamp();

        while (true)
        {
            if (_lastRejectionReport.TryGetValue(key, out var last))
            {
                if (now - last < _rejectionWindowTicks)
                {
                    return false;
                }

                if (_lastRejectionReport.TryUpdate(key, now, last))
                {
                    return true;
                }

                continue;
            }

            if (_lastRejectionReport.Count >= RejectionThrottleCapacity)
            {
                // A key generator producing unbounded distinct rejections would otherwise grow this
                // table without bound, which is D10's failure wearing a different hat. The counters
                // still move, so the condition remains visible.
                return false;
            }

            if (_lastRejectionReport.TryAdd(key, now))
            {
                return true;
            }
        }
    }

    private void EnsureStaleTable()
    {
        var version = Descriptors.Version;
        if (Volatile.Read(ref _staleRegistryVersion) == version)
        {
            return;
        }

        lock (_staleGate)
        {
            if (_staleRegistryVersion == version)
            {
                return;
            }

            var keys = new List<SignalKey>();
            var deadlines = new List<long>();

            foreach (var (key, descriptor) in Descriptors.Effective)
            {
                if (descriptor.StaleAfterMs is not { } ms || ms <= 0)
                {
                    continue;
                }

                keys.Add(key);
                deadlines.Add((long)(ms / 1000.0 * _time.TimestampFrequency));
            }

            _staleTracked = [.. keys];
            _staleTicks = [.. deadlines];
            Volatile.Write(ref _staleRegistryVersion, version);
        }
    }

    private readonly record struct PreparedMutation(SignalKey Key, SignalValue Value, Temperament Temperament);
}
