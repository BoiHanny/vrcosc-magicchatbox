using MagicChatbox.Vocabulary;
using MagicChatbox.Vrc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using vrcosc_magicchatbox.Core.Vrc;
using Xunit;

namespace MagicChatbox.Tests.Core.Vrc;

// App settings carried on the avatar. The safety rule is that a parameter riding on somebody's
// avatar may switch a feature OFF and may never switch one ON, so a world, a badly built prefab or a
// stale saved value can take capability away but never grant it.
//
// Whether VRChat actually persists an OSC-written value to a saved parameter is still unmeasured, so
// this ships switched off. Both answers leave this code correct: it either has values to read or it
// reports NotOnThisAvatar.
public class AvatarConfigSeederTests
{
    private static AvatarSchemaSnapshot Schema(string avatarId, params (string Name, bool Value)[] parameters)
        => new(
            avatarId, 1, DateTime.UtcNow,
            parameters
                .Select(p => new VrcParameterDeclaration(p.Name, SignalKind.Bool, SignalValue.Bool(p.Value), true))
                .ToList());

    private static AvatarConfigBinding Binding(
        string name, List<(string Name, bool Value)> applied, ConfigDirection direction = ConfigDirection.OffOnly)
        => new(name, "test", direction, v => applied.Add((name, v)));

    private static readonly TimeSpan Instant = TimeSpan.Zero;

    [Fact]
    public void Nothing_happens_while_the_feature_is_switched_off()
    {
        var applied = new List<(string, bool)>();
        var seeder = new AvatarConfigSeeder(
            new[] { Binding("MCB/Cfg/Quiet", applied) }, () => false, Instant);

        seeder.Seed(Schema("avtr_a", ("MCB/Cfg/Quiet", false)));

        Assert.Empty(applied);
    }

    [Fact]
    public void A_config_parameter_may_switch_something_off()
    {
        var applied = new List<(string Name, bool Value)>();
        var seeder = new AvatarConfigSeeder(
            new[] { Binding("MCB/Cfg/Quiet", applied) }, () => true, Instant);

        IReadOnlyList<ConfigSeedRow> rows = seeder.Seed(Schema("avtr_a", ("MCB/Cfg/Quiet", false)));

        Assert.Contains(rows, r => r.Outcome == ConfigSeedOutcome.Applied);
        Assert.Single(applied);
        Assert.False(applied[0].Value);
    }

    [Fact]
    public void A_config_parameter_may_never_switch_something_on()
    {
        // The rule that makes this safe to carry on an avatar at all.
        var applied = new List<(string, bool)>();
        var seeder = new AvatarConfigSeeder(
            new[] { Binding("MCB/Cfg/Quiet", applied) }, () => true, Instant);

        IReadOnlyList<ConfigSeedRow> rows = seeder.Seed(Schema("avtr_a", ("MCB/Cfg/Quiet", true)));

        Assert.Contains(rows, r => r.Outcome == ConfigSeedOutcome.RefusedTurningOn);
        Assert.Empty(applied);
    }

    [Fact]
    public void A_binding_marked_both_ways_may_turn_something_on()
    {
        var applied = new List<(string Name, bool Value)>();
        var seeder = new AvatarConfigSeeder(
            new[] { Binding("MCB/Cfg/Loud", applied, ConfigDirection.Both) }, () => true, Instant);

        seeder.Seed(Schema("avtr_a", ("MCB/Cfg/Loud", true)));

        Assert.Single(applied);
        Assert.True(applied[0].Value);
    }

    [Fact]
    public void An_avatar_without_the_parameter_is_reported_rather_than_defaulted()
    {
        var applied = new List<(string, bool)>();
        var seeder = new AvatarConfigSeeder(
            new[] { Binding("MCB/Cfg/Quiet", applied) }, () => true, Instant);

        IReadOnlyList<ConfigSeedRow> rows = seeder.Seed(Schema("avtr_a", ("Toggles/Hat", true)));

        Assert.Contains(rows, r => r.Outcome == ConfigSeedOutcome.NotOnThisAvatar);
        Assert.Empty(applied);
    }

