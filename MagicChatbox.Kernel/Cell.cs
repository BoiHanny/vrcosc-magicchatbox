using System.Collections.Immutable;
using MagicChatbox.Vocabulary;

namespace MagicChatbox.Kernel;

/// <summary>
/// The unit of truth: one value, one version, and everything needed to know whether to believe it.
/// </summary>
/// <param name="Key">The fact this cell names.</param>
/// <param name="Value">The last value written. Retained even when <paramref name="Availability"/> says not to believe it.</param>
/// <param name="Version">Per-key and monotonic, stamped inside the stripe lock. Never global.</param>
/// <param name="Timestamp">
/// <c>TimeProvider.GetTimestamp()</c> — monotonic, never wall clock. Staleness measured against a wall
/// clock breaks across a DST boundary and across an NTP correction, both of which happen to real users.
/// </param>
/// <param name="LastActor">Who wrote it.</param>
/// <param name="LastCause">The <c>Correlation.Cause</c> of the write, kept for the Audit screen.</param>
/// <param name="Availability">Whether the value is current. See <see cref="Kernel.Availability"/>.</param>
/// <param name="Reason">Why it is not <see cref="Kernel.Availability.Live"/>, or <c>Ok</c> when it is.</param>
/// <remarks>
/// A <c>readonly record struct</c> rather than v2's sealed record class over <c>object? Value</c>,
/// which heap-allocated a cell and boxed the value twice on a 2,700/sec path.
/// </remarks>
public readonly record struct Cell(
    SignalKey Key,
    SignalValue Value,
    long Version,
    long Timestamp,
    KernelActor LastActor,
    string? LastCause,
    Availability Availability,
    ReasonCode Reason)
{
    /// <summary>True only when the value may be rendered as the current state of the world.</summary>
    public bool IsLive => Availability == Availability.Live;
}

/// <summary>
/// Every cell as of one moment, stamped with the sequence number that moment corresponds to.
/// </summary>
/// <remarks>
/// <b>A snapshot may observe a partially-applied batch (D9.)</b> Stripes are taken hand-over-hand, so
/// there is no cross-key isolation and none will be added — buying it costs a global reader-writer lock
/// on the OSC ingress path. The contract that consumers actually need is that the coalescing drain
/// never delivers a partial transaction, and that one holds.
/// <para>
/// A reconnecting client takes a snapshot and then follows the state stream, which corrects any torn
/// read within one drain.
/// </para>
/// </remarks>
public sealed class StateSnapshot
{
    internal StateSnapshot(long seq, ImmutableDictionary<SignalKey, Cell> cells)
    {
        Seq = seq;
        Cells = cells;
    }

    /// <summary>The global sequence number this snapshot was taken at.</summary>
    public long Seq { get; }

    /// <summary>Every cell the caller was granted read access to.</summary>
    public ImmutableDictionary<SignalKey, Cell> Cells { get; }

    /// <summary>Point lookup within the snapshot.</summary>
    public bool TryGet(SignalKey key, out Cell cell) => Cells.TryGetValue(key, out cell);

    /// <summary>
    /// This moment with some cells replaced: the state a rehearsal asks its question of.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A real snapshot rather than an override parameter threaded through the reader, because the
    /// alternative is two reading paths.</b> "What would this rule do if BPM were 112" has to be asked
    /// of the same evaluator the engine runs, and the only way to ask it is to hand that evaluator a
    /// state which says so. An <c>overrides</c> argument on the evaluation call would be live on the
    /// rehearsal path and dead on the tick's, which is precisely the drift the one-evaluator rule
    /// exists to stop — and it would put a feature of the authoring surface inside the function that
    /// runs 30 times a second.
    /// </para>
    /// <para>
    /// <b><see cref="Seq"/> is carried over unchanged, and the snapshot this returns is not a moment
    /// that ever happened.</b> Nothing may store one, publish one or compare one against a real
    /// sequence number; it exists to be read once and dropped. Keeping the number rather than zeroing
    /// it is the lesser lie — zero is a legitimate sequence, and a caller that logged it would report
    /// the beginning of the session rather than "made up".
    /// </para>
    /// <para>
    /// A key with no cell here is added rather than refused. Building a rule with VRChat closed is the
    /// case the whole affordance exists for, and in that state the interesting keys have no cells at
    /// all — a replacement that could only replace would answer <c>Unknown</c> for every one of them
    /// and rehearse nothing.
    /// </para>
    /// </remarks>
    public StateSnapshot With(IEnumerable<Cell> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);

        var builder = Cells.ToBuilder();

        foreach (var cell in cells)
        {
            builder[cell.Key] = cell;
        }

        return new StateSnapshot(Seq, builder.ToImmutable());
    }
}
