using MagicChatbox.Vocabulary;
using MagicChatbox.Vrc;
using System;
using System.Collections.Generic;
using System.Linq;
using vrcosc_magicchatbox.Core.Vrc;
using Xunit;

namespace MagicChatbox.Tests.Core.Vrc;

// The failure this exists to name: a prefab that installs cleanly, uploads cleanly, and does nothing,
// because the installer renamed every parameter on the way in. From inside VRChat there is no symptom
// at all - the menu is there and the buttons do nothing. From outside, the renamed parameter is
// visible in the avatar's own schema, so the answer is one line instead of a support thread.
public class LayoutDoctorTests
{
    private static readonly string[] Expected = ["MCB/Ctrl/Tts/Stop", "MCB/Ctrl/Panic"];

    private static AvatarSchemaSnapshot Schema(params string[] names)
        => new(
            "avtr_test",
            1,
            DateTime.UtcNow,
            names
                .Select(n => new VrcParameterDeclaration(n, SignalKind.Bool, SignalValue.Bool(false), true))
                .ToList());

    [Fact]
    public void An_avatar_with_the_controls_installed_reports_its_version()
    {
        LayoutReport report = LayoutDoctor.Inspect(
            Schema("MCB/Version/1", "MCB/Ctrl/Tts/Stop", "MCB/Ctrl/Panic", "Toggles/Hat"),
            Expected);

        Assert.Equal(LayoutState.Installed, report.State);
        Assert.Equal(1, report.InstalledVersion);
        Assert.Empty(report.MissingControls);
    }

    [Fact]
    public void An_ordinary_avatar_is_told_it_is_normal_rather_than_broken()
    {
        LayoutReport report = LayoutDoctor.Inspect(Schema("Toggles/Hat", "Go/Locomotion"), Expected);

        Assert.Equal(LayoutState.NotInstalled, report.State);
        Assert.Contains("Most avatars do not", report.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_VRCFury_rename_is_diagnosed_with_the_name_it_actually_produced()
    {
        // Without globalParams set to MCB/*, VRCFury renames every merged parameter. The prefab then
        // installs, uploads, and does nothing, and unity/README calls this undiagnosable from inside
        // VRChat. It is entirely diagnosable from outside.
        LayoutReport report = LayoutDoctor.Inspect(
            Schema("VF12_MCB/Ctrl/Panic", "VF12_MCB/Ctrl/Tts/Stop", "Toggles/Hat"),
            Expected);

        Assert.Equal(LayoutState.RenamedByVrcFury, report.State);
        Assert.Contains("VF12_MCB/", report.Detail, StringComparison.Ordinal);
        Assert.Contains("Global Parameters", report.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Modular_Avatar_rename_is_diagnosed_and_names_the_right_checkbox()
    {
        LayoutReport report = LayoutDoctor.Inspect(
            Schema("MCB/Ctrl/Panic$$Internal_4", "Toggles/Hat"),
            Expected);

        Assert.Equal(LayoutState.RenamedByModularAvatar, report.State);
        Assert.Contains("Auto Rename", report.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_hashed_Modular_Avatar_rename_is_also_recognised()
    {
        LayoutReport report = LayoutDoctor.Inspect(
            Schema("MCB/Ctrl/Panic$351eea58e972", "Toggles/Hat"),
            Expected);

        Assert.Equal(LayoutState.RenamedByModularAvatar, report.State);
    }

    [Fact]
    public void A_version_parameter_that_survived_renaming_still_counts_as_installed()
    {
        // VRCFury renames the version parameter too, and its presence is still the handshake.
        LayoutReport report = LayoutDoctor.Inspect(
            Schema("VF12_MCB/Version/1", "VF12_MCB/Ctrl/Tts/Stop", "VF12_MCB/Ctrl/Panic"),
            Expected);

        Assert.Equal(LayoutState.Installed, report.State);
        Assert.Equal(1, report.InstalledVersion);
    }

    [Fact]
    public void A_half_installed_layout_names_what_is_missing()
    {
        LayoutReport report = LayoutDoctor.Inspect(
            Schema("MCB/Version/1", "MCB/Ctrl/Panic"),
            Expected);

        Assert.Equal(LayoutState.Installed, report.State);
        Assert.Equal(new[] { "MCB/Ctrl/Tts/Stop" }, report.MissingControls);
        Assert.Contains("missing", report.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void With_no_avatar_yet_it_says_waiting_rather_than_guessing()
    {
        LayoutReport report = LayoutDoctor.Inspect(AvatarSchemaSnapshot.Empty, Expected);

        Assert.Equal(LayoutState.Unknown, report.State);
    }

    [Fact]
    public void A_newer_contract_version_is_reported_as_the_one_that_is_there()
    {
        LayoutReport report = LayoutDoctor.Inspect(
            Schema("MCB/Version/3", "MCB/Ctrl/Tts/Stop", "MCB/Ctrl/Panic"),
            Expected);

        Assert.Equal(3, report.InstalledVersion);
    }
}
