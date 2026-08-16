using MagicChatbox.Tests.TestDoubles;
using MagicChatbox.Vocabulary;
using MagicChatbox.Vrc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Core.Vrc;
using Xunit;

namespace MagicChatbox.Tests.Core.Vrc;

// Restoring a preset writes into somebody's avatar, so the rule is that every refusal is reported by
// name and nothing is ever guessed. A near-miss written anyway is the worst outcome available here:
// it reads as the app having messed with their avatar.
public class AvatarPresetPlannerTests
{
    private static AvatarSchemaSnapshot Schema(params (string Name, SignalKind Kind, bool Writable)[] parameters)
        => new(
            "avtr_test",
            1,
            DateTime.UtcNow,
            parameters
                .Select(p => new VrcParameterDeclaration(p.Name, p.Kind, SignalValue.Bool(false), p.Writable))
                .ToList());

    private static AvatarPreset Preset(params (string Name, SignalKind Kind, double Value)[] values)
        => new(
            "Test",
            "avtr_test",
            "Test avatar",
            DateTime.UtcNow,
            values.Select(v => new AvatarPresetValue(v.Name, v.Kind, v.Value)).ToList());

    [Fact]
    public void Capture_takes_only_what_the_avatar_will_accept()
    {
        AvatarPreset preset = AvatarPresetPlanner.Capture(
            "Outfit",
            new AvatarIdentity("avtr_test", "Test avatar", AvatarIdSource.SchemaHarvest),
            Schema(
                ("Toggles/Hat", SignalKind.Bool, true),
                ("Grounded", SignalKind.Bool, false),
                ("VelocityMagnitude", SignalKind.Float, false)));

        Assert.Single(preset.Values);
        Assert.Equal("Toggles/Hat", preset.Values[0].Name);
    }

    [Fact]
    public void Capture_leaves_VRChat_s_own_parameters_alone_even_when_they_are_writable()
    {
        // VRCEmote is writable, and restoring an emote as part of an outfit would make somebody
        // perform in front of other people when they loaded a preset.
        AvatarPreset preset = AvatarPresetPlanner.Capture(
            "Outfit",
            new AvatarIdentity("avtr_test", "Test", AvatarIdSource.SchemaHarvest),
            Schema(("VRCEmote", SignalKind.Int, true), ("Toggles/Hat", SignalKind.Bool, true)));

        Assert.Single(preset.Values);
        Assert.Equal("Toggles/Hat", preset.Values[0].Name);
    }

    [Fact]
    public void A_parameter_the_new_avatar_does_not_have_is_refused_by_name()
    {
        PresetApplyPlan plan = AvatarPresetPlanner.Plan(
            Preset(("Toggles/Hat", SignalKind.Bool, 1), ("Toggles/Cape", SignalKind.Bool, 1)),
            Schema(("Toggles/Hat", SignalKind.Bool, true)));

        Assert.Equal(1, plan.Carried);
        Assert.Equal(1, plan.Refused);
        Assert.Contains(plan.Rows, r => r.Name == "Toggles/Cape" && r.Outcome == PresetOutcome.NotOnThisAvatar);
    }

    [Fact]
    public void A_name_that_means_something_different_now_is_refused_and_said_so()
    {
        // The near miss, and the single most useful line the report carries: the person's own two
        // avatars disagree about what a word means, and only they can say which is right.
        PresetApplyPlan plan = AvatarPresetPlanner.Plan(
            Preset(("Toggles/Size", SignalKind.Float, 0.5)),
            Schema(("Toggles/Size", SignalKind.Int, true)));

        Assert.Equal(0, plan.Carried);
        Assert.Contains(plan.Rows, r => r.Outcome == PresetOutcome.KindChanged);
    }

    [Fact]
    public void Nothing_is_fuzzy_matched()
    {
        // Toggles/Jacket to Clothing/Jacket is a coin flip that writes into a stranger's rig.
        PresetApplyPlan plan = AvatarPresetPlanner.Plan(
            Preset(("Toggles/Jacket", SignalKind.Bool, 1)),
            Schema(("Clothing/Jacket", SignalKind.Bool, true)));

        Assert.Equal(0, plan.Carried);
        Assert.Contains(plan.Rows, r => r.Outcome == PresetOutcome.NotOnThisAvatar);
    }

