using System.Collections.Immutable;
using MagicChatbox.Vocabulary;

namespace MagicChatbox.Kernel;

/// <summary>Where a write came from, which decides whether policy runs at all.</summary>
public enum MutationOrigin
{
    /// <summary>
    /// Reported external truth. Short-circuits the policy call entirely — v2's trusted-source
    /// exemption, kept deliberately, because it takes the rule engine off the 2,700/sec path.
    /// </summary>
    Observation,

    /// <summary>Somebody asked for this. Policy runs.</summary>
    Request,

    /// <summary>Persistence replaying saved state at startup.</summary>
    Restore,
}

/// <summary>What a write did.</summary>
public enum MutationStatus
{
    /// <summary>The cell changed and a <see cref="SignalChanged"/> was published.</summary>
    Accepted,

    /// <summary>The write was legal and the value was indistinguishable from the current one. Nothing published.</summary>
    NoChange,

    /// <summary>Policy or a boundary check said no. Nothing was written.</summary>
    Rejected,
}

/// <summary>One requested change.</summary>
/// <param name="Key">The key to write.</param>
/// <param name="Value">The value to write, before coercion to the descriptor's kind.</param>
/// <param name="Origin">Where it came from.</param>
public readonly record struct Mutation(SignalKey Key, SignalValue Value, MutationOrigin Origin)
{
    /// <summary>A requested write — the ordinary case for anything that is not the transport.</summary>
    public static Mutation Request(SignalKey key, SignalValue value) =>
        new(key, value, MutationOrigin.Request);
}

/// <summary>
/// One reported reading. The hot path's shape.
/// </summary>
/// <remarks>
/// No <see cref="MutationOrigin"/> field: <c>Observe</c> implies <see cref="MutationOrigin.Observation"/>,
/// and a field nobody can set differently is a field that costs eight bytes per message at 2,700/sec
/// to encode a constant.
/// </remarks>
public readonly record struct Observation(SignalKey Key, SignalValue Value);

/// <summary>The outcome of a single write.</summary>
/// <param name="Status">Accepted, NoChange or Rejected.</param>
/// <param name="Key">The key written.</param>
/// <param name="Accepted">The value actually stored, after coercion. Default when rejected.</param>
/// <param name="Version">The new version on acceptance; the unchanged version otherwise.</param>
/// <param name="Reason">Ok, or why it was rejected.</param>
public readonly record struct MutationResult(
    MutationStatus Status,
    SignalKey Key,
    SignalValue Accepted,
    long Version,
    ReasonCode Reason)
{
    /// <summary>True only when the cell changed.</summary>
    public bool Applied => Status == MutationStatus.Accepted;

    internal static MutationResult Rejected(SignalKey key, ReasonCode reason) =>
        new(MutationStatus.Rejected, key, default, 0L, reason);
}

/// <summary>
/// Before and after for one key, captured at write time.
/// </summary>
/// <remarks>
/// Never reconstructed. A ledger row that says "hue went from 0.2 to 0.8" must be reading what the
/// writer saw, not re-deriving it from a later state that has moved on.
/// </remarks>
public readonly record struct SignalTransition(
    SignalKey Key,
    SignalValue Before,
    SignalValue After,
    long Version);

/// <summary>The outcome of an all-or-nothing batch.</summary>
/// <param name="Status">
/// <c>Accepted</c> when at least one member changed, <c>NoChange</c> when every member deduped, and
/// <c>Rejected</c> when any member failed authorization — in which case nothing was written.
/// </param>
/// <param name="Applied">Every transition that occurred, in the order they were published.</param>
/// <param name="OffendingKey">On rejection, the member that failed. Default otherwise.</param>
/// <param name="Reason">
/// On rejection, the offending member's own reason. The design named a separate
/// <c>BatchMemberRejected</c> code for this; reporting the member's actual reason beside its key says
/// strictly more and needs no extra vocabulary.
/// </param>
public readonly record struct BatchResult(
    MutationStatus Status,
    ImmutableArray<SignalTransition> Applied,
    SignalKey OffendingKey,
    ReasonCode Reason)
{
    /// <summary>True only when the batch was applied and something changed.</summary>
    public bool IsAccepted => Status == MutationStatus.Accepted;

    internal static BatchResult Rejected(SignalKey key, ReasonCode reason) =>
        new(MutationStatus.Rejected, ImmutableArray<SignalTransition>.Empty, key, reason);
}
