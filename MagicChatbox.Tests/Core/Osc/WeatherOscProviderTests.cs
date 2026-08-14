using System;
using System.Collections.Generic;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Osc;
using vrcosc_magicchatbox.Core.Osc.Providers;
using vrcosc_magicchatbox.Services;
using Xunit;

namespace MagicChatbox.Tests.Core.Osc;

/// <summary>
/// The weather template is user-authored and used to arrive at whatever length it happened to be.
/// These pin that the segment now spends what is left of the line, and never more than its share.
/// </summary>
public class WeatherOscProviderTests
{
    [Fact]
    public void A_template_that_would_fill_the_line_is_cut_to_the_weather_share()
    {
        string text = Build(new string('w', 400));

        Assert.Equal(WeatherBudget.MaxSegmentLength, text.Length);
        Assert.EndsWith("…", text);
    }

    [Fact]
    public void What_is_already_on_the_line_shrinks_the_share_further()
    {
        // 100 characters plus the separator are gone before Weather is asked.
        string text = Build(new string('w', 400), new string('a', 100));

        Assert.Equal(OscBuildContext.MaxOscLength - 103, text.Length);
    }

    [Fact]
    public void The_prefix_and_suffix_come_out_of_the_weather_share_too()
    {
        // A prefix and suffix are always sent, so they are spent before Weather is offered anything.
        string text = Build(new string('w', 400), prefix: new string('p', 90), suffix: new string('s', 10));

        Assert.Equal(OscBuildContext.MaxOscLength - 100, text.Length);
    }

    [Fact]
    public void A_weather_line_that_fits_is_handed_over_untouched()
    {
        Assert.Equal("18ᶜ ᶜˡᵉᵃʳ", Build("18ᶜ ᶜˡᵉᵃʳ"));
    }

    [Fact]
    public void Weather_is_omitted_rather_than_reduced_to_a_mark_when_nothing_is_left()
    {
        Assert.Null(Segment("18ᶜ ᶜˡᵉᵃʳ", new string('a', OscBuildContext.MaxOscLength)));
    }

    [Fact]
    public void Nothing_to_report_is_still_no_segment_at_all()
    {
        Assert.Null(Segment(string.Empty));
        Assert.Null(Segment("   "));
    }

    #region Harness

    private static string Build(string weatherText, string? existingSegment = null, string prefix = "", string suffix = "")
        => Segment(weatherText, existingSegment, prefix, suffix)?.Text ?? string.Empty;

    private static OscSegment? Segment(string weatherText, string? existingSegment = null, string prefix = "", string suffix = "")
    {
        var weather = new StubWeatherService(weatherText);

        var provider = new WeatherOscProvider(
            new StubSettingsProvider<IntegrationSettings>(new IntegrationSettings()),
            new StubSettingsProvider<WeatherSettings>(weather.Settings),
            weather);

        var context = new OscBuildContext
        {
            CurrentSegments = existingSegment is null ? [] : [existingSegment],
            Separator = " ┆ ",
            Prefix = prefix,
            Suffix = suffix,
            AllowExternalRefresh = false,
        };

        return provider.TryBuild(context);
    }

    private sealed class StubWeatherService(string text) : IWeatherService
    {
        public WeatherSettings Settings { get; } = new();
        public void SaveSettings() { }
        public void TriggerRefreshIfNeeded() => throw new InvalidOperationException("A build must not reach the network.");
        public void TriggerManualRefresh() { }
        public string BuildTimeWeatherText(string timeText) => text;
        public string BuildWeatherOnlyText() => text;
        public IReadOnlyDictionary<int, string> GetDefaultConditionMap() => new Dictionary<int, string>();
        public IReadOnlyDictionary<int, string> GetDefaultConditionIconMap() => new Dictionary<int, string>();
    }

    private sealed class StubSettingsProvider<T>(T value) : ISettingsProvider<T> where T : class, new()
    {
        public T Value { get; } = value;
        public void Save() { }
        public void FlushPendingSave() { }
        public void Reload() { }
        public event EventHandler SettingsChanged { add { } remove { } }
    }

    #endregion
}
