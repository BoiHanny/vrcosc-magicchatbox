using MagicChatbox.Scope;
using MagicChatbox.Vocabulary;
using MagicChatbox.Vrc;
using System;
using System.Collections.Generic;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Integrations;
using vrcosc_magicchatbox.Core.Vrc;
using vrcosc_magicchatbox.Services.Scope;
using Xunit;

namespace MagicChatbox.Tests.Scope;

// A guard narrows: the integration is something the user already switched on, and the guard says when it
// may actually run. It can never switch on something they left off, which is what keeps a rule from
// routing around a privacy consent they never granted.
public class ScopeRuntimeTests
{
    private sealed class Provider<T> : ISettingsProvider<T> where T : class, new()
    {
        public Provider(T value) => Value = value;

        public T Value { get; }

        public event EventHandler SettingsChanged;

        public void Save() => SettingsChanged?.Invoke(this, EventArgs.Empty);

        public void FlushPendingSave() { }

        public void Reload() { }
    }

    private sealed class World
    {
        public AvatarIdentity Identity = AvatarIdentity.Unknown;
        public bool BridgeRunning = true;
        public VrcInstance Instance = VrcInstance.None;
        public bool RadarRunning = true;
        public int Headcount;
        public string WorldName = string.Empty;
        public List<AvatarSense> Senses = new();
        public AvatarSchemaSnapshot Schema = AvatarSchemaSnapshot.Empty;

        public ScopeFactSource Source() => new(
            () => Identity,
            () => Schema,
            () => Senses,
            () => BridgeRunning,
            () => Instance,
            () => WorldName,
            () => Headcount,
            () => RadarRunning,
            () => false);
    }

    private static long _clock;

    private static (ScopeRuntime Runtime, ScopeFactSource Facts, World World, ScopeSettings Settings) Build(
        params ScopeRule[] rules)
    {
        var settings = new ScopeSettings();
        foreach (ScopeRule rule in rules)
            settings.Rules.Add(rule);

        var world = new World();
        ScopeFactSource facts = world.Source();
        _clock = 0;
        var runtime = new ScopeRuntime(new Provider<ScopeSettings>(settings), facts, () => _clock);

        return (runtime, facts, world, settings);
    }

    private static void Advance(TimeSpan by) => _clock += by.Ticks;

    private static ScopeRule Guarding(string tileKey, ScopeGroup when) =>
        ScopeRule.For("r1", "Heart rate while streaming", ScopeTarget.Integration(tileKey), when) with
        {
            Enabled = true,
            DwellMs = 0,
        };

    [Fact]
    public void An_integration_no_rule_names_is_permitted_without_reading_a_single_fact()
    {
        // The fail-safe. Somebody who never wrote a rule cannot have an integration switched off by a log
        // format change or a transport that failed to start.
        var (runtime, facts, _, _) = Build();
        facts.Refresh();
        runtime.Evaluate();

        Assert.True(runtime.PermitsIntegration("HeartRate"));
        Assert.True(runtime.PermitsSending());
    }

    [Fact]
    public void A_guard_that_matches_permits_and_one_that_does_not_blocks()
    {
        var (runtime, facts, world, _) = Build(
            Guarding("HeartRate", ScopeGroup.All(ScopePredicate.Is(ScopeFactKey.AvatarId, "avtr_one"))));

        world.Identity = new AvatarIdentity("avtr_one", "Kobold", AvatarIdSource.AvatarChange);
        facts.Refresh();
        runtime.Evaluate();
        Assert.True(runtime.PermitsIntegration("HeartRate"));

        world.Identity = new AvatarIdentity("avtr_two", "Other", AvatarIdSource.AvatarChange);
        facts.Refresh();
        runtime.Evaluate();
        Assert.False(runtime.PermitsIntegration("HeartRate"));
    }

    [Fact]
    public void The_off_half_needs_no_second_rule()
    {
        // This is the whole argument for guards over rules with effects: "when I am not wearing it, turn
        // it off" is the same guard, not an inverted twin that would then have to be arbitrated.
        var (runtime, facts, world, _) = Build(
            Guarding("HeartRate", ScopeGroup.All(ScopePredicate.Is(ScopeFactKey.AvatarId, "avtr_one"))));

        world.Identity = new AvatarIdentity("avtr_one", "Kobold", AvatarIdSource.AvatarChange);
        facts.Refresh();
        runtime.Evaluate();
        Assert.True(runtime.PermitsIntegration("HeartRate"));

        world.Identity = AvatarIdentity.Unknown;
        world.BridgeRunning = false;
        facts.Refresh();
        runtime.Evaluate();

        Assert.True(runtime.PermitsIntegration("HeartRate"));
    }

    [Fact]
    public void An_unreadable_guard_holds_the_last_decision_rather_than_flapping()
    {
        var (runtime, facts, world, _) = Build(
            Guarding("HeartRate", ScopeGroup.All(ScopePredicate.Is(ScopeFactKey.AvatarId, "avtr_two"))));

        world.Identity = new AvatarIdentity("avtr_one", "Kobold", AvatarIdSource.AvatarChange);
        facts.Refresh();
        runtime.Evaluate();
        Assert.False(runtime.PermitsIntegration("HeartRate"));

        world.BridgeRunning = false;
        facts.Refresh();
        runtime.Evaluate();

        Assert.False(runtime.PermitsIntegration("HeartRate"));
        Assert.Equal(ScopeOutcome.Unknown, runtime.Decisions.Single().Outcome);
    }

