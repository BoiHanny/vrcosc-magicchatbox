using MagicChatbox.Vocabulary;
using MagicChatbox.Vrc;
using System;
using System.Collections.Generic;
using System.Linq;
using vrcosc_magicchatbox.Core.Vrc;
using Xunit;

namespace MagicChatbox.Tests.Core.Vrc;

// Readiness is four questions asked in order, and the first "no" is the whole answer. The last one -
// does this avatar actually have the parameters - is the one that must never read as a fault, because
// 194 of 197 avatars fail it and none of them is broken.
public class AvatarReadinessTests
{
    private static AvatarSchemaSnapshot Schema(params (string Name, bool Writable)[] parameters)
        => new(
            "avtr_test",
            1,
            DateTime.UtcNow,
            parameters
                .Select(p => new VrcParameterDeclaration(p.Name, SignalKind.Float, SignalValue.Float(0), p.Writable))
                .ToList());

    private static ReadinessInput HeartRate(
        bool connected = true, bool live = true, bool route = true, string? fault = null)
        => new("Heart rate", connected, live, route, fault, new[] { "HR", "HRPercent", "isHRBeat" });

    [Fact]
    public void A_disconnected_source_is_the_whole_message()
    {
        ReadinessRow row = AvatarReadiness.Evaluate(HeartRate(connected: false), Schema(("HR", true)), true);

        Assert.Equal(ReadinessState.NotConnected, row.State);
    }

    [Fact]
    public void A_real_error_is_shown_verbatim_rather_than_summarised()
    {
        ReadinessRow row = AvatarReadiness.Evaluate(
            HeartRate(fault: "Your Pulsoid token expired."), Schema(("HR", true)), true);

        Assert.Equal(ReadinessState.Faulted, row.State);
        Assert.Equal("Your Pulsoid token expired.", row.Detail);
        Assert.True(row.IsFault);
    }

    [Fact]
    public void A_switched_off_route_is_not_reported_as_a_fault()
    {
        ReadinessRow row = AvatarReadiness.Evaluate(HeartRate(route: false), Schema(("HR", true)), true);

        Assert.Equal(ReadinessState.RouteOff, row.State);
        Assert.False(row.IsFault);
    }

    [Fact]
    public void A_matching_avatar_reports_how_much_of_it_is_being_driven()
    {
        ReadinessRow row = AvatarReadiness.Evaluate(
            HeartRate(), Schema(("HR", true), ("HRPercent", true)), true);

        Assert.Equal(ReadinessState.Driving, row.State);
        Assert.Equal(2, row.Matched);
        Assert.Equal(3, row.Total);
        Assert.Equal("Driving 2 of 3", row.Headline);
        Assert.True(row.IsLit);
    }

    [Fact]
    public void An_avatar_with_none_of_the_parameters_is_never_a_fault()
    {
        // This is the one that matters. Nearly every avatar lands here, and if it renders red the new
        // main page ships a permanent warning to almost everybody for something they did not do wrong.
        ReadinessRow row = AvatarReadiness.Evaluate(
            HeartRate(), Schema(("Toggles/Hat", true)), true);

        Assert.Equal(ReadinessState.Ready, row.State);
        Assert.False(row.IsFault);
        Assert.Contains("normal", row.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_avatar_wearing_someone_else_s_heart_rate_prefab_is_told_so()
    {
        // Measured on a real avatar here: HBG/ heart-rate parameters that match none of the aliases.
        // Telling somebody staring at their own heart-rate prefab that they have none destroys trust
        // in every other claim the page makes.
        ReadinessRow row = AvatarReadiness.Evaluate(
            HeartRate(),
            Schema(("HBG/Local_FullHR_Float", true), ("HBG/ManualHR_Float", true)),
            true);

        Assert.Equal(ReadinessState.FoundOtherPrefab, row.State);
        Assert.Contains("HBG/", row.Detail, StringComparison.Ordinal);
        Assert.False(row.IsFault);
    }

    [Fact]
    public void A_VRCFury_renamed_parameter_still_counts_as_a_match()
    {
        // 74 of 197 avatars carry VF<n>_ renames. Not normalising them means telling three quarters
        // of VRCFury users their working setup is missing.
        ReadinessRow row = AvatarReadiness.Evaluate(
            HeartRate(), Schema(("VF55_HR", true), ("VF55_HRPercent", true)), true);

        Assert.Equal(ReadinessState.Driving, row.State);
        Assert.Equal(2, row.Matched);
    }

    [Fact]
    public void An_unknown_avatar_waits_rather_than_claiming_anything()
    {
        ReadinessRow row = AvatarReadiness.Evaluate(HeartRate(), AvatarSchemaSnapshot.Empty, false);

        Assert.Equal(ReadinessState.Waiting, row.State);
        Assert.False(row.IsFault);
    }

    [Fact]
    public void A_read_only_declaration_does_not_count_as_drivable()
    {
        ReadinessRow row = AvatarReadiness.Evaluate(
            HeartRate(), Schema(("HR", false)), true);

        Assert.NotEqual(ReadinessState.Driving, row.State);
        Assert.Equal(0, row.Matched);
    }

    [Fact]
    public void The_first_broken_link_wins_even_when_later_ones_also_fail()
    {
        ReadinessRow row = AvatarReadiness.Evaluate(
            HeartRate(connected: false, live: false, route: false),
            AvatarSchemaSnapshot.Empty,
            false);

        Assert.Equal(ReadinessState.NotConnected, row.State);
    }
}
