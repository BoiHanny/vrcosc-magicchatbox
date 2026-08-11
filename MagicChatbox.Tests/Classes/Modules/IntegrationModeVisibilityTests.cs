using System.Linq;
using vrcosc_magicchatbox.Classes.Modules;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules;

/// <summary>
/// Every integration is gated by <c>master &amp;&amp; (isVR ? X_VR : X_DESKTOP)</c>. Several per-mode
/// defaults ship off, so flipping only the master toggle used to produce nothing, silently —
/// the "I enabled it and nothing showed up in VRChat" report.
/// </summary>
public class IntegrationModeVisibilityTests
{
    [Fact]
    public void EveryGateMapsToADistinctMasterProperty()
    {
        var names = IntegrationModeVisibility.Gates.Select(g => g.MasterPropertyName).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.NotEmpty(names);
    }

    // ---- The defaults that actually bite --------------------------------

    [Fact]
    public void NetworkStatistics_EnabledInVr_IsHiddenByDefault()
    {
        var settings = new IntegrationSettings { IntgrNetworkStatistics = true };

        var hidden = IntegrationModeVisibility.GetHiddenInCurrentMode(settings, isVR: true);

        Assert.Contains(hidden, h => h.DisplayName == "Network Statistics");
    }

    [Fact]
    public void ComponentStats_EnabledOnDesktop_IsHiddenByDefault()
    {
        var settings = new IntegrationSettings { IntgrComponentStats = true };

        var hidden = IntegrationModeVisibility.GetHiddenInCurrentMode(settings, isVR: false);

        Assert.Contains(hidden, h => h.DisplayName == "Component Stats");
    }

    [Fact]
    public void NothingIsReportedWhenTheMasterToggleIsOff()
    {
        var settings = new IntegrationSettings { IntgrNetworkStatistics = false };

        var hidden = IntegrationModeVisibility.GetHiddenInCurrentMode(settings, isVR: true);

        Assert.DoesNotContain(hidden, h => h.DisplayName == "Network Statistics");
    }

    // ---- Auto-enable ----------------------------------------------------

    [Fact]
    public void TryEnableCurrentMode_TurnsOnTheVrFlagAndLeavesDesktopAlone()
    {
        var settings = new IntegrationSettings
        {
            IntgrNetworkStatistics = true,
            IntgrNetworkStatistics_VR = false,
            IntgrNetworkStatistics_DESKTOP = false,
        };

        bool changed = IntegrationModeVisibility.TryEnableCurrentMode(
            settings, nameof(IntegrationSettings.IntgrNetworkStatistics), isVR: true, out string name);

        Assert.True(changed);
        Assert.Equal("Network Statistics", name);
        Assert.True(settings.IntgrNetworkStatistics_VR);
        Assert.False(settings.IntgrNetworkStatistics_DESKTOP);
    }

    [Fact]
    public void TryEnableCurrentMode_TurnsOnTheDesktopFlagAndLeavesVrAlone()
    {
        var settings = new IntegrationSettings
        {
            IntgrComponentStats = true,
            IntgrComponentStats_VR = false,
            IntgrComponentStats_DESKTOP = false,
        };

        bool changed = IntegrationModeVisibility.TryEnableCurrentMode(
            settings, nameof(IntegrationSettings.IntgrComponentStats), isVR: false, out _);

        Assert.True(changed);
        Assert.True(settings.IntgrComponentStats_DESKTOP);
        Assert.False(settings.IntgrComponentStats_VR);
    }

    [Fact]
    public void TryEnableCurrentMode_DoesNothingWhenAlreadyVisible()
    {
        var settings = new IntegrationSettings
        {
            IntgrComponentStats = true,
            IntgrComponentStats_VR = true,
        };

        bool changed = IntegrationModeVisibility.TryEnableCurrentMode(
            settings, nameof(IntegrationSettings.IntgrComponentStats), isVR: true, out _);

        Assert.False(changed);
    }

