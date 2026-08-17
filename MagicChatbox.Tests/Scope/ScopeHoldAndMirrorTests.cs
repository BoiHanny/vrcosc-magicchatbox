using MagicChatbox.Scope;
using MagicChatbox.Vocabulary;
using System;
using Xunit;

namespace MagicChatbox.Tests.Scope;

public class ScopeHoldTests
{
    private static long Ms(int milliseconds) => TimeSpan.FromMilliseconds(milliseconds).Ticks;

    private static readonly TimeSpan Dwell = TimeSpan.FromSeconds(2);

    [Fact]
    public void The_first_reading_commits_immediately()
    {
        // Nothing to settle against on the first observation, and making somebody wait two seconds for
        // the app's opening state would be a delay with no question behind it.
        var hold = default(ScopeHold);
        hold.Observe(ScopeOutcome.True, Ms(0));

        Assert.True(hold.HasHeldFor(ScopeOutcome.True, Dwell, Ms(0)));
    }

    [Fact]
    public void A_change_has_to_stand_for_the_whole_dwell()
    {
        var hold = default(ScopeHold);
        hold.Observe(ScopeOutcome.True, Ms(0));
        hold.Observe(ScopeOutcome.False, Ms(1000));

        Assert.False(hold.HasHeldFor(ScopeOutcome.False, Dwell, Ms(2500)));
        Assert.True(hold.HasHeldFor(ScopeOutcome.False, Dwell, Ms(3000)));
    }

    [Fact]
    public void Repeating_the_same_answer_does_not_restart_the_clock()
    {
        var hold = default(ScopeHold);
        hold.Observe(ScopeOutcome.True, Ms(0));
        hold.Observe(ScopeOutcome.False, Ms(1000));
        hold.Observe(ScopeOutcome.False, Ms(1500));
        hold.Observe(ScopeOutcome.False, Ms(2900));

        Assert.True(hold.HasHeldFor(ScopeOutcome.False, Dwell, Ms(3000)));
    }

    [Fact]
    public void Unknown_breaks_the_run_rather_than_extending_it()
    {
        // A fact dropping out is the absence of evidence, not evidence the last answer still holds. The
        // dwell has to start again when it comes back, or a guard commits on the strength of a window in
        // which nothing could be read.
        var hold = default(ScopeHold);
        hold.Observe(ScopeOutcome.True, Ms(0));
        hold.Observe(ScopeOutcome.False, Ms(1000));
        hold.Observe(ScopeOutcome.Unknown, Ms(1500));
        hold.Observe(ScopeOutcome.False, Ms(2000));

        Assert.False(hold.HasHeldFor(ScopeOutcome.False, Dwell, Ms(3500)));
        Assert.True(hold.HasHeldFor(ScopeOutcome.False, Dwell, Ms(4000)));
    }

    [Fact]
    public void A_zero_dwell_commits_on_sight()
    {
        var hold = default(ScopeHold);
        hold.Observe(ScopeOutcome.True, Ms(0));
        hold.Observe(ScopeOutcome.False, Ms(10));

        Assert.True(hold.HasHeldFor(ScopeOutcome.False, TimeSpan.Zero, Ms(10)));
    }

    [Fact]
    public void Asking_about_an_answer_it_is_not_currently_giving_is_always_false()
    {
        var hold = default(ScopeHold);
        hold.Observe(ScopeOutcome.False, Ms(0));

        Assert.False(hold.HasHeldFor(ScopeOutcome.True, TimeSpan.Zero, Ms(10_000)));
    }

    [Fact]
    public void A_reset_hold_has_observed_nothing()
    {
        var hold = default(ScopeHold);
        hold.Observe(ScopeOutcome.True, Ms(0));
        hold.Reset();

        Assert.False(hold.HasObserved);
        Assert.False(hold.HasHeldFor(ScopeOutcome.True, TimeSpan.Zero, Ms(0)));
    }
}