    [Fact]
    public void An_unreadable_guard_with_nothing_decided_yet_leaves_the_users_own_switch_alone()
    {
        // At startup nothing is readable. Blocking here would make a guarded integration look broken
        // rather than guarded -- and a guard only ever narrows, so "we cannot tell" is not a reason to
        // override what somebody switched on.
        var (runtime, facts, world, _) = Build(
            Guarding("HeartRate", ScopeGroup.All(ScopePredicate.Is(ScopeFactKey.AvatarId, "avtr_one"))));

        world.BridgeRunning = false;
        facts.Refresh();
        runtime.Evaluate();

        Assert.True(runtime.PermitsIntegration("HeartRate"));
    }

    [Fact]
    public void A_guard_written_for_privacy_can_ask_to_stay_shut_while_unknown()
    {
        var (runtime, facts, world, _) = Build(
            Guarding("HeartRate", ScopeGroup.All(ScopePredicate.Is(ScopeFactKey.AvatarId, "avtr_one")))
                with { BlockWhileUnknown = true });

        world.BridgeRunning = false;
        facts.Refresh();
        runtime.Evaluate();

        Assert.False(runtime.PermitsIntegration("HeartRate"));
    }

    [Fact]
    public void A_change_has_to_settle_before_it_is_acted_on()
    {
        var (runtime, facts, world, settings) = Build();
        settings.Rules.Add(
            ScopeRule.For("r1", "Streaming", ScopeTarget.Integration("HeartRate"),
                ScopeGroup.All(ScopePredicate.Is(ScopeFactKey.AvatarId, "avtr_one")))
                with { Enabled = true, DwellMs = 2000 });

        world.Identity = new AvatarIdentity("avtr_one", "Kobold", AvatarIdSource.AvatarChange);
        facts.Refresh();
        runtime.Evaluate();
        Assert.True(runtime.PermitsIntegration("HeartRate"));

        world.Identity = new AvatarIdentity("avtr_two", "Other", AvatarIdSource.AvatarChange);
        facts.Refresh();
        runtime.Evaluate();
        Assert.True(runtime.PermitsIntegration("HeartRate"));

        Advance(TimeSpan.FromSeconds(3));
        runtime.Evaluate();
        Assert.False(runtime.PermitsIntegration("HeartRate"));
    }

    [Fact]
    public void Two_guards_on_one_integration_both_narrow_it_so_the_strictest_wins()
    {
        // Nothing to arbitrate, which is why the editor never has to refuse a save.
        var (runtime, facts, world, settings) = Build();
        settings.Rules.Add(Guarding("HeartRate", ScopeGroup.All(ScopePredicate.Is(ScopeFactKey.AvatarId, "avtr_one"))));
        settings.Rules.Add(
            ScopeRule.For("r2", "Not in public", ScopeTarget.Integration("HeartRate"),
                ScopeGroup.All(ScopePredicate.IsNot(ScopeFactKey.InstanceType, "Public")))
                with { Enabled = true, DwellMs = 0 });

        world.Identity = new AvatarIdentity("avtr_one", "Kobold", AvatarIdSource.AvatarChange);
        world.Instance = new VrcInstance("wrld_a", "1", VrcInstanceAccess.Public, "eu");
        facts.Refresh();
        runtime.Evaluate();

        Assert.False(runtime.PermitsIntegration("HeartRate"));

        world.Instance = new VrcInstance("wrld_a", "1", VrcInstanceAccess.Friends, "eu");
        facts.Refresh();
        runtime.Evaluate();

        Assert.True(runtime.PermitsIntegration("HeartRate"));
    }

    [Fact]
    public void A_disabled_rule_decides_nothing()
    {
        var (runtime, facts, world, _) = Build(
            Guarding("HeartRate", ScopeGroup.All(ScopePredicate.Is(ScopeFactKey.AvatarId, "avtr_one")))
                with { Enabled = false });

        world.Identity = new AvatarIdentity("avtr_two", "Other", AvatarIdSource.AvatarChange);
        facts.Refresh();
        runtime.Evaluate();

        Assert.True(runtime.PermitsIntegration("HeartRate"));
        Assert.Empty(runtime.Decisions);
    }

    [Fact]
    public void Switching_the_whole_system_off_permits_everything()
    {
        var (runtime, facts, world, settings) = Build(
            Guarding("HeartRate", ScopeGroup.All(ScopePredicate.Is(ScopeFactKey.AvatarId, "avtr_one"))));

        world.Identity = new AvatarIdentity("avtr_two", "Other", AvatarIdSource.AvatarChange);
        facts.Refresh();
        runtime.Evaluate();
        Assert.False(runtime.PermitsIntegration("HeartRate"));

        settings.Enabled = false;
        runtime.Evaluate();

        Assert.True(runtime.PermitsIntegration("HeartRate"));
    }

