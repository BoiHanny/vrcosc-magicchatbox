using System.Globalization;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.ViewModels.Sections;
using Xunit;

namespace MagicChatbox.Tests.ViewModels.Sections;

public sealed class NetworkStatsPreviewTests
{
    // The module formats in the user's own culture, so the expected text is built the same way -
    // hard-coding "84.30" would pass in one country and fail in the next.
    private static string N(double value) => value.ToString("N2", CultureInfo.CurrentCulture);

    private static NetworkStatsSettings Nothing() => new()
    {
        ShowCurrentDown = false,
        ShowNetworkUtilization = false,
    };

    [Fact]
    public void WithEverythingOffThereIsNothingToShow()
    {
        Assert.Equal(string.Empty, NetworkStatsPreview.Render(Nothing()));
    }

    [Fact]
    public void TheDefaultsProduceADownloadSpeedAndAUtilisation()
    {
        string line = NetworkStatsPreview.Render(new NetworkStatsSettings());

        Assert.Contains(N(84.3), line);
        Assert.Contains(N(17.68), line);
    }

    [Fact]
    public void StyledCharactersRaiseTheLabelAndNeverTheNumber()
    {
        var settings = Nothing();
        settings.ShowCurrentDown = true;
        settings.StyledCharacters = true;

        string line = NetworkStatsPreview.Render(settings);

        Assert.Contains("ᵈᵒʷⁿ", line);
        Assert.Contains(N(84.3), line);
        Assert.DoesNotContain("⁸⁴", line);
    }

    [Fact]
    public void TurningStyledCharactersOffLeavesPlainWords()
    {
        var settings = Nothing();
        settings.ShowCurrentDown = true;
        settings.StyledCharacters = false;

        string line = NetworkStatsPreview.Render(settings);

        Assert.Contains("Down", line);
        Assert.DoesNotContain("ᵈᵒʷⁿ", line);
    }

    [Fact]
    public void ARaisedUnitStaysGluedToItsNumberWhileAPlainOneBecomesItsOwnWord()
    {
        // Worth pinning because it is the one place the styled-characters switch changes the length
        // as well as the look, and the preview's counter has to agree with it.
        var raised = new NetworkStatsSettings { StyledCharacters = true, ShowNetworkUtilization = false };
        var plain = new NetworkStatsSettings { StyledCharacters = false, ShowNetworkUtilization = false };

        Assert.Contains(N(84.3) + "ᵐᵇᵖˢ", NetworkStatsPreview.Render(raised));
        Assert.Contains(N(84.3) + " Mbps", NetworkStatsPreview.Render(plain));
    }

    [Fact]
    public void EachReadingOnlyAppearsWhenItsOwnSwitchIsOn()
    {
        var settings = Nothing();
        settings.StyledCharacters = false;
        Assert.DoesNotContain("Total", NetworkStatsPreview.Render(settings));

        settings.ShowTotalDown = true;
        Assert.Contains("Total Down", NetworkStatsPreview.Render(settings));
    }

    [Fact]
    public void LargeTotalsAreReportedInGigabytesRatherThanFourFigureMegabytes()
    {
        var settings = Nothing();
        settings.ShowTotalDown = true;
        settings.StyledCharacters = false;

        Assert.Contains(N(1.84) + " GB", NetworkStatsPreview.Render(settings));
    }

    [Fact]
    public void TheReadingsKeepTheOrderTheChatboxWouldPrintThem()
    {
        var settings = new NetworkStatsSettings
        {
            ShowCurrentDown = true,
            ShowCurrentUp = true,
            ShowNetworkUtilization = true,
            StyledCharacters = false,
        };

        string line = NetworkStatsPreview.Render(settings);

        Assert.True(line.IndexOf("Down") < line.IndexOf("Up"));
        Assert.True(line.IndexOf("Up") < line.IndexOf("Network Utilization"));
    }

    [Fact]
    public void ThereIsNoPreviewWithoutSettings()
    {
        Assert.Equal(string.Empty, NetworkStatsPreview.Render(null!));
    }
}
