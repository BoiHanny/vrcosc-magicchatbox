using MagicChatbox.Tests.TestDoubles;
using MagicChatbox.Vocabulary;
using System;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.Vrc;
using vrcosc_magicchatbox.ViewModels;
using Xunit;

namespace MagicChatbox.Tests.ViewModels;

// The owner: "when I switch avatars, when I have this preset ... it did not work."
//
// Four separate reasons, none of which had a test. The one he most likely hit: pressing Wear it before
// the schema harvest lands. Plan against an empty schema puts every value in NotOnThisAvatar, so the
// app told him his avatar was missing all 51 parameters when the truth is it had not looked yet - and
// the button had no guard and no disabled state, so the only recovery was to press it again.
public class AvatarPresetApplyTests
{
    private static AvatarPageViewModel Build(StubSettingsProvider<AvatarPresetSettings> presets)
        => new(
            new StubSettingsProvider<VrcBridgeSettings>(),
            new StubSettingsProvider<IntegrationSettings>(),
            presets,
            new Lazy<IModuleHost>(() => new BridgelessModuleHost()),
            new RecordingParameterSink(),
            StubConsentService.ApprovingAll());

    private static AvatarPreset Preset(string name, string avatarId, bool automatic = false)
        => new(name, avatarId, "Test avatar", DateTime.UtcNow,
            [new AvatarPresetValue("Toggles/Hat", SignalKind.Bool, 1)])
        { Automatic = automatic };

    [Fact]
    public void Applying_before_the_avatar_has_been_read_says_so_instead_of_blaming_the_avatar()
    {
        // With no bridge at all the schema is empty, which is the same state as "the harvest has not
        // landed yet". The old code reported every value as not on this avatar.
        var vm = Build(new StubSettingsProvider<AvatarPresetSettings>());

        vm.ApplyPresetCommand.Execute(Preset("Club", "avtr_a"));

        Assert.Empty(vm.PresetRefusals);
        Assert.DoesNotContain("not on this avatar", vm.PresetStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_look_is_never_saved_against_an_avatar_that_has_not_been_identified()
    {
        // PresetKey used to fall back to the display name, and AvatarIdentity's display name is the
        // literal "Unknown avatar" when there is no id. So every unidentified avatar - including every
        // local test avatar, which the resolver refuses by design - shared one bucket, and a look
        // saved there became permanently invisible the moment the real id arrived, with no way back.
        var settings = new AvatarPresetSettings();
        var vm = Build(new StubSettingsProvider<AvatarPresetSettings>(settings));

        Assert.Equal("No avatar yet", vm.AvatarName);

        vm.CapturePresetCommand.Execute(null);

        Assert.Empty(settings.Presets);
        Assert.DoesNotContain(settings.Presets, p => p.AvatarId == "Unknown avatar");
    }

    [Fact]
    public void Only_one_look_per_avatar_goes_on_by_itself()
    {
        var settings = new AvatarPresetSettings();
        settings.Presets.Add(Preset("Club", "avtr_a", automatic: true));
        settings.Presets.Add(Preset("Quiet", "avtr_a"));

        var vm = Build(new StubSettingsProvider<AvatarPresetSettings>(settings));

        vm.WearAutomaticallyCommand.Execute(settings.Presets[1]);

        Assert.Single(settings.Presets.Where(p => p.AvatarId == "avtr_a" && p.Automatic));
        Assert.Equal("Quiet", settings.Presets.Single(p => p.Automatic).Name);
    }

    [Fact]
    public void Turning_it_off_again_leaves_nothing_automatic()
    {
        var settings = new AvatarPresetSettings();
        settings.Presets.Add(Preset("Club", "avtr_a", automatic: true));

        var vm = Build(new StubSettingsProvider<AvatarPresetSettings>(settings));

        vm.WearAutomaticallyCommand.Execute(settings.Presets[0]);

        Assert.DoesNotContain(settings.Presets, p => p.Automatic);
    }

    [Fact]
    public void Marking_a_look_automatic_does_not_touch_another_avatar_s_looks()
    {
        var settings = new AvatarPresetSettings();
        settings.Presets.Add(Preset("Theirs", "avtr_other", automatic: true));
        settings.Presets.Add(Preset("Mine", "avtr_a"));

        var vm = Build(new StubSettingsProvider<AvatarPresetSettings>(settings));

        vm.WearAutomaticallyCommand.Execute(settings.Presets[1]);

        Assert.True(settings.Presets.Single(p => p.AvatarId == "avtr_other").Automatic);
        Assert.True(settings.Presets.Single(p => p.AvatarId == "avtr_a").Automatic);
    }

    [Fact]
    public void The_automatic_flag_survives_a_restart()
    {
        var settings = new AvatarPresetSettings();
        settings.Presets.Add(Preset("Club", "avtr_a", automatic: true));

        AvatarPresetSettings restored = Newtonsoft.Json.JsonConvert.DeserializeObject<AvatarPresetSettings>(
            Newtonsoft.Json.JsonConvert.SerializeObject(settings))!;

        Assert.True(restored.Presets.Single().Automatic);
    }
}
