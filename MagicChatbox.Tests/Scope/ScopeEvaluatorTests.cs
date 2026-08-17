using MagicChatbox.Scope;
using MagicChatbox.Vocabulary;
using System.Collections.Immutable;
using Xunit;

namespace MagicChatbox.Tests.Scope;

// A guard decides whether something the user switched on may actually run. The third value is the whole
// reason it works: the schema and sense stores are cleared on every avatar change, so avatar facts are
// genuinely unreadable for a few seconds after a swap. A two-valued guard reads that window as "no" and
// shuts down whatever it gates, every time somebody changes clothes.
public class ScopeEvaluatorTests
{
    private static ScopeFacts Facts(params (ScopeFactKey Key, SignalValue Value, bool Live)[] cells)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, ScopeCell>(System.StringComparer.Ordinal);
        foreach (var cell in cells)
            builder[cell.Key.Value] = new ScopeCell(cell.Value, cell.Live);

        return ScopeFacts.Empty with { Cells = builder.ToImmutable() };
    }

    private static ScopeFacts Wearing(string avatarId) =>
        Facts((ScopeFactKey.AvatarId, SignalValue.Text(avatarId), true));

    [Fact]
    public void An_empty_guard_is_true_so_that_no_guard_and_a_blank_one_behave_alike()
    {
        Assert.Equal(ScopeOutcome.True, ScopeEvaluator.Evaluate(ScopeGroup.Always, ScopeFacts.Empty));
    }

    [Fact]
    public void An_unreadable_fact_is_unknown_rather_than_false()
    {
        ScopeFacts facts = Facts((ScopeFactKey.AvatarId, SignalValue.Text("avtr_one"), false));

        ScopeOutcome outcome = ScopeEvaluator.Evaluate(
            ScopeGroup.All(ScopePredicate.Is(ScopeFactKey.AvatarId, "avtr_one")),
            facts,
            out ScopeBlock block);

        Assert.Equal(ScopeOutcome.Unknown, outcome);
        Assert.True(block.WasUnknown);
        Assert.Equal(ScopeFactKey.AvatarId, block.Key);
    }

    [Fact]
    public void An_absent_fact_is_unknown_too()
    {
        ScopeOutcome outcome = ScopeEvaluator.Evaluate(
            ScopeGroup.All(ScopePredicate.Is(ScopeFactKey.WorldId, "wrld_x")),
            ScopeFacts.Empty);

        Assert.Equal(ScopeOutcome.Unknown, outcome);
    }

    [Theory]
    [InlineData("avtr_one", ScopeOutcome.True)]
    [InlineData("avtr_two", ScopeOutcome.False)]
    public void A_readable_fact_decides(string worn, ScopeOutcome expected)
    {
        Assert.Equal(
            expected,
            ScopeEvaluator.Evaluate(ScopeGroup.All(ScopePredicate.Is(ScopeFactKey.AvatarId, "avtr_one")), Wearing(worn)));
    }

    [Fact]
    public void A_definite_no_beats_an_unreadable_maybe_inside_All()
    {
        // One refusal settles it. Reporting Unknown here would hold a decision that is already made.
        ScopeFacts facts = Facts(
            (ScopeFactKey.WorldId, SignalValue.Text("wrld_x"), false),
            (ScopeFactKey.AvatarId, SignalValue.Text("avtr_two"), true));

        ScopeOutcome outcome = ScopeEvaluator.Evaluate(
            ScopeGroup.All(
                ScopePredicate.Is(ScopeFactKey.WorldId, "wrld_x"),
                ScopePredicate.Is(ScopeFactKey.AvatarId, "avtr_one")),
            facts,
            out ScopeBlock block);

        Assert.Equal(ScopeOutcome.False, outcome);
        Assert.Equal(ScopeFactKey.AvatarId, block.Key);
        Assert.False(block.WasUnknown);
    }

    [Fact]
    public void A_definite_yes_beats_an_unreadable_maybe_inside_Any()
    {
        ScopeFacts facts = Facts(
            (ScopeFactKey.WorldId, SignalValue.Text("wrld_x"), false),
            (ScopeFactKey.AvatarId, SignalValue.Text("avtr_one"), true));

        ScopeOutcome outcome = ScopeEvaluator.Evaluate(
            ScopeGroup.Any(
                ScopePredicate.Is(ScopeFactKey.WorldId, "wrld_x"),
                ScopePredicate.Is(ScopeFactKey.AvatarId, "avtr_one")),
            facts,
            out ScopeBlock block);

        Assert.Equal(ScopeOutcome.True, outcome);
        Assert.Equal(ScopeFactKey.AvatarId, block.Key);
    }

    [Fact]
    public void An_All_with_nothing_refused_but_something_unreadable_is_unknown()
    {
        ScopeFacts facts = Facts(
            (ScopeFactKey.AvatarId, SignalValue.Text("avtr_one"), true),
            (ScopeFactKey.WorldId, SignalValue.Text("wrld_x"), false));

        Assert.Equal(
            ScopeOutcome.Unknown,
            ScopeEvaluator.Evaluate(
                ScopeGroup.All(
                    ScopePredicate.Is(ScopeFactKey.AvatarId, "avtr_one"),
                    ScopePredicate.Is(ScopeFactKey.WorldId, "wrld_x")),
                facts));
    }

    [Fact]
    public void None_is_Any_negated_and_negating_unknown_leaves_it_unknown()
    {
        ScopeFacts unreadable = Facts((ScopeFactKey.AvatarId, SignalValue.Text("avtr_one"), false));

        Assert.Equal(
            ScopeOutcome.Unknown,
            ScopeEvaluator.Evaluate(ScopeGroup.None(ScopePredicate.Is(ScopeFactKey.AvatarId, "avtr_one")), unreadable));

        Assert.Equal(
            ScopeOutcome.False,
            ScopeEvaluator.Evaluate(ScopeGroup.None(ScopePredicate.Is(ScopeFactKey.AvatarId, "avtr_one")), Wearing("avtr_one")));

        Assert.Equal(
            ScopeOutcome.True,
            ScopeEvaluator.Evaluate(ScopeGroup.None(ScopePredicate.Is(ScopeFactKey.AvatarId, "avtr_one")), Wearing("avtr_two")));
    }

    [Fact]
    public void An_empty_Any_is_false_and_an_empty_None_is_true()
    {
        Assert.Equal(ScopeOutcome.False, ScopeEvaluator.Evaluate(ScopeGroup.Any(), ScopeFacts.Empty));
        Assert.Equal(ScopeOutcome.True, ScopeEvaluator.Evaluate(ScopeGroup.None(), ScopeFacts.Empty));
    }

    [Fact]
    public void Liveness_can_be_asked_about_directly_and_never_answers_unknown()
    {
        ScopeFacts dark = Facts((ScopeFactKey.WorldId, SignalValue.Text("wrld_x"), false));

        Assert.Equal(
            ScopeOutcome.False,
            ScopeEvaluator.Evaluate(ScopeGroup.All(new ScopePredicate(ScopeFactKey.WorldId, ScopeOperator.IsLive, default)), dark));

        Assert.Equal(
            ScopeOutcome.True,
            ScopeEvaluator.Evaluate(ScopeGroup.All(new ScopePredicate(ScopeFactKey.WorldId, ScopeOperator.IsNotLive, default)), dark));
    }

    [Fact]
    public void Membership_is_unknown_until_the_identity_it_is_keyed_on_is_readable()
    {
        // The resolved set is empty when nobody knows which avatar is worn. Answering False from that
        // would be the two-valued collapse in a different disguise.
        ScopeFacts dark = Facts((ScopeFactKey.AvatarId, SignalValue.Text(""), false));

        Assert.Equal(
            ScopeOutcome.Unknown,
            ScopeEvaluator.Evaluate(ScopeGroup.All(ScopePredicate.InGroup(ScopeFactKey.AvatarGroup, "g1")), dark));
    }

    [Fact]
    public void Membership_reads_the_resolved_set_when_the_identity_is_known()
    {
        ScopeFacts facts = Wearing("avtr_one") with
        {
            AvatarGroupIds = ImmutableHashSet.Create("streaming"),
        };

        Assert.Equal(
            ScopeOutcome.True,
            ScopeEvaluator.Evaluate(ScopeGroup.All(ScopePredicate.InGroup(ScopeFactKey.AvatarGroup, "streaming")), facts));

        Assert.Equal(
            ScopeOutcome.False,
            ScopeEvaluator.Evaluate(ScopeGroup.All(ScopePredicate.InGroup(ScopeFactKey.AvatarGroup, "private")), facts));
    }

    [Fact]
    public void Text_comparison_ignores_case_because_nobody_types_a_world_name_twice_the_same_way()
    {
        ScopeFacts facts = Facts((ScopeFactKey.WorldName, SignalValue.Text("The Black Cat"), true));

        Assert.Equal(
            ScopeOutcome.True,
            ScopeEvaluator.Evaluate(ScopeGroup.All(ScopePredicate.Is(ScopeFactKey.WorldName, "the black cat")), facts));

        Assert.Equal(
            ScopeOutcome.True,
            ScopeEvaluator.Evaluate(
                ScopeGroup.All(new ScopePredicate(ScopeFactKey.WorldName, ScopeOperator.Contains, SignalValue.Text("BLACK"))),
                facts));
    }

    [Fact]
    public void Comparing_a_number_to_words_is_unknown_rather_than_false()
    {
        ScopeFacts facts = Facts((ScopeFactKey.Parameter("HR"), SignalValue.Int(88), true));

        Assert.Equal(
            ScopeOutcome.Unknown,
            ScopeEvaluator.Evaluate(ScopeGroup.All(ScopePredicate.Is(ScopeFactKey.Parameter("HR"), "high")), facts));
    }

    [Theory]
    [InlineData(ScopeOperator.GreaterThan, 80, ScopeOutcome.True)]
    [InlineData(ScopeOperator.GreaterThan, 88, ScopeOutcome.False)]
    [InlineData(ScopeOperator.GreaterOrEqual, 88, ScopeOutcome.True)]
    [InlineData(ScopeOperator.LessThan, 90, ScopeOutcome.True)]
    [InlineData(ScopeOperator.LessOrEqual, 87, ScopeOutcome.False)]
    public void Numbers_compare_across_kinds(ScopeOperator op, int against, ScopeOutcome expected)
    {
        ScopeFacts facts = Facts((ScopeFactKey.Parameter("HR"), SignalValue.Int(88), true));

        Assert.Equal(
            expected,
            ScopeEvaluator.Evaluate(
                ScopeGroup.All(new ScopePredicate(ScopeFactKey.Parameter("HR"), op, SignalValue.Float(against))),
                facts));
    }

    [Fact]
    public void A_bool_parameter_reads_as_a_number_when_compared_as_one()
    {
        ScopeFacts facts = Facts((ScopeFactKey.Parameter("MCB/Cfg/HeartRate"), SignalValue.Bool(true), true));

        Assert.Equal(
            ScopeOutcome.True,
            ScopeEvaluator.Evaluate(ScopeGroup.All(ScopePredicate.IsOn("MCB/Cfg/HeartRate")), facts));
    }

    [Fact]
    public void Nested_groups_are_visited_after_predicates_in_author_order()
    {
        var guard = new ScopeGroup(
            ScopeJoin.All,
            [ScopePredicate.Is(ScopeFactKey.AvatarId, "avtr_one")],
            [ScopeGroup.All(ScopePredicate.Is(ScopeFactKey.WorldId, "wrld_x"))]);

        ScopeFacts facts = Facts(
            (ScopeFactKey.AvatarId, SignalValue.Text("avtr_one"), false),
            (ScopeFactKey.WorldId, SignalValue.Text("wrld_x"), false));

        ScopeEvaluator.Evaluate(guard, facts, out ScopeBlock block);

        Assert.Equal(ScopeFactKey.AvatarId, block.Key);
    }

    [Fact]
    public void The_keys_a_guard_reads_are_collected_once_without_duplicates()
    {
        var guard = new ScopeGroup(
            ScopeJoin.All,
            [
                ScopePredicate.Is(ScopeFactKey.AvatarId, "a"),
                ScopePredicate.Is(ScopeFactKey.AvatarId, "b"),
            ],
            [ScopeGroup.Any(ScopePredicate.Is(ScopeFactKey.WorldId, "w"))]);

        Assert.Equal(
            new[] { ScopeFactKey.AvatarId, ScopeFactKey.WorldId },
            guard.Reads());
    }

    [Fact]
    public void Depth_counts_the_outermost_group_as_one()
    {
        Assert.Equal(1, ScopeGroup.Always.Depth);

        var two = new ScopeGroup(ScopeJoin.All, ImmutableArray<ScopePredicate>.Empty, [ScopeGroup.Always]);
        Assert.Equal(2, two.Depth);
    }

    [Fact]
    public void A_default_constructed_group_does_not_throw_on_its_uninitialised_arrays()
    {
        // Newtonsoft can hand back a record whose ImmutableArray fields are default rather than empty.
        var fromDisk = new ScopeGroup(ScopeJoin.All, default, default);

        Assert.Equal(ScopeOutcome.True, ScopeEvaluator.Evaluate(fromDisk, ScopeFacts.Empty));
        Assert.Empty(fromDisk.Reads());
        Assert.Equal(1, fromDisk.Depth);
    }
}
