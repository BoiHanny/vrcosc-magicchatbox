using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Vrc;
using Xunit;

namespace MagicChatbox.Tests.Core.Vrc;

// The avatar connection shipped off, and every avatar feature is invisible without it. Turning it on for
// everybody would open a UDP port and start advertising over mDNS on a machine whose owner never asked
// for either, so a new install gets it and somebody who already has settings gets told once instead.
public class VrcBridgeFirstRunTests
{
    [Fact]
    public void A_new_install_gets_the_connection_switched_on()
    {
        var settings = new VrcBridgeSettings();

        BridgeFirstRunOutcome outcome = VrcBridgeFirstRun.Apply(settings, hadSettingsFile: false);

        Assert.Equal(BridgeFirstRunOutcome.EnabledForNewInstall, outcome);
        Assert.True(settings.EnableBridge);
        Assert.True(settings.EnableParameterInput);
        Assert.True(settings.BridgeIntroSeen);
    }

    [Fact]
    public void Somebody_who_already_has_settings_is_asked_rather_than_switched_on()
    {
        var settings = new VrcBridgeSettings();

        BridgeFirstRunOutcome outcome = VrcBridgeFirstRun.Apply(settings, hadSettingsFile: true);

        Assert.Equal(BridgeFirstRunOutcome.NeedsIntroduction, outcome);
        Assert.False(settings.EnableBridge);
        Assert.False(settings.EnableParameterInput);
        Assert.False(settings.BridgeIntroSeen);
    }

    [Fact]
    public void The_question_is_only_asked_until_it_is_answered()
    {
        var settings = new VrcBridgeSettings { BridgeIntroSeen = true };

        BridgeFirstRunOutcome outcome = VrcBridgeFirstRun.Apply(settings, hadSettingsFile: true);

        Assert.Equal(BridgeFirstRunOutcome.Nothing, outcome);
        Assert.False(settings.EnableBridge);
    }

    [Fact]
    public void Somebody_who_already_found_the_switch_is_never_introduced_to_it()
    {
        var settings = new VrcBridgeSettings { EnableBridge = true };

        BridgeFirstRunOutcome outcome = VrcBridgeFirstRun.Apply(settings, hadSettingsFile: true);

        Assert.Equal(BridgeFirstRunOutcome.Nothing, outcome);
        Assert.True(settings.BridgeIntroSeen);
        Assert.True(settings.EnableBridge);
    }

    [Fact]
    public void Deciding_reports_without_changing_anything()
    {
        var settings = new VrcBridgeSettings();

        Assert.Equal(
            BridgeFirstRunOutcome.EnabledForNewInstall,
            VrcBridgeFirstRun.Decide(settings, hadSettingsFile: false));

        Assert.False(settings.EnableBridge);
        Assert.False(settings.BridgeIntroSeen);
    }

    [Fact]
    public void A_new_install_that_answered_already_is_left_alone()
    {
        // The bootstrapper runs this on every launch, not only the first, so a second pass must not undo
        // somebody switching the connection back off.
        var settings = new VrcBridgeSettings();
        VrcBridgeFirstRun.Apply(settings, hadSettingsFile: false);
        settings.EnableBridge = false;

        BridgeFirstRunOutcome outcome = VrcBridgeFirstRun.Apply(settings, hadSettingsFile: false);

        Assert.Equal(BridgeFirstRunOutcome.Nothing, outcome);
        Assert.False(settings.EnableBridge);
    }
}
