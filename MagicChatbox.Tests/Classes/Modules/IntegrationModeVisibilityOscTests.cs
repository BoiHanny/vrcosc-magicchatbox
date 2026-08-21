using vrcosc_magicchatbox.Classes.Modules;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules;

public class IntegrationModeVisibilityOscTests
{
    [Fact]
    public void Heart_rate_sending_only_over_OSC_is_not_reported_as_hidden()
    {
        // Driving an avatar parameter with no chatbox text is a legitimate setup, not a
        // misconfiguration. Telling that person to turn on a mode switch is wrong advice.
        var settings = new IntegrationSettings
        {
            IntgrHeartRate = true,
            IntgrHeartRate_OSC = true,
            IntgrHeartRate_DESKTOP = false,
            IntgrHeartRate_VR = false,
        };

        // BuildWarning covers every gate at once and a default IntegrationSettings has other
        // integrations switched on, so assert on Heart Rate's absence rather than on an empty
        // warning.
        Assert.DoesNotContain(
            "Heart Rate",
            IntegrationModeVisibility.BuildWarning(settings, isVR: false) ?? string.Empty);
        Assert.DoesNotContain(
            "Heart Rate",
            IntegrationModeVisibility.BuildWarning(settings, isVR: true) ?? string.Empty);
    }

    [Fact]
    public void Heart_rate_with_no_output_at_all_is_still_reported()
    {
        var settings = new IntegrationSettings
        {
            IntgrHeartRate = true,
            IntgrHeartRate_OSC = false,
            IntgrHeartRate_DESKTOP = false,
            IntgrHeartRate_VR = false,
        };

        string? warning = IntegrationModeVisibility.BuildWarning(settings, isVR: false);

        Assert.NotNull(warning);
        Assert.Contains("Heart Rate", warning);
    }

    [Fact]
    public void Voicemod_with_both_mode_chips_off_is_not_reported_as_hidden()
    {
        // Its controls are the point; the chips only gate the chatbox announcement.
        var settings = new IntegrationSettings
        {
            IntgrVoicemod = true,
            IntgrVoicemod_DESKTOP = false,
            IntgrVoicemod_VR = false,
        };

        Assert.DoesNotContain(
            "Voicemod",
            IntegrationModeVisibility.BuildWarning(settings, isVR: false) ?? string.Empty);
        Assert.DoesNotContain(
            "Voicemod",
            IntegrationModeVisibility.BuildWarning(settings, isVR: true) ?? string.Empty);
    }

    [Fact]
    public void An_integration_with_no_other_output_path_is_unaffected()
    {
        var settings = new IntegrationSettings
        {
            IntgrScanWindowTime = true,
            IntgrCurrentTime_DESKTOP = false,
            IntgrCurrentTime_VR = false,
        };

        string? warning = IntegrationModeVisibility.BuildWarning(settings, isVR: false);

        Assert.NotNull(warning);
        Assert.Contains("Current Time", warning);
    }

    [Fact]
    public void The_OSC_only_case_does_not_claim_a_mode_switch_can_be_turned_on()
    {
        var settings = new IntegrationSettings
        {
            IntgrHeartRate = true,
            IntgrHeartRate_OSC = true,
            IntgrHeartRate_DESKTOP = false,
            IntgrHeartRate_VR = false,
        };

        Assert.False(IntegrationModeVisibility.TryDescribeHiddenMode(
            settings,
            nameof(IntegrationSettings.IntgrHeartRate),
            isVR: false,
            out _));
    }
}
