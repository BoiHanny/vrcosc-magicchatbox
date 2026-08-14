using vrcosc_magicchatbox.Classes.Modules;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules;

public sealed class PulsoidHeartRateTextTests
{
    private static PulsoidModuleSettings Settings() => new();

    private static PulsoidStatisticsResponse Stats() => new()
    {
        average_beats_per_minute = 80,
        maximum_beats_per_minute = 150,
        minimum_beats_per_minute = 55,
        calories_burned_in_kcal = 320,
        streamed_duration_in_seconds = 3725,
    };

    private static string Build(PulsoidModuleSettings settings, int heartRate = 72)
        => PulsoidModule.BuildHeartRateString(settings, heartRate, deviceOnline: true, Stats());

    [Fact]
    public void StatisticsKeepTheirNumberAndRaiseOnlyTheWord()
    {
        string text = Build(Settings());

        Assert.Contains("80 ᵃᵛᵍ", text);
        Assert.Contains("150 ᵐᵃˣ", text);
        Assert.Contains("55 ᵐⁱⁿ", text);
        Assert.DoesNotContain("⁸⁰", text);
        Assert.DoesNotContain("¹⁵⁰", text);
    }

    [Fact]
    public void TheCurrentHeartRateIsTheOnlyFullSizeNumberThatWasAlreadyRight()
    {
        Assert.Contains(" 72 ", Build(Settings()));
    }

    [Fact]
    public void TheBpmUnitIsRaisedSoTheReadingStandsOut()
    {
        var settings = Settings();
        settings.ShowBPMSuffix = true;

        string text = Build(settings);

        Assert.Contains("72 ᵇᵖᵐ", text);
        Assert.DoesNotContain("bpm", text);
    }

    [Fact]
    public void CaloriesKeepTheirNumberToo()
    {
        var settings = Settings();
        settings.ShowCalories = true;

        Assert.Contains("320 ᵏᶜᵃˡ", Build(settings));
    }

    [Fact]
    public void DurationStaysReadableInsteadOfBecomingRaisedDigitsAndApostrophes()
    {
        var settings = Settings();
        settings.ShowDuration = true;

        string text = Build(settings);

        Assert.Contains("01:02:05 ᵈᵘʳᵃᵗⁱᵒⁿ", text);
        Assert.DoesNotContain("⁰¹", text);
    }

    [Fact]
    public void TheStatisticsTimeRangeRidesAlongInTheRaisedLabel()
    {
        var settings = Settings();
        settings.ShowDuration = true;
        settings.ShowStatsTimeRange = true;

        string text = Build(settings);

        Assert.Contains("01:02:05 ᵈᵘʳᵃᵗⁱᵒⁿ ᵒᵛᵉʳ ²⁴ʰ", text);
    }

    [Fact]
    public void RaisingTheLabelsDidNotChangeWhatTheSegmentCosts()
    {
        var settings = Settings();
        settings.ShowCalories = true;
        settings.ShowDuration = true;

        // Raising buys legibility, never budget: every raised glyph has to stay one char of the
        // 144. Swapping each label back for its plain spelling must not move the total.
        string text = Build(settings);
        int rawLength = text
            .Replace("ᵃᵛᵍ", "avg")
            .Replace("ᵐᵃˣ", "max")
            .Replace("ᵐⁱⁿ", "min")
            .Replace("ᵏᶜᵃˡ", "kcal")
            .Replace("ᵈᵘʳᵃᵗⁱᵒⁿ", "duration")
            .Length;

        Assert.Equal(rawLength, text.Length);
    }

    [Fact]
    public void AnOfflineDeviceStillProducesNothing()
    {
        Assert.Equal(string.Empty, PulsoidModule.BuildHeartRateString(Settings(), 72, deviceOnline: false, Stats()));
    }

    [Fact]
    public void WithoutStatisticsOnlyTheReadingIsSent()
    {
        string text = PulsoidModule.BuildHeartRateString(Settings(), 72, deviceOnline: true, stats: null);

        Assert.Contains("72", text);
        Assert.DoesNotContain("ᵃᵛᵍ", text);
    }

    [Fact]
    public void TheTemperatureWordIsRaisedBecauseItIsALabel()
    {
        var settings = Settings();

        Assert.Contains("ˢˡᵉᵉᵖʸ", Build(settings, heartRate: 50));
        Assert.Contains("ʰᵒᵗ", Build(settings, heartRate: 120));
    }

    [Fact]
    public void TheTemperatureWordFollowsTheSettingWithoutNeedingAReconnect()
    {
        var settings = Settings();
        settings.LowHeartRateText = "chill";

        Assert.Contains("ᶜʰⁱˡˡ", Build(settings, heartRate: 50));
    }
}
