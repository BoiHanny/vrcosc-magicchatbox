using System.Collections.Immutable;
using MagicChatbox.Vocabulary;

namespace MagicChatbox.Scope;

/// <summary>
/// What a guard is allowed to ask about.
/// </summary>
/// <remarks>
/// A closed set plus one open namespace, and the closure is the point. Every key here has exactly one
/// writer and a documented meaning, so "why did my rule not fire" is answerable by naming a key. The
/// open <see cref="ParameterPrefix"/> space is the exception because the wearer's own avatar decides
/// what is in it, and no list written here could keep up.
/// </remarks>
public readonly record struct ScopeFactKey(string Value)
{
    /// <summary>VRChat's own <c>avtr_…</c> id for what is being worn.</summary>
    public static readonly ScopeFactKey AvatarId = new("avatar.id");

    /// <summary>The avatar's display name, which is not unique and must never be an identity key.</summary>
    public static readonly ScopeFactKey AvatarName = new("avatar.name");

    /// <summary>Membership test only. Answered from a resolved set, never by string comparison.</summary>
    public static readonly ScopeFactKey AvatarGroup = new("avatar.group");

    public static readonly ScopeFactKey WorldId = new("world.id");

    public static readonly ScopeFactKey WorldName = new("world.name");

    public static readonly ScopeFactKey WorldGroup = new("world.group");

    /// <summary>Public, Friends+, Friends, Invite+, Invite, or Group.</summary>
    public static readonly ScopeFactKey InstanceType = new("instance.type");

    public static readonly ScopeFactKey InstanceRegion = new("instance.region");

    /// <summary>Quiet, Busy or Packed — bucketed, because a raw headcount makes every rule flap.</summary>
    public static readonly ScopeFactKey InstanceCrowd = new("instance.crowd");

    /// <summary>VR or Desktop.</summary>
    public static readonly ScopeFactKey AppMode = new("app.mode");

    /// <summary>The open namespace: one key per parameter the worn avatar declares.</summary>
    public const string ParameterPrefix = "avatar.param.";

    /// <summary>The curated keys, in the order a picker should offer them.</summary>
    public static readonly ImmutableArray<ScopeFactKey> Curated =
    [
        AvatarId, AvatarName, AvatarGroup,
        WorldId, WorldName, WorldGroup,
        InstanceType, InstanceRegion, InstanceCrowd,
        AppMode,
    ];

    public static ScopeFactKey Parameter(string name) => new(ParameterPrefix + name);

    public bool IsParameter => Value.StartsWith(ParameterPrefix, StringComparison.Ordinal);

    public bool IsGroupMembership => this == AvatarGroup || this == WorldGroup;

    public override string ToString() => Value;
}

/// <summary>
/// One fact's reading, and whether anybody can currently see it.
/// </summary>
/// <remarks>
/// <b>Liveness is what makes <see cref="ScopeOutcome.Unknown"/> reachable, and it is the whole reason
/// this is not just a value.</b> The schema and sense stores are cleared on every avatar change, so for
/// the seconds between a swap and the next harvest an avatar fact is genuinely unreadable rather than
/// false. A guard that cannot tell those apart tears down whatever it gates several times an evening.
/// </remarks>
public readonly record struct ScopeCell(SignalValue Value, bool IsLive)
{
    public static ScopeCell Live(SignalValue value) => new(value, true);

    public static ScopeCell Dark(SignalValue value) => new(value, false);

    public static readonly ScopeCell Absent = new(default, false);
}

/// <summary>
/// Everything true at one instant, as an immutable snapshot.
/// </summary>
/// <remarks>
/// Snapshot rather than a live reader so that evaluating a rulebook cannot observe a fact changing
/// halfway through, which would let one rule see a swap that the rule above it did not.
/// <para>
/// Group membership is resolved into sets <b>before</b> the evaluator runs. That keeps
/// <see cref="ScopeEvaluator"/> a pure function of its two arguments with no injected resolver — the
/// service-locator shape a previous generation needed here only to break a dependency cycle.
/// </para>
/// </remarks>
public sealed record ScopeFacts(
    ImmutableDictionary<string, ScopeCell> Cells,
    ImmutableHashSet<string> AvatarGroupIds,
    ImmutableHashSet<string> WorldGroupIds)
{
    public static readonly ScopeFacts Empty = new(
        ImmutableDictionary<string, ScopeCell>.Empty,
        ImmutableHashSet<string>.Empty,
        ImmutableHashSet<string>.Empty);

    public ScopeCell Read(ScopeFactKey key) =>
        Cells.TryGetValue(key.Value, out ScopeCell cell) ? cell : ScopeCell.Absent;
}