public class ScopeMirrorTests
{
    private sealed class Names : IScopeFactNames
    {
        public string? NameFor(ScopeFactKey key) =>
            key == ScopeFactKey.AvatarGroup ? "the avatar" :
            key == ScopeFactKey.InstanceType ? "the instance" : null;

        public string? ValueFor(ScopeFactKey key, SignalValue value) =>
            key == ScopeFactKey.AvatarGroup ? $"in {value.AsText()}" : null;
    }

    [Fact]
    public void An_empty_guard_reads_as_always()
    {
        Assert.Equal("always", ScopeMirror.Canonical(ScopeGroup.Always));
        Assert.Equal("never", ScopeMirror.Canonical(ScopeGroup.Any()));
    }

    [Fact]
    public void A_guard_reads_back_as_an_english_sentence()
    {
        var guard = ScopeGroup.All(
            ScopePredicate.InGroup(ScopeFactKey.AvatarGroup, "streaming"),
            ScopePredicate.IsNot(ScopeFactKey.InstanceType, "Public"));

        Assert.Equal(
            "avatar.group is streaming and instance.type is not Public",
            ScopeMirror.Canonical(guard));

        Assert.Equal(
            "the avatar is in streaming and the instance is not Public",
            ScopeMirror.Friendly(guard, new Names()));
    }

    [Fact]
    public void The_canonical_form_never_uses_display_names()
    {
        // It is the only form stored or diffed, so a renamed group must not read as an edited rule.
        var guard = ScopeGroup.All(ScopePredicate.InGroup(ScopeFactKey.AvatarGroup, "streaming"));

        Assert.Equal(ScopeMirror.Canonical(guard), ScopeMirror.Friendly(guard, names: null));
    }

    [Fact]
    public void A_single_negated_test_reads_as_not_rather_than_none_of()
    {
        Assert.Equal(
            "not avatar.id is avtr_one",
            ScopeMirror.Canonical(ScopeGroup.None(ScopePredicate.Is(ScopeFactKey.AvatarId, "avtr_one"))));
    }

    [Fact]
    public void Several_negated_tests_read_as_none_of()
    {
        Assert.Equal(
            "none of (avatar.id is a or avatar.id is b)",
            ScopeMirror.Canonical(ScopeGroup.None(
                ScopePredicate.Is(ScopeFactKey.AvatarId, "a"),
                ScopePredicate.Is(ScopeFactKey.AvatarId, "b"))));
    }

    [Fact]
    public void A_nested_group_is_bracketed_so_the_sentence_cannot_be_misread()
    {
        var guard = new ScopeGroup(
            ScopeJoin.All,
            [ScopePredicate.Is(ScopeFactKey.AvatarId, "a")],
            [ScopeGroup.Any(
                ScopePredicate.Is(ScopeFactKey.WorldId, "w1"),
                ScopePredicate.Is(ScopeFactKey.WorldId, "w2"))]);

        Assert.Equal(
            "avatar.id is a and (world.id is w1 or world.id is w2)",
            ScopeMirror.Canonical(guard));
    }

    [Fact]
    public void Booleans_read_as_on_and_off_rather_than_true_and_false()
    {
        Assert.Equal(
            "avatar.param.MCB/Cfg/HeartRate is on",
            ScopeMirror.Canonical(ScopeGroup.All(ScopePredicate.IsOn("MCB/Cfg/HeartRate"))));
    }

    [Fact]
    public void A_blocked_guard_says_which_fact_stopped_it()
    {
        var waiting = new ScopeBlock(ScopeFactKey.AvatarId, ScopeOperator.Equals, WasUnknown: true);
        var refused = new ScopeBlock(ScopeFactKey.InstanceType, ScopeOperator.Equals, WasUnknown: false);

        Assert.Equal("waiting on avatar.id", ScopeMirror.Because(ScopeOutcome.Unknown, waiting, names: null));
        Assert.Equal("instance.type does not match", ScopeMirror.Because(ScopeOutcome.False, refused, names: null));
        Assert.Equal(string.Empty, ScopeMirror.Because(ScopeOutcome.True, refused, names: null));
    }
}