    [Fact]
    public void A_freshly_seen_avatar_is_left_alone_until_its_values_settle()
    {
        // VRChat streams defaults the instant an avatar loads, and acting on the first thing seen
        // means acting on a default rather than on what the user saved.
        var applied = new List<(string, bool)>();
        var seeder = new AvatarConfigSeeder(
            new[] { Binding("MCB/Cfg/Quiet", applied) }, () => true, TimeSpan.FromSeconds(30));

        IReadOnlyList<ConfigSeedRow> rows = seeder.Seed(Schema("avtr_a", ("MCB/Cfg/Quiet", false)));

        Assert.Contains(rows, r => r.Outcome == ConfigSeedOutcome.NotStableYet);
        Assert.Empty(applied);
    }

    [Fact]
    public void The_same_value_is_not_applied_twice()
    {
        var applied = new List<(string, bool)>();
        var seeder = new AvatarConfigSeeder(
            new[] { Binding("MCB/Cfg/Quiet", applied) }, () => true, Instant);

        AvatarSchemaSnapshot schema = Schema("avtr_a", ("MCB/Cfg/Quiet", false));

        seeder.Seed(schema);
        IReadOnlyList<ConfigSeedRow> second = seeder.Seed(schema);

        Assert.Single(applied);
        Assert.Contains(second, r => r.Outcome == ConfigSeedOutcome.Unchanged);
    }

    [Fact]
    public void Changing_avatar_lets_the_new_one_speak_for_itself()
    {
        var applied = new List<(string Name, bool Value)>();
        var seeder = new AvatarConfigSeeder(
            new[] { Binding("MCB/Cfg/Quiet", applied) }, () => true, Instant);

        seeder.Seed(Schema("avtr_a", ("MCB/Cfg/Quiet", false)));
        seeder.Seed(Schema("avtr_b", ("MCB/Cfg/Quiet", false)));

        Assert.Equal(2, applied.Count);
    }

    [Fact]
    public void A_VRCFury_renamed_config_parameter_is_still_found()
    {
        var applied = new List<(string, bool)>();
        var seeder = new AvatarConfigSeeder(
            new[] { Binding("MCB/Cfg/Quiet", applied) }, () => true, Instant);

        seeder.Seed(Schema("avtr_a", ("VF7_MCB/Cfg/Quiet", false)));

        Assert.Single(applied);
    }

    [Fact]
    public void A_binding_that_throws_does_not_stop_the_next_one()
    {
        var applied = new List<(string Name, bool Value)>();

        var bindings = new[]
        {
            new AvatarConfigBinding("MCB/Cfg/Bad", "test", ConfigDirection.OffOnly,
                _ => throw new InvalidOperationException("boom")),
            Binding("MCB/Cfg/Good", applied),
        };

        var seeder = new AvatarConfigSeeder(bindings, () => true, Instant);

        seeder.Seed(Schema("avtr_a", ("MCB/Cfg/Bad", false), ("MCB/Cfg/Good", false)));

        Assert.Single(applied);
        Assert.Equal("MCB/Cfg/Good", applied[0].Name);
    }

    [Fact]
    public void A_throwing_gate_is_treated_as_switched_off()
    {
        var applied = new List<(string, bool)>();
        var seeder = new AvatarConfigSeeder(
            new[] { Binding("MCB/Cfg/Quiet", applied) },
            () => throw new InvalidOperationException("settings gone"),
            Instant);

        seeder.Seed(Schema("avtr_a", ("MCB/Cfg/Quiet", false)));

        Assert.Empty(applied);
    }

    [Fact]
    public void Config_parameters_live_under_their_own_prefix()
    {
        var applied = new List<(string, bool)>();

        Assert.True(Binding("MCB/Cfg/Quiet", applied).IsOwned);
        Assert.False(Binding("Toggles/Hat", applied).IsOwned);
    }
}