    [Fact]
    public void TryEnableCurrentMode_DoesNothingWhenTheMasterToggleIsOff()
    {
        var settings = new IntegrationSettings
        {
            IntgrComponentStats = false,
            IntgrComponentStats_DESKTOP = false,
        };

        bool changed = IntegrationModeVisibility.TryEnableCurrentMode(
            settings, nameof(IntegrationSettings.IntgrComponentStats), isVR: false, out _);

        Assert.False(changed);
        Assert.False(settings.IntgrComponentStats_DESKTOP);
    }

    [Fact]
    public void TryEnableCurrentMode_IgnoresUnknownProperties()
    {
        var settings = new IntegrationSettings();

        Assert.False(IntegrationModeVisibility.TryEnableCurrentMode(
            settings, "IntgrSomethingThatDoesNotExist", isVR: true, out _));
    }

    // ---- Tracker battery has no desktop path ----------------------------

    [Fact]
    public void TrackerBattery_OnDesktop_IsReportedAsNotFixable()
    {
        var settings = new IntegrationSettings { IntgrTrackerBattery = true };

        var hidden = IntegrationModeVisibility.GetHiddenInCurrentMode(settings, isVR: false);
        var entry = Assert.Single(hidden, h => h.DisplayName.StartsWith("Tracker Battery"));

        Assert.False(entry.CanEnableInCurrentMode);
        Assert.False(IntegrationModeVisibility.TryEnableCurrentMode(
            settings, nameof(IntegrationSettings.IntgrTrackerBattery), isVR: false, out _));
    }

    [Fact]
    public void TrackerBattery_InVr_IsNotReported()
    {
        var settings = new IntegrationSettings { IntgrTrackerBattery = true };

        var hidden = IntegrationModeVisibility.GetHiddenInCurrentMode(settings, isVR: true);

        Assert.DoesNotContain(hidden, h => h.DisplayName.StartsWith("Tracker Battery"));
    }

    // ---- Banner text ----------------------------------------------------

    [Fact]
    public void BuildWarning_IsNullWhenNothingIsHidden()
    {
        var settings = new IntegrationSettings();
        foreach (var gate in IntegrationModeVisibility.Gates)
            Assert.False(gate.IsMasterEnabled(settings) && !gate.IsVisibleIn(settings, isVR: true),
                $"{gate.DisplayName} is on-by-default but hidden in VR — the banner would show on a fresh install.");

        Assert.Null(IntegrationModeVisibility.BuildWarning(settings, isVR: true));
    }

    [Fact]
    public void BuildWarning_SuggestsTheModeSwitchWhenFixable()
    {
        var settings = new IntegrationSettings { IntgrNetworkStatistics = true };

        string? warning = IntegrationModeVisibility.BuildWarning(settings, isVR: true);

        Assert.NotNull(warning);
        Assert.Contains("Network Statistics", warning);
        Assert.Contains("VR", warning);
        Assert.Contains("Turn on", warning);
    }

    [Fact]
    public void BuildWarning_OmitsTheSuggestionWhenNothingCanBeSwitchedOn()
    {
        var settings = new IntegrationSettings
        {
            IntgrTrackerBattery = true,
            // Current Time ships enabled with its desktop flag off (see the test below), which
            // would otherwise contribute a fixable entry and bring the suggestion back.
            IntgrScanWindowTime = false,
        };

        string? warning = IntegrationModeVisibility.BuildWarning(settings, isVR: false);

        Assert.NotNull(warning);
        Assert.Contains("Tracker Battery", warning);
        Assert.DoesNotContain("Turn on", warning);
    }

    [Fact]
    public void FreshInstallOnDesktop_AlreadyHidesCurrentTime()
    {
        // Documents a real shipped default: IntgrScanWindowTime is on but IntgrCurrentTime_DESKTOP
        // is off, so a desktop user never sees the clock and is given no reason why. The banner
        // now says so instead of leaving them to guess.
        var settings = new IntegrationSettings();

        var hidden = IntegrationModeVisibility.GetHiddenInCurrentMode(settings, isVR: false);

        Assert.Contains(hidden, h => h.DisplayName == "Current Time" && h.CanEnableInCurrentMode);
    }
}
