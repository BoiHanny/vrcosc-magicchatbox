using MagicChatbox.Scope;
using MagicChatbox.Vocabulary;
using Newtonsoft.Json;
using System.Collections.Immutable;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using Xunit;

namespace MagicChatbox.Tests.Scope;

// A guard that cannot survive a restart is worse than no guard: it stops doing its job silently, and the
// screen still shows the rule that is no longer doing it.
public class ScopePersistenceTests
{
    private static T RoundTrip<T>(T value) =>
        JsonConvert.DeserializeObject<T>(
            JsonConvert.SerializeObject(value),
            JsonSettingsSerialization.DeserializerSettings);

    [Theory]
    [InlineData(SignalKind.Bool)]
    [InlineData(SignalKind.Int)]
    [InlineData(SignalKind.Float)]
    [InlineData(SignalKind.Text)]
    public void A_comparison_value_survives_the_two_members_a_predicate_stores(SignalKind kind)
    {
        SignalValue original = Sample(kind);

        SignalValue back = ScopeValues.From(kind, ScopeValues.TextOf(original));

        Assert.Equal(original.Kind, back.Kind);
        Assert.Equal(original, back);
    }

    [Fact]
    public void A_raw_SignalValue_still_cannot_be_serialised_which_is_why_it_is_not()
    {
        // Pinned deliberately. SignalValue keeps its payload in private fields with no settable member,
        // so a general-purpose serializer writes only the Kind and reads back Bool false. A guard stored
        // that way would come back saying "avatar.id is off" and look fine on screen while doing nothing.
        SignalValue back = RoundTrip(SignalValue.Text("avtr_one"));

        Assert.NotEqual(SignalKind.Text, back.Kind);
    }

    private static SignalValue Sample(SignalKind kind) => kind switch
    {
        SignalKind.Bool => SignalValue.Bool(true),
        SignalKind.Int => SignalValue.Int(42),
        SignalKind.Float => SignalValue.Float(0.25f),
        _ => SignalValue.Text("Public"),
    };

    [Fact]
    public void A_whole_guard_survives_being_written_to_disk()
    {
        var rule = ScopeRule.For(
            "rule-1",
            "Only while streaming",
            ScopeTarget.Integration("HeartRate"),
            ScopeGroup.All(
                ScopePredicate.InGroup(ScopeFactKey.AvatarGroup, "streaming"),
                ScopePredicate.IsNot(ScopeFactKey.InstanceType, "Public"),
                ScopePredicate.IsOn("MCB/Cfg/HeartRate"))) with
        {
            Enabled = true,
            DwellMs = 1500,
            BlockWhileUnknown = true,
        };

        ScopeRule back = RoundTrip(rule);

        Assert.Equal(rule.Id, back.Id);
        Assert.Equal(rule.Name, back.Name);
        Assert.True(back.Enabled);
        Assert.Equal(1500, back.DwellMs);
        Assert.True(back.BlockWhileUnknown);
        Assert.Equal(rule.Target, back.Target);
        Assert.Equal(ScopeMirror.Canonical(rule.SafeWhen), ScopeMirror.Canonical(back.SafeWhen));
    }

    [Fact]
    public void A_nested_guard_survives_too()
    {
        var guard = new ScopeGroup(
            ScopeJoin.All,
            [ScopePredicate.Is(ScopeFactKey.AvatarId, "avtr_one")],
            [ScopeGroup.Any(
                ScopePredicate.Is(ScopeFactKey.WorldId, "wrld_a"),
                ScopePredicate.Is(ScopeFactKey.WorldId, "wrld_b"))]);

        Assert.Equal(ScopeMirror.Canonical(guard), ScopeMirror.Canonical(RoundTrip(guard)));
    }

    [Fact]
    public void Whole_settings_survive_being_written_to_disk()
    {
        var settings = new ScopeSettings();
        settings.Rules.Add(ScopeRule.For("rule-1", "A", ScopeTarget.Sending,
            ScopeGroup.All(ScopePredicate.Is(ScopeFactKey.InstanceType, "Public"))) with { Enabled = true });
        settings.AvatarGroups.Add(new AvatarGroup("group-1", "Streaming", ["avtr_one", "avtr_two"]));
        settings.WorldGroups.Add(new WorldGroup("group-2", "Muted", ["wrld_a"]));

        ScopeSettings back = RoundTrip(settings);

        Assert.Equal("A", back.Rules.Single().Name);
        Assert.Equal(
            "instance.type is Public",
            ScopeMirror.Canonical(back.Rules.Single().SafeWhen));
        Assert.Equal(["avtr_one", "avtr_two"], back.AvatarGroups.Single().SafeAvatarIds.ToArray());
        Assert.Equal("Muted", back.WorldGroups.Single().Name);
    }

    [Fact]
    public void A_guard_read_back_with_no_arrays_at_all_does_not_throw()
    {
        // Newtonsoft can hand back an ImmutableArray field as default rather than empty, and default
        // throws on enumeration. Every read goes through SafePredicates/SafeGroups for this reason.
        ScopeGroup guard = JsonConvert.DeserializeObject<ScopeGroup>(
            "{\"Join\":0}", JsonSettingsSerialization.DeserializerSettings);

        Assert.Equal(ScopeOutcome.True, ScopeEvaluator.Evaluate(guard, ScopeFacts.Empty));
        Assert.Empty(guard.Reads());
    }
}