    [Fact]
    public void A_VRCFury_rebuild_that_only_changed_the_prefix_still_restores()
    {
        PresetApplyPlan plan = AvatarPresetPlanner.Plan(
            Preset(("VF12_Toggles/Hat", SignalKind.Bool, 1)),
            Schema(("VF88_Toggles/Hat", SignalKind.Bool, true)));

        Assert.Equal(1, plan.Carried);
    }

    [Fact]
    public void A_read_only_parameter_is_refused_rather_than_written_into_the_void()
    {
        PresetApplyPlan plan = AvatarPresetPlanner.Plan(
            Preset(("Toggles/Hat", SignalKind.Bool, 1)),
            Schema(("Toggles/Hat", SignalKind.Bool, false)));

        Assert.Equal(0, plan.Carried);
        Assert.Contains(plan.Rows, r => r.Outcome == PresetOutcome.NotWritable);
    }

    [Fact]
    public void The_estimate_is_honest_about_how_slow_a_big_restore_is()
    {
        // 8 sends per 50ms tick is 160 a second, so a 656 parameter avatar takes about four seconds
        // and starves heart rate while it runs. This cannot be presented as instant.
        TimeSpan estimate = AvatarPresetPlanner.Estimate(656);

        Assert.InRange(estimate.TotalSeconds, 3.5, 5.0);
    }

    [Fact]
    public void An_empty_restore_takes_no_time()
    {
        Assert.Equal(TimeSpan.Zero, AvatarPresetPlanner.Estimate(0));
    }

    [Fact]
    public async Task Applying_goes_through_the_pump_so_the_budget_still_applies()
    {
        // Never a raw burst: change detection, per-parameter spacing and the per-tick budget all have
        // to keep applying or a restore floods VRChat.
        var egress = new FakeVrcEgress();
        using var pump = new AvatarParameterPump(new AvatarParameterPumpOptions
        {
            TickInterval = TimeSpan.FromMilliseconds(10),
            DefaultMinInterval = TimeSpan.Zero,
            MaxSendsPerTick = 64,
        });
        pump.Start(egress);

        PresetApplyPlan plan = AvatarPresetPlanner.Plan(
            Preset(
                ("Toggles/Hat", SignalKind.Bool, 1),
                ("Toggles/Outfit", SignalKind.Int, 3),
                ("Toggles/Size", SignalKind.Float, 0.25)),
            Schema(
                ("Toggles/Hat", SignalKind.Bool, true),
                ("Toggles/Outfit", SignalKind.Int, true),
                ("Toggles/Size", SignalKind.Float, true)));

        int published = AvatarPresetPlanner.Publish(plan, pump);
        Assert.Equal(3, published);

        var clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < 3000 && egress.Writes.Count < 3)
            await Task.Delay(20);

        Assert.Equal(3, egress.Writes.Count);
        Assert.True(egress.LastValueOf("Toggles/Hat")!.Value.AsBool());
        Assert.Equal(3, egress.LastValueOf("Toggles/Outfit")!.Value.AsInt());

        await pump.StopAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Refused_rows_are_never_published()
    {
        var egress = new FakeVrcEgress();
        using var pump = new AvatarParameterPump();

        PresetApplyPlan plan = AvatarPresetPlanner.Plan(
            Preset(("Toggles/Missing", SignalKind.Bool, 1)),
            Schema(("Toggles/Hat", SignalKind.Bool, true)));

        Assert.Equal(0, AvatarPresetPlanner.Publish(plan, pump));
    }

    [Fact]
    public void The_summary_names_what_will_not_come_across()
    {
        PresetApplyPlan plan = AvatarPresetPlanner.Plan(
            Preset(("A", SignalKind.Bool, 1), ("B", SignalKind.Bool, 1)),
            Schema(("A", SignalKind.Bool, true)));

        Assert.Contains("not on this avatar", plan.Summary, StringComparison.OrdinalIgnoreCase);
    }
}
