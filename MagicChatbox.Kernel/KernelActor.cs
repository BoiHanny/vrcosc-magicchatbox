namespace MagicChatbox.Kernel;

/// <summary>
/// What kind of thing performed a write.
/// </summary>
/// <remarks>
/// The kind, not the identity, is what policy reads: <c>WriteSafety.Privileged</c> keys are closed to
/// <see cref="Assistant"/>, <see cref="Rule"/> and <see cref="Module"/> and open to <see cref="User"/>,
/// <see cref="System"/> and <see cref="Restore"/>. The identity is for the Audit screen.
/// <para>
/// The kind decides safety class only. <i>Which</i> keys an actor reaches is the grant it was handed —
/// <c>GrantSet.ForModule</c> and <c>GrantSet.ForRule</c> are where the difference between a module and a
/// rule is actually written down, and it is the opposite ordering from this list: a rule reaches further
/// than a module and is trusted less than the person who wrote it.
/// </para>
/// </remarks>
public enum ActorKind
{
    /// <summary>The OSC bridge and anything else that reports observed external truth.</summary>
    Transport,

    /// <summary>A person, through the UI.</summary>
    User,

    /// <summary>The LLM. The reason <c>Correlation</c> exists at all.</summary>
    Assistant,

    /// <summary>An automation rule firing. <see cref="KernelActor.Id"/> is the rule's own id.</summary>
    /// <remarks>
    /// The rule id being the actor id is what makes self-write suppression exact rather than a timing
    /// heuristic: a rule can tell its own writes from everyone else's by identity, so it never
    /// re-triggers off the change it just made and there is no echo window to tune.
    /// </remarks>
    Rule,

    /// <summary>An installed module or integration.</summary>
    Module,

    /// <summary>MagicChatbox itself — the staleness sweep, startup, shutdown.</summary>
    System,

    /// <summary>Persistence replaying saved state at startup.</summary>
    Restore,
}

/// <summary>
/// Who performed a write.
/// </summary>
/// <param name="Kind">What sort of actor this is. Policy reads this.</param>
/// <param name="Id">
/// A stable identity — a module's source id, a rule id, the signed-in user. <b>Host-assigned, never
/// caller-claimed.</b> v2 established that at <c>IMagicStateService.cs:60-66</c> and it is kept: an
/// actor a caller can name is an actor a caller can impersonate, and the whole point of the ledger is
/// that "who did this" survives.
/// </param>
public readonly record struct KernelActor(ActorKind Kind, string Id)
{
    /// <summary>The kernel acting on its own behalf — staleness sweeps and eviction.</summary>
    public static readonly KernelActor Kernel = new(ActorKind.System, "kernel");
}
