using MagicChatbox.Vocabulary;

namespace MagicChatbox.Kernel;

/// <summary>
/// One accepted change on the state tape: the "current value changed" fact.
/// </summary>
/// <param name="Key">The key that changed.</param>
/// <param name="After">The value now stored, after coercion.</param>
/// <param name="Before">The value it replaced. Captured at write time, never reconstructed.</param>
/// <param name="Availability">The cell's availability after the write. Availability-only changes publish too.</param>
/// <param name="Version">The cell's new per-key version. Every state sink compares this before overwriting.</param>
/// <param name="Seq">
/// The global sequence number, stamped inside the same stripe lock as <paramref name="Version"/>. A
/// total order over publishes; <b>not</b> a real-time order across keys.
/// </param>
/// <param name="Timestamp">Monotonic ticks at write time.</param>
/// <param name="Actor">Who wrote it.</param>
/// <param name="TransactionId">The batch this belongs to, or <see cref="Guid.Empty"/> when ungrouped.</param>
/// <param name="TransactionSize">
/// D1: the total number of members the batch actually published, stamped by the writer. Closure of a
/// transaction depends on this count and not on any single member's slot surviving.
/// </param>
/// <param name="TransactionIndex">D1: this member's 0-based position within the batch.</param>
/// <remarks>
/// Coalescing this tape is lossless <i>by definition</i>: a state fact's latest value is the whole
/// truth about it. Facts for which that is untrue — a momentary pulse, a transcript final — are
/// <see cref="Temperament.Discrete"/> and additionally ride the occurrence tape, which never coalesces.
/// </remarks>
public readonly record struct SignalChanged(
    SignalKey Key,
    SignalValue After,
    SignalValue Before,
    Availability Availability,
    long Version,
    long Seq,
    long Timestamp,
    KernelActor Actor,
    Guid TransactionId,
    ushort TransactionSize,
    ushort TransactionIndex)
{
    /// <summary>
    /// Derived, never stored. D1: a member must not be able to become un-final by being overwritten,
    /// which is precisely what killed the superseded design's coalescing drain.
    /// </summary>
    public bool IsBatchEnd => TransactionSize != 0 && TransactionIndex == TransactionSize - 1;

    /// <summary>True when this change belongs to an all-or-nothing group.</summary>
    public bool InTransaction => TransactionId != Guid.Empty;
}

/// <summary>
/// Receives state changes. Implemented only by kernel-owned mailboxes.
/// </summary>
/// <remarks>
/// <b>A sink may never block the writer.</b> Every shipped implementation is a mailbox whose
/// <c>OnSignalChanged</c> is a dictionary upsert or a <c>TryWrite</c> — never <c>WriteAsync</c>, never
/// an <c>await</c>, never a lock held across dispatch. That is what makes "a slow browser tab cannot
/// back-pressure the OSC receive loop" a structural property rather than a convention.
/// </remarks>
public interface IStateSink
{
    /// <summary>Called on the writer's thread, outside every stripe lock. Must not block.</summary>
    void OnSignalChanged(in SignalChanged e);
}

/// <summary>The state tape: every accepted change, filtered by grant at subscribe time.</summary>
public interface IStateTape
{
    /// <summary>
    /// Subscribes a sink to the changes <paramref name="grants"/> permits reading.
    /// </summary>
    /// <remarks>
    /// Grant filtering happens here, in the kernel. There is no unfiltered stream a caller can
    /// subscribe to and filter for itself.
    /// </remarks>
    IDisposable Subscribe(IStateSink sink, GrantSet grants);
}
