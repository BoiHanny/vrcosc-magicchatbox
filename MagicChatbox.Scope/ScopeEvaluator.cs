using System.Collections.Immutable;
using MagicChatbox.Vocabulary;

namespace MagicChatbox.Scope;

/// <summary>
/// Answers a guard against a snapshot. Pure, three-valued, and the only place that decides.
/// </summary>
/// <remarks>
/// <para>
/// <b>Kleene logic, with the asymmetry written down.</b> <c>All</c> is False if any member is False even
/// when another is Unknown, because one definite refusal settles it; it is Unknown only when nothing
/// refused and something could not be read. <c>Any</c> mirrors that. <c>None</c> is <c>Any</c> negated,
/// and negating Unknown leaves it Unknown.
/// </para>
/// <para>
/// <b>Members are visited predicates-first, in author order, everywhere.</b> That ordering is shared with
/// <see cref="ScopeGroup.Reads"/> and with the mirror, so "the first thing that stopped it" means the same
/// thing in the evaluator, in the sentence a person reads, and in the key set a runtime indexes on.
/// </para>
/// </remarks>
public static class ScopeEvaluator
{
    public static ScopeOutcome Evaluate(ScopeGroup group, ScopeFacts facts) =>
        Evaluate(group, facts, out _);

    public static ScopeOutcome Evaluate(ScopeGroup group, ScopeFacts facts, out ScopeBlock block)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(facts);

