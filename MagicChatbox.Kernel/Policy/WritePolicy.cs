using MagicChatbox.Vocabulary;

namespace MagicChatbox.Kernel;

/// <summary>Whether a write is allowed, and if not, why.</summary>
/// <param name="Allowed">True when the write may proceed.</param>
/// <param name="Reason">The machine-readable half. <c>Ok</c> when allowed.</param>
/// <param name="Message">The human-readable tail, for the Audit screen. Never parsed.</param>
public readonly record struct WriteDecision(bool Allowed, ReasonCode Reason, string? Message)
{
    /// <summary>Allowed.</summary>
    public static readonly WriteDecision Ok = new(true, ReasonCode.Ok, null);

    /// <summary>Denied, with a reason.</summary>
    public static WriteDecision Deny(ReasonCode reason, string? message = null) =>
        new(false, reason, message);
}

/// <summary>
/// The kernel's only authorization function.
/// </summary>
/// <remarks>
/// <para>
/// <b>One function, one implementation, one call site.</b> v2 had four copies of this rule and they
/// disagreed. The single call site is <c>SignalStore.Authorize</c>; a second one is a bug even if it
/// looks like a convenience.
/// </para>
/// <para>
/// <b>Pure.</b> It reads the descriptor and the grant set and <i>never reads a cell</i>. That purity is
/// not stylistic — it is what lets a batch authorize all 37 of its members before any stripe lock is
/// taken, which is what removes the lock-cycle question without a global lock.
/// </para>
/// <para>
/// <b><see cref="MutationOrigin.Observation"/> short-circuits this call entirely</b> at the caller. The
/// observe path is the transport reporting what VRChat said; running a rule engine on it at 2,700/sec
/// buys nothing, because there is no decision to make about a fact that already happened.
/// </para>
/// <para>
/// Kind coercion is not evaluated here, because it needs the value and this function deliberately does
/// not take one — a decision that depends only on the key stays cacheable and stays pure. The store
/// applies the descriptor's kind immediately after this call, and reports <c>KindMismatch</c> when the
/// conversion matrix refuses.
/// </para>
/// <para>
/// <b>This function decides safety class, not reach.</b> It answers "may an actor of this kind write a
/// key of this safety", and <see cref="GrantSet"/> answers "which keys does this holder reach at all".
/// Both halves are load-bearing and neither is sufficient alone: <see cref="GrantSet.ForRule"/> is where
/// a rule's reach is spelled out, and it says so in the other direction too, because a reader who finds
/// only one of the two will conclude the wrong thing about what an automation can touch.
/// </para>
/// </remarks>
public static class WritePolicy
{
    /// <summary>
    /// Evaluates one write. Rejection reasons, in evaluation order: revoked scope, ungranted key,
    /// missing descriptor, externally side-effecting key, observation-only key, privileged key written
    /// by an assistant, a rule or a module.
    /// </summary>
    /// <remarks>
    /// Two reason codes carry more than their name suggests, because the vocabulary is deliberately
    /// small:
    /// <list type="bullet">
    /// <item><description>
    /// <c>ObservedOnly</c> covers both <see cref="WriteSafety.ObservedOnly"/> and
    /// <see cref="SignalDescriptor.HasExternalSideEffect"/>. Its documented meaning — "it reflects
    /// external truth and may not be set directly; writing an avatar parameter goes through egress,
    /// not through the store" — is exactly the side-effect case as well.
    /// </description></item>
    /// <item><description>
    /// <c>NotGranted</c> covers a <see cref="WriteSafety.Privileged"/> key written by an assistant, a
    /// rule or a module. "Your grant does not extend here" is true of all three, and the
    /// <see cref="WriteDecision.Message"/> distinguishes them for the human reading the ledger.
    /// </description></item>
    /// </list>
    /// <para>
    /// <b>Why <see cref="ActorKind.Rule"/> sits with the assistant rather than with the user.</b> A rule
    /// is authored by a person, which is the whole of the argument for trusting it further than a
    /// module — and it is only an argument about <i>reach</i>, which is why it is settled in
    /// <see cref="GrantSet.ForRule"/> and not here. A rule fires unattended forever after it is written,
    /// so at the moment it writes there is nobody to ask, and <see cref="WriteSafety.Privileged"/> means
    /// precisely "this one needs a person". v2 settled the same question as audit finding F-107 — an
    /// unattended automation has no human to ask, so anything that would require approval is denied —
    /// and the alternative here is worse than v2's, because a rule holding a grant and no safety check
    /// would be as wide as the person's own gesture while nobody is watching it.
    /// </para>
    /// <para>
    /// <see cref="ActorKind.Restore"/> is deliberately absent from that list. A restore is the user's own
    /// saved state coming back at startup; closing it would mean every privileged cell silently failed to
    /// survive a restart, which is the persistence surface's entire reason for existing.
    /// </para>
    /// </remarks>
    public static WriteDecision Evaluate(
        SignalKey key,
        in KernelActor actor,
        in SignalDescriptor? descriptor,
        GrantSet grants)
    {
        ArgumentNullException.ThrowIfNull(grants);

        if (grants.IsRevoked)
        {
            return WriteDecision.Deny(ReasonCode.ScopeRevoked, $"The scope that granted '{key}' was revoked.");
        }

        if (!grants.CanWrite(key))
        {
            return WriteDecision.Deny(ReasonCode.NotGranted, $"No write grant covers '{key}'.");
        }

        if (descriptor is not { } d)
        {
            return WriteDecision.Deny(
                ReasonCode.UnknownKey,
                $"'{key}' has no descriptor. Declare it before writing it.");
        }

        if (d.HasExternalSideEffect)
        {
            return WriteDecision.Deny(
                ReasonCode.ObservedOnly,
                $"'{key}' is changed by asking the outside world, not by writing a cell.");
        }

        if (d.Safety == WriteSafety.ObservedOnly)
        {
            return WriteDecision.Deny(
                ReasonCode.ObservedOnly,
                $"'{key}' is observation-only; only the source that observes it may write it.");
        }

        if (d.Safety == WriteSafety.Privileged &&
            actor.Kind is ActorKind.Assistant or ActorKind.Rule or ActorKind.Module)
        {
            return WriteDecision.Deny(
                ReasonCode.NotGranted,
                $"'{key}' is privileged; {actor.Kind} may not write it.");
        }

        return WriteDecision.Ok;
    }
}
