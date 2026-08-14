using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.ViewModels.Sections;
using Xunit;

namespace MagicChatbox.Tests.ViewModels.Sections;

/// <summary>
/// The preview is a projection of the live settings through the module's own writer. These pin the
/// two things a projection can get wrong: dropping a setting, or inventing one.
/// </summary>
public sealed class PulsoidPreviewTests
{
    [Fact]
    public void TheSampleBeatIsShownEvenThoughNoBandIsConnected()
    {
        Assert.Contains(PulsoidPreview.SampleHeartRate.ToString(), PulsoidPreview.Render(new PulsoidModuleSettings()));
    }

    [Fact]
    public void TheOfflineCheckDoesNotBlankThePreview()
    {
        // It is on by default and the sensor is always offline while someone is in the settings.
        // Obeying it here would leave every switch below with an empty box to judge itself by.
        var settings = new PulsoidModuleSettings { EnableHeartRateOfflineCheck = true };

        Assert.NotEqual(string.Empty, PulsoidPreview.Render(settings));
    }

    [Fact]
    public void TheArrowIsShownWhenTheTrendIndicatorIsOnAndNotWhenItIsOff()
    {
        // The live indicator is empty whenever the rate is steady, which is nearly always at rest.
        var on = new PulsoidModuleSettings { ShowHeartRateTrendIndicator = true };
        var off = new PulsoidModuleSettings { ShowHeartRateTrendIndicator = false };

        Assert.Contains(on.SelectedPulsoidTrendSymbol.UpwardTrendSymbol, PulsoidPreview.Render(on));
        Assert.DoesNotContain(off.SelectedPulsoidTrendSymbol.UpwardTrendSymbol, PulsoidPreview.Render(off));
    }

    [Fact]
    public void TheChosenArrowSetIsTheOneShown()
    {
        var settings = new PulsoidModuleSettings
        {
            ShowHeartRateTrendIndicator = true,
            SelectedPulsoidTrendSymbol = new PulsoidTrendSymbolSet { UpwardTrendSymbol = "⤴️", DownwardTrendSymbol = "⤵️" },
        };

        Assert.Contains("⤴️", PulsoidPreview.Render(settings));
    }

    [Fact]
    public void TheBpmSuffixShowsUpRaisedBesideTheFullSizeNumber()
    {
        var settings = new PulsoidModuleSettings { ShowBPMSuffix = true };

        Assert.Contains("88 ᵇᵖᵐ", PulsoidPreview.Render(settings));
    }

    [Fact]
    public void TheUsersOwnWordsForALowRateReachThePreview()
    {
        var settings = new PulsoidModuleSettings
        {
            ShowTemperatureText = true,
            LowTemperatureThreshold = 200,
            LowHeartRateText = "chill",
        };

        Assert.Contains("ᶜʰⁱˡˡ", PulsoidPreview.Render(settings));
    }

    [Fact]
    public void TheTitleAndItsSeparatorAreBothHonoured()
    {
        var inline = new PulsoidModuleSettings { HeartRateTitle = true, CurrentHeartRateTitle = "Pulse" };
        var stacked = new PulsoidModuleSettings { HeartRateTitle = true, CurrentHeartRateTitle = "Pulse", SeparateTitleWithEnter = true };

        Assert.StartsWith("Pulse: ", PulsoidPreview.Render(inline));
        Assert.StartsWith("Pulse\v", PulsoidPreview.Render(stacked));
    }

    [Fact]
    public void HidingTheLiveRateLeavesTheStatsBehind()
    {
        var settings = new PulsoidModuleSettings
        {
            PulsoidStatsEnabled = true,
            HideCurrentHeartRate = true,
            ShowAverageHeartRate = true,
        };

        string line = PulsoidPreview.Render(settings);

        Assert.Contains("ᵃᵛᵍ", line);
        Assert.DoesNotContain(" 88 ", line);
    }

    [Fact]
    public void TurningEveryStatOffLeavesOnlyTheReading()
    {
        var settings = new PulsoidModuleSettings
        {
            PulsoidStatsEnabled = false,
            ShowHeartRateTrendIndicator = false,
            MagicHeartIconPrefix = false,
        };

        Assert.Equal("88", PulsoidPreview.Render(settings).Trim());
    }

    [Fact]
    public void ThereIsNoPreviewWithoutSettings()
    {
        Assert.Equal(string.Empty, PulsoidPreview.Render(null!));
    }
}
