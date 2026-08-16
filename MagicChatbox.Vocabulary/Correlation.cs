namespace MagicChatbox.Vocabulary;

/// <summary>
/// Who caused this, and as part of what.
/// </summary>
/// <param name="OperationId">
/// Identifies one logical operation end to end. A chatbox send and the occurrences it emits share one.
/// </param>
/// <param name="TransactionId">
/// Groups writes that must be applied all-or-nothing. <see cref="Guid.Empty"/> means ungrouped.
/// </param>
/// <param name="Cause">
/// A short, stable, dotted verb naming what initiated this — <c>chatbox.send</c>,
/// <c>assistant.tool.set_parameter</c>, <c>trigger.rule.14</c>. Not free text and not a log message:
/// the Audit screen groups on it and a rule can filter self-caused edges by it.
/// </param>
/// <remarks>
/// This exists because an LLM writes to this system. VRChat's other OSC apps are driven by a human who
/// knows what they just did; MagicChatbox's assistant is not, and "what changed, who changed it, and
/// why" has to survive into a ledger the user can read and revert.
/// </remarks>
public readonly record struct Correlation(Guid OperationId, Guid TransactionId, string Cause)
{
    /// <summary>True when this write belongs to an all-or-nothing group.</summary>
    public bool InTransaction => TransactionId != Guid.Empty;

    /// <summary>An ungrouped operation with a fresh id.</summary>
    public static Correlation For(string cause) => new(Guid.NewGuid(), Guid.Empty, cause);
}