public class ScopeRuleValidationTests
{
    private static ScopeRule Rule(ScopeGroup when) =>
        ScopeRule.For("r1", "Heart rate while streaming", ScopeTarget.Integration("HeartRate"), when);

    [Fact]
    public void A_well_formed_rule_has_no_problems()
    {
        Assert.Empty(Rule(ScopeGroup.All(ScopePredicate.InGroup(ScopeFactKey.AvatarGroup, "g"))).Validate());
    }

    [Fact]
    public void Membership_is_refused_on_anything_that_is_not_a_group()
    {
        var problems = Rule(ScopeGroup.All(ScopePredicate.InGroup(ScopeFactKey.AvatarId, "g"))).Validate();

        ScopeProblem problem = Assert.Single(problems);
        Assert.Equal(ScopeProblemCode.OperatorInvalidForKey, problem.Code);
        Assert.Equal("when.predicates[0]", problem.Slot);
    }

    [Fact]
    public void A_group_key_is_refused_for_anything_but_membership()
    {
        var problems = Rule(ScopeGroup.All(ScopePredicate.Is(ScopeFactKey.AvatarGroup, "g"))).Validate();

        Assert.Equal(ScopeProblemCode.OperatorInvalidForKey, Assert.Single(problems).Code);
    }

    [Fact]
    public void Everything_wrong_is_reported_at_once_and_tagged_with_where_it_is()
    {
        // All at once rather than first-wins, so an editor can outline every offending card instead of
        // making somebody fix one mistake per save.
        var rule = new ScopeRule(
            Id: "",
            Name: "  ",
            Enabled: true,
            Target: ScopeTarget.Integration(""),
            When: ScopeGroup.All(ScopePredicate.InGroup(ScopeFactKey.AvatarId, "g")),
            DwellMs: -1,
            Note: "");

        var problems = rule.Validate();

        Assert.Contains(problems, p => p.Code == ScopeProblemCode.MissingId);
        Assert.Contains(problems, p => p.Code == ScopeProblemCode.MissingName);
        Assert.Contains(problems, p => p.Code == ScopeProblemCode.MissingTarget);
        Assert.Contains(problems, p => p.Code == ScopeProblemCode.DwellOutOfRange);
        Assert.Contains(problems, p => p.Code == ScopeProblemCode.OperatorInvalidForKey);
    }

    [Fact]
    public void Nesting_deeper_than_the_cap_is_refused_at_save()
    {
        ScopeGroup deep = ScopeGroup.Always;
        for (int i = 0; i < ScopeGroup.MaxDepth; i++)
            deep = new ScopeGroup(ScopeJoin.All, System.Collections.Immutable.ImmutableArray<ScopePredicate>.Empty, [deep]);

        Assert.Contains(Rule(deep).Validate(), p => p.Code == ScopeProblemCode.DepthExceeded);
    }

    [Fact]
    public void A_rule_that_guards_sending_needs_no_target_key()
    {
        var rule = ScopeRule.For("r1", "Quiet here", ScopeTarget.Sending, ScopeGroup.Always);

        Assert.Empty(rule.Validate());
    }

    [Fact]
    public void A_nested_problem_names_the_group_it_is_in()
    {
        var guard = new ScopeGroup(
            ScopeJoin.All,
            System.Collections.Immutable.ImmutableArray<ScopePredicate>.Empty,
            [ScopeGroup.All(ScopePredicate.InGroup(ScopeFactKey.AvatarId, "g"))]);

        Assert.Equal("when.groups[0].predicates[0]", Assert.Single(Rule(guard).Validate()).Slot);
    }

    [Fact]
    public void A_ludicrous_dwell_is_clamped_rather_than_trusted()
    {
        var rule = ScopeRule.For("r1", "n", ScopeTarget.Sending, ScopeGroup.Always) with { DwellMs = int.MaxValue };

        Assert.Equal(TimeSpan.FromMilliseconds(ScopeRule.MaxDwellMs), rule.Dwell);
    }
}
