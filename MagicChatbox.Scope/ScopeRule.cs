using System.Collections.Immutable;

namespace MagicChatbox.Scope;

/// <summary>A named set of avatars, referred to by id so a rename cannot break a guard.</summary>
public sealed record AvatarGroup(string Id, string Name, ImmutableArray<string> AvatarIds)
{
    public ImmutableArray<string> SafeAvatarIds => AvatarIds.IsDefault ? ImmutableArray<string>.Empty : AvatarIds;
}

/// <summary>A named set of worlds.</summary>
public sealed record WorldGroup(string Id, string Name, ImmutableArray<string> WorldIds)
{
    public ImmutableArray<string> SafeWorldIds => WorldIds.IsDefault ? ImmutableArray<string>.Empty : WorldIds;
}

/// <summary>
/// What a guard is attached to.
/// </summary>
/// <remarks>
/// <b>There is no trigger and no action list here, and that is the design rather than an omission.</b>
/// A guard says <i>when something may run</i>, which is a level; the previous generation modelled the
/// same idea as rules firing effects, and then needed conflict arbitration, cooldowns and a graph
/// visualiser to keep two of them from fighting. One guard per target cannot conflict with itself.
/// </remarks>
public enum ScopeTargetKind : byte
{
    /// <summary>An <c>IntegrationTileCatalog</c> key.</summary>
    Integration = 0,

    /// <summary>A saved avatar look, applied on the edge of the guard becoming true.</summary>
    AvatarPreset = 1,

    /// <summary>Everything the app sends to VRChat.</summary>
    Sending = 2,
}

public sealed record ScopeTarget(ScopeTargetKind Kind, string Key)
{
    public static ScopeTarget Integration(string tileKey) => new(ScopeTargetKind.Integration, tileKey);

    public static ScopeTarget Preset(string presetName) => new(ScopeTargetKind.AvatarPreset, presetName);

    public static readonly ScopeTarget Sending = new(ScopeTargetKind.Sending, string.Empty);
}

/// <summary>
/// One guard, the thing it guards, and how long it must settle before anybody acts on it.
/// </summary>
public sealed record ScopeRule(
    string Id,
    string Name,
    bool Enabled,
    ScopeTarget Target,
    ScopeGroup When,
    int DwellMs,
    string Note)
{
    public const int DefaultDwellMs = 2000;

    public const int MaxDwellMs = 120_000;

    /// <summary>
    /// Whether a guard nobody can read yet should hold the thing off rather than leave it alone.
    /// </summary>
    /// <remarks>
    /// <b>Off by default, and the default is the safe one for almost every rule.</b> A guard only ever
    /// narrows — the thing it guards is something the user already switched on — so "we cannot tell yet"
    /// is not a reason to override them. At startup, and whenever the world reader is not running, the
    /// facts are unreadable for a while; blocking by default there would make a guarded integration look
    /// broken rather than guarded.
    /// <para>
    /// It is worth turning on for a guard written for privacy rather than convenience, where the honest
    /// answer to "am I in the world I said to stay quiet in?" being unknown should mean staying quiet.
    /// </para>
    /// </remarks>
    public bool BlockWhileUnknown { get; init; }

    public static ScopeRule For(string id, string name, ScopeTarget target, ScopeGroup when) =>
        new(id, name, Enabled: false, target, when, DefaultDwellMs, string.Empty);

    public ScopeGroup SafeWhen => When ?? ScopeGroup.Always;

    public TimeSpan Dwell => TimeSpan.FromMilliseconds(Math.Clamp(DwellMs, 0, MaxDwellMs));

    /// <summary>
    /// Everything wrong with this rule, all at once, each tagged with the field that owns it.
    /// </summary>
    /// <remarks>
    /// All at once rather than first-wins, and slot-tagged rather than prose, so an editor can outline the
    /// card that is wrong instead of raising a message that names none of them.
    /// </remarks>
    public IReadOnlyList<ScopeProblem> Validate()
    {
        var problems = new List<ScopeProblem>();

        if (string.IsNullOrWhiteSpace(Id))
            problems.Add(new ScopeProblem("rule", ScopeProblemCode.MissingId, "This rule has no id."));

        if (string.IsNullOrWhiteSpace(Name))
            problems.Add(new ScopeProblem("name", ScopeProblemCode.MissingName, "Give this rule a name you will recognise."));

        if (Target is null)
        {
            problems.Add(new ScopeProblem("target", ScopeProblemCode.MissingTarget, "This rule does not say what it guards."));
        }
        else if (Target.Kind != ScopeTargetKind.Sending && string.IsNullOrWhiteSpace(Target.Key))
        {
            problems.Add(new ScopeProblem("target", ScopeProblemCode.MissingTarget, "This rule does not say what it guards."));
        }

        if (DwellMs < 0 || DwellMs > MaxDwellMs)
        {
            problems.Add(new ScopeProblem(
                "dwell",
                ScopeProblemCode.DwellOutOfRange,
                $"Settling time must be between 0 and {MaxDwellMs / 1000} seconds."));
        }

        ScopeGroup guard = SafeWhen;

        if (guard.Depth > ScopeGroup.MaxDepth)
        {
            problems.Add(new ScopeProblem(
                "when",
                ScopeProblemCode.DepthExceeded,
                $"Groups may only nest {ScopeGroup.MaxDepth} deep."));
        }

        ValidatePredicates(guard, "when", problems);

        return problems;
    }

    private static void ValidatePredicates(ScopeGroup group, string slot, List<ScopeProblem> problems)
    {
        ImmutableArray<ScopePredicate> predicates = group.SafePredicates;

        for (int i = 0; i < predicates.Length; i++)
        {
            ScopePredicate predicate = predicates[i];
            string here = $"{slot}.predicates[{i}]";

            if (string.IsNullOrWhiteSpace(predicate.Key.Value))
            {
                problems.Add(new ScopeProblem(here, ScopeProblemCode.MissingFact, "Pick something to test."));
                continue;
            }

            bool wantsGroup = predicate.Op == ScopeOperator.InGroup;
            if (wantsGroup != predicate.Key.IsGroupMembership)
            {
                problems.Add(new ScopeProblem(
                    here,
                    ScopeProblemCode.OperatorInvalidForKey,
                    predicate.Key.IsGroupMembership
                        ? "A group can only be tested for membership."
                        : "Only avatar groups and world groups can be tested for membership."));
            }
        }

        ImmutableArray<ScopeGroup> nested = group.SafeGroups;
        for (int i = 0; i < nested.Length; i++)
            ValidatePredicates(nested[i], $"{slot}.groups[{i}]", problems);
    }
}

public enum ScopeProblemCode
{
    MissingId = 0,
    MissingName = 1,
    MissingTarget = 2,
    DwellOutOfRange = 3,
    DepthExceeded = 4,
    MissingFact = 5,
    OperatorInvalidForKey = 6,
}

public sealed record ScopeProblem(string Slot, ScopeProblemCode Code, string Detail);