    [Fact]
    public void Groups_are_resolved_from_the_worn_avatar_before_the_evaluator_runs()
    {
        var (runtime, facts, world, settings) = Build(
            Guarding("HeartRate", ScopeGroup.All(ScopePredicate.InGroup(ScopeFactKey.AvatarGroup, "streaming"))));

        settings.AvatarGroups.Add(new AvatarGroup("streaming", "Streaming", ["avtr_one", "avtr_three"]));
        runtime.SyncGroups();

        world.Identity = new AvatarIdentity("avtr_three", "Third", AvatarIdSource.AvatarChange);
        facts.Refresh();
        runtime.Evaluate();
        Assert.True(runtime.PermitsIntegration("HeartRate"));

        world.Identity = new AvatarIdentity("avtr_nine", "Ninth", AvatarIdSource.AvatarChange);
        facts.Refresh();
        runtime.Evaluate();
        Assert.False(runtime.PermitsIntegration("HeartRate"));
    }

    [Fact]
    public void A_world_group_matches_however_the_world_was_written_down()
    {
        var (runtime, facts, world, settings) = Build(
            ScopeRule.For("r1", "Quiet here", ScopeTarget.Sending,
                ScopeGroup.None(ScopePredicate.InGroup(ScopeFactKey.WorldGroup, "muted")))
                with { Enabled = true, DwellMs = 0 });

        settings.WorldGroups.Add(new WorldGroup("muted", "Muted", ["WRLD_ABC:1234~private(x)"]));
        runtime.SyncGroups();

        world.Instance = new VrcInstance("wrld_abc", "77", VrcInstanceAccess.Public, "eu");
        facts.Refresh();
        runtime.Evaluate();

        Assert.False(runtime.PermitsSending());
    }

    [Fact]
    public void The_crowd_bucket_reaches_a_guard()
    {
        var (runtime, facts, world, _) = Build(
            Guarding("HeartRate", ScopeGroup.All(ScopePredicate.Is(ScopeFactKey.InstanceCrowd, "Packed"))));

        world.Instance = new VrcInstance("wrld_a", "1", VrcInstanceAccess.Public, "eu");
        world.Headcount = 20;
        facts.Refresh();
        runtime.Evaluate();

        Assert.True(runtime.PermitsIntegration("HeartRate"));
    }

    [Fact]
    public void An_avatar_parameter_reaches_a_guard_only_while_the_avatar_declares_it()
    {
        var (runtime, facts, world, _) = Build(
            Guarding("HeartRate", ScopeGroup.All(ScopePredicate.IsOn("MCB/Cfg/HeartRate"))));

        world.Identity = new AvatarIdentity("avtr_one", "Kobold", AvatarIdSource.AvatarChange);
        world.Senses.Add(new AvatarSense("avatar.param.MCB/Cfg/HeartRate", SignalKind.Bool, 1, string.Empty, DateTime.UtcNow));
        world.Schema = new AvatarSchemaSnapshot(
            "avtr_one", 1, DateTime.UtcNow,
            [new VrcParameterDeclaration("MCB/Cfg/HeartRate", SignalKind.Bool, SignalValue.Bool(true), true)]);

        facts.Refresh();
        runtime.Evaluate();
        Assert.True(runtime.PermitsIntegration("HeartRate"));

        world.Schema = AvatarSchemaSnapshot.Empty;
        facts.Refresh();
        runtime.Evaluate();

        Assert.Equal(ScopeOutcome.Unknown, runtime.Decisions.Single().Outcome);
        Assert.True(runtime.PermitsIntegration("HeartRate"));
    }

    [Fact]
    public void The_gate_folds_the_ui_key_aliases_onto_the_tile_they_mean()
    {
        var (runtime, facts, world, _) = Build(
            Guarding("Network", ScopeGroup.All(ScopePredicate.Is(ScopeFactKey.AvatarId, "avtr_one"))));

        world.Identity = new AvatarIdentity("avtr_two", "Other", AvatarIdSource.AvatarChange);
        facts.Refresh();
        runtime.Evaluate();

        var gate = new IntegrationGate(runtime);

        Assert.False(gate.Permits("NetworkStatistics"));
        Assert.False(gate.Permits("Network"));
        Assert.True(gate.Permits("Spotify"));
    }

    [Fact]
    public void A_decision_says_which_fact_stopped_it()
    {
        var (runtime, facts, world, _) = Build(
            Guarding("HeartRate", ScopeGroup.All(ScopePredicate.Is(ScopeFactKey.InstanceType, "Public"))));

        world.RadarRunning = false;
        facts.Refresh();
        runtime.Evaluate();

        ScopeDecision decision = runtime.Decisions.Single();
        Assert.Equal(ScopeOutcome.Unknown, decision.Outcome);
        Assert.Equal(ScopeFactKey.InstanceType, decision.Block.Key);
        Assert.Equal("waiting on instance.type", ScopeMirror.Because(decision.Outcome, decision.Block, names: null));
    }
}