        return EvaluateGroup(group, facts, out block);
    }

    private static ScopeOutcome EvaluateGroup(ScopeGroup group, ScopeFacts facts, out ScopeBlock block)
    {
        block = ScopeBlock.None;

        ImmutableArray<ScopePredicate> predicates = group.SafePredicates;
        ImmutableArray<ScopeGroup> nested = group.SafeGroups;

        if (predicates.Length == 0 && nested.Length == 0)
            return group.Join == ScopeJoin.Any ? ScopeOutcome.False : ScopeOutcome.True;

        ScopeBlock firstFalse = ScopeBlock.None;
        ScopeBlock firstTrue = ScopeBlock.None;
        ScopeBlock firstUnknown = ScopeBlock.None;
        bool sawFalse = false;
        bool sawTrue = false;
        bool sawUnknown = false;

        foreach (ScopePredicate predicate in predicates)
        {
            ScopeOutcome outcome = EvaluatePredicate(predicate, facts);
            var here = new ScopeBlock(predicate.Key, predicate.Op, outcome == ScopeOutcome.Unknown);
            Record(outcome, here, ref sawFalse, ref firstFalse, ref sawTrue, ref firstTrue, ref sawUnknown, ref firstUnknown);
        }

        foreach (ScopeGroup child in nested)
        {
            ScopeOutcome outcome = EvaluateGroup(child, facts, out ScopeBlock childBlock);
            Record(outcome, childBlock, ref sawFalse, ref firstFalse, ref sawTrue, ref firstTrue, ref sawUnknown, ref firstUnknown);
        }

        switch (group.Join)
        {
            case ScopeJoin.All:
                if (sawFalse)
                {
                    block = firstFalse;
                    return ScopeOutcome.False;
                }

                if (sawUnknown)
                {
                    block = firstUnknown;
                    return ScopeOutcome.Unknown;
                }

                return ScopeOutcome.True;

            case ScopeJoin.Any:
                if (sawTrue)
                {
                    block = firstTrue;
                    return ScopeOutcome.True;
                }

                if (sawUnknown)
                {
                    block = firstUnknown;
                    return ScopeOutcome.Unknown;
                }

                return ScopeOutcome.False;

            case ScopeJoin.None:
                if (sawTrue)
                {
                    block = firstTrue;
                    return ScopeOutcome.False;
                }

                if (sawUnknown)
                {
                    block = firstUnknown;
                    return ScopeOutcome.Unknown;
                }

                return ScopeOutcome.True;
        }

        return ScopeOutcome.Unknown;
    }

    private static void Record(
        ScopeOutcome outcome,
        ScopeBlock where,
        ref bool sawFalse,
        ref ScopeBlock firstFalse,
        ref bool sawTrue,
        ref ScopeBlock firstTrue,
        ref bool sawUnknown,
        ref ScopeBlock firstUnknown)
    {
        switch (outcome)
        {
            case ScopeOutcome.False:
                if (!sawFalse)
                {
                    sawFalse = true;
                    firstFalse = where;
                }

                break;

            case ScopeOutcome.True:
                if (!sawTrue)
                {
                    sawTrue = true;
                    firstTrue = where;
                }

                break;

            case ScopeOutcome.Unknown:
                if (!sawUnknown)
                {
                    sawUnknown = true;
                    firstUnknown = where;
                }

                break;
        }
    }

    internal static ScopeOutcome EvaluatePredicate(ScopePredicate predicate, ScopeFacts facts)
    {
        ScopeCell cell = facts.Read(predicate.Key);

        // These two ask about readability itself, so they are the only operators that can answer while
        // the fact is dark -- and the only ones that never return Unknown.
        if (predicate.Op == ScopeOperator.IsLive)
            return cell.IsLive ? ScopeOutcome.True : ScopeOutcome.False;

        if (predicate.Op == ScopeOperator.IsNotLive)
            return cell.IsLive ? ScopeOutcome.False : ScopeOutcome.True;

        if (predicate.Op == ScopeOperator.InGroup)
            return EvaluateMembership(predicate, facts);

        if (!cell.IsLive)
            return ScopeOutcome.Unknown;

        return predicate.Op switch
        {
            ScopeOperator.Equals => Same(cell.Value, predicate.Value),
            ScopeOperator.NotEquals => Negate(Same(cell.Value, predicate.Value)),
            ScopeOperator.Contains => TextContains(cell.Value, predicate.Value),
            ScopeOperator.GreaterThan => Compare(cell.Value, predicate.Value, static c => c > 0),
            ScopeOperator.GreaterOrEqual => Compare(cell.Value, predicate.Value, static c => c >= 0),
            ScopeOperator.LessThan => Compare(cell.Value, predicate.Value, static c => c < 0),
            ScopeOperator.LessOrEqual => Compare(cell.Value, predicate.Value, static c => c <= 0),
            ScopeOperator.IsLive => ScopeOutcome.True,
            ScopeOperator.IsNotLive => ScopeOutcome.False,
            ScopeOperator.InGroup => ScopeOutcome.Unknown,
        };
    }

    /// <remarks>
    /// The membership sets are always resolved, so a naive test would answer False for an avatar nobody
    /// has identified yet -- the two-valued collapse this whole type exists to avoid. Membership is
    /// therefore Unknown until the identity the groups are keyed on is readable.
    /// </remarks>
    private static ScopeOutcome EvaluateMembership(ScopePredicate predicate, ScopeFacts facts)
    {
        if (predicate.Value.Kind != SignalKind.Text)
            return ScopeOutcome.Unknown;

        if (predicate.Key == ScopeFactKey.AvatarGroup)
        {
            return !facts.Read(ScopeFactKey.AvatarId).IsLive
                ? ScopeOutcome.Unknown
                : Bool(facts.AvatarGroupIds.Contains(predicate.Value.AsText()));
        }

        if (predicate.Key == ScopeFactKey.WorldGroup)
        {
            return !facts.Read(ScopeFactKey.WorldId).IsLive
                ? ScopeOutcome.Unknown
                : Bool(facts.WorldGroupIds.Contains(predicate.Value.AsText()));
        }

        return ScopeOutcome.Unknown;
    }

    private static ScopeOutcome Same(SignalValue left, SignalValue right)
    {
        if (left.Kind == SignalKind.Text || right.Kind == SignalKind.Text)
        {
            if (left.Kind != SignalKind.Text || right.Kind != SignalKind.Text)
                return ScopeOutcome.Unknown;

            return Bool(string.Equals(left.AsText(), right.AsText(), StringComparison.OrdinalIgnoreCase));
        }

        if (left.Kind == right.Kind)
            return Bool(left == right);

        if (!right.TryConvertTo(left.Kind, out SignalValue converted))
            return ScopeOutcome.Unknown;

        return Bool(left == converted);
    }

    private static ScopeOutcome TextContains(SignalValue haystack, SignalValue needle)
    {
        if (haystack.Kind != SignalKind.Text || needle.Kind != SignalKind.Text)
            return ScopeOutcome.Unknown;

        return Bool(haystack.AsText().Contains(needle.AsText(), StringComparison.OrdinalIgnoreCase));
    }

    private static ScopeOutcome Compare(SignalValue left, SignalValue right, Func<int, bool> accept)
    {
        if (!TryNumber(left, out double a) || !TryNumber(right, out double b))
            return ScopeOutcome.Unknown;

        if (double.IsNaN(a) || double.IsNaN(b))
            return ScopeOutcome.Unknown;

        return Bool(accept(a.CompareTo(b)));
    }

    private static bool TryNumber(SignalValue value, out double number)
    {
        switch (value.Kind)
        {
            case SignalKind.Int:
                number = value.AsInt();
                return true;
            case SignalKind.Float:
                number = value.AsFloat();
                return true;
            case SignalKind.Bool:
                number = value.AsBool() ? 1d : 0d;
                return true;
            default:
                number = 0d;
                return false;
        }
    }

    private static ScopeOutcome Bool(bool value) => value ? ScopeOutcome.True : ScopeOutcome.False;

    private static ScopeOutcome Negate(ScopeOutcome outcome) => outcome switch
    {
        ScopeOutcome.True => ScopeOutcome.False,
        ScopeOutcome.False => ScopeOutcome.True,
        _ => ScopeOutcome.Unknown,
    };
}
