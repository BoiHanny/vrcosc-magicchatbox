using System.Collections.Immutable;
using MagicChatbox.Vocabulary;

namespace MagicChatbox.Scope;

/// <summary>
/// What a guard answered. Three values, and the third is not a convenience.
/// </summary>
/// <remarks>
/// <b>A guard that cannot say "I do not know" says "no", and "no" is an instruction.</b> Every avatar
/// fact is unreadable for the seconds between an avatar change and the next schema harvest; a
/// two-valued guard turns that window into a decision to switch things off, and then back on, every
/// time somebody changes clothes.
/// <para>
/// The numbering is deliberate: <c>Unknown = 0</c> so that <c>default</c> is the value that acts on
/// nothing.
/// </para>
/// </remarks>
public enum ScopeOutcome : byte
{
    Unknown = 0,
    False = 1,
    True = 2,
}

/// <summary>How the members of a group combine.</summary>
/// <remarks>
/// <see cref="None"/> is the negation, and it is a third join rather than a <c>bool Negate</c> on the
/// group. A flag reads as a modifier on something else; a third chip beside All and Any reads as the
/// choice it is, and it is the only form that renders back as an English sentence.
/// </remarks>
public enum ScopeJoin : byte
{
    All = 0,
    Any = 1,
    None = 2,
}

/// <summary>What a predicate does with the fact it names.</summary>
public enum ScopeOperator : byte
{
    Equals = 0,
    NotEquals = 1,
    GreaterThan = 2,
    GreaterOrEqual = 3,
    LessThan = 4,
    LessOrEqual = 5,
    Contains = 6,

    /// <summary>True when the fact is readable at all, whatever it says. Never Unknown.</summary>
    IsLive = 7,

    /// <summary>The negation of <see cref="IsLive"/>. Also never Unknown.</summary>
    IsNotLive = 8,

    /// <summary>Set membership. Legal only on the two group keys.</summary>
    InGroup = 9,
}

/// <summary>One test against one fact.</summary>
public sealed record ScopePredicate(ScopeFactKey Key, ScopeOperator Op, SignalValue Value)
{
    public static ScopePredicate Is(ScopeFactKey key, string text) =>
        new(key, ScopeOperator.Equals, SignalValue.Text(text));

    public static ScopePredicate IsNot(ScopeFactKey key, string text) =>
        new(key, ScopeOperator.NotEquals, SignalValue.Text(text));

    public static ScopePredicate InGroup(ScopeFactKey key, string groupId) =>
        new(key, ScopeOperator.InGroup, SignalValue.Text(groupId));

    public static ScopePredicate IsOn(string parameterName) =>
        new(ScopeFactKey.Parameter(parameterName), ScopeOperator.Equals, SignalValue.Bool(true));
}

/// <summary>
/// A guard: predicates and nested groups, combined one way.
/// </summary>
/// <remarks>
/// <b>An empty group is <see cref="ScopeOutcome.True"/>, and every caller depends on it.</b>
/// <see cref="Always"/> is what an unguarded thing carries, so "no guard" and "a guard nobody has filled
/// in" behave identically rather than one of them silently gating everything off.
/// <para>
/// Depth is capped at <see cref="MaxDepth"/> and the cap is enforced when a rule is saved, never inside
/// the evaluator. A guard that is already on disk has to produce an answer; refusing it at evaluate time
/// would turn a validation mistake into a feature that stops working with no way to see why.
/// </para>
/// </remarks>
public sealed record ScopeGroup(
    ScopeJoin Join,
    ImmutableArray<ScopePredicate> Predicates,
    ImmutableArray<ScopeGroup> Groups)
{
    public const int MaxDepth = 3;

    public static readonly ScopeGroup Always =
        new(ScopeJoin.All, ImmutableArray<ScopePredicate>.Empty, ImmutableArray<ScopeGroup>.Empty);

    public static ScopeGroup All(params ScopePredicate[] predicates) =>
        new(ScopeJoin.All, [.. predicates], ImmutableArray<ScopeGroup>.Empty);

    public static ScopeGroup Any(params ScopePredicate[] predicates) =>
        new(ScopeJoin.Any, [.. predicates], ImmutableArray<ScopeGroup>.Empty);

    public static ScopeGroup None(params ScopePredicate[] predicates) =>
        new(ScopeJoin.None, [.. predicates], ImmutableArray<ScopeGroup>.Empty);

    public ImmutableArray<ScopePredicate> SafePredicates =>
        Predicates.IsDefault ? ImmutableArray<ScopePredicate>.Empty : Predicates;

    public ImmutableArray<ScopeGroup> SafeGroups =>
        Groups.IsDefault ? ImmutableArray<ScopeGroup>.Empty : Groups;

    public bool IsEmpty => SafePredicates.Length == 0 && SafeGroups.Length == 0;

    /// <summary>How deep this guard nests, counting itself as one.</summary>
    public int Depth
    {
        get
        {
            int deepest = 0;
            foreach (ScopeGroup nested in SafeGroups)
            {
                int depth = nested.Depth;
                if (depth > deepest)
                    deepest = depth;
            }

            return deepest + 1;
        }
    }

    /// <summary>
    /// Every distinct key this guard reads, in the order the evaluator visits them.
    /// </summary>
    /// <remarks>
    /// Collected once when a rule is saved rather than on every evaluation, so a runtime can index rules
    /// by key and re-evaluate only the ones whose facts moved. Without it, one inbound parameter at
    /// face-tracking rates re-runs every guard in the book.
    /// </remarks>
    public ImmutableArray<ScopeFactKey> Reads()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ordered = ImmutableArray.CreateBuilder<ScopeFactKey>();
        Collect(this, seen, ordered);
        return ordered.ToImmutable();
    }

    private static void Collect(
        ScopeGroup group,
        HashSet<string> seen,
        ImmutableArray<ScopeFactKey>.Builder ordered)
    {
        foreach (ScopePredicate predicate in group.SafePredicates)
        {
            if (seen.Add(predicate.Key.Value))
                ordered.Add(predicate.Key);
        }

        foreach (ScopeGroup nested in group.SafeGroups)
            Collect(nested, seen, ordered);
    }
}

/// <summary>Which member settled a guard, so a person can be told why.</summary>
public readonly record struct ScopeBlock(ScopeFactKey Key, ScopeOperator Op, bool WasUnknown)
{
    public static readonly ScopeBlock None = new(default, default, false);

    public bool HasKey => !string.IsNullOrEmpty(Key.Value);
}
