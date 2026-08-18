using System;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Osc;
using vrcosc_magicchatbox.Core.Osc.Providers;
using vrcosc_magicchatbox.ViewModels.State;
using Xunit;

namespace MagicChatbox.Tests.Core.Osc;

/// <summary>
/// The clock is the cheapest thing on the line and one of the two that is almost always on, so what
/// it spends on its own label matters. These pin the value/label rule for Time.
/// </summary>
public class TimeOscProviderTests
{
    [Fact]
    public void The_prefix_is_raised_and_the_clock_is_not()
    {
        string text = Build(prefixTime: true, clock: "13:37");

        Assert.Equal("ᵐʸ ᵗⁱᵐᵉ 13:37", text);
        Assert.Contains("13:37", text);
    }

    [Fact]
    public void The_prefix_no_longer_pays_for_a_colon()
    {
        // The writer places the space, which is all the colon was doing.
        string text = Build(prefixTime: true, clock: "01:37 PM");

        Assert.Equal("My time: 01:37 PM".Length - 1, text.Length);
        Assert.DoesNotContain("My time", text);
        Assert.DoesNotContain("˸", text);
    }

    [Fact]
    public void The_zone_the_formatter_appended_still_arrives_whole()
    {
        Assert.Equal("ᵐʸ ᵗⁱᵐᵉ 13:37 (CEST+2)", Build(prefixTime: true, clock: "13:37 (CEST+2)"));
    }

    [Fact]
    public void Without_the_prefix_the_segment_is_the_clock_and_nothing_else()
    {
        Assert.Equal("13:37", Build(prefixTime: false, clock: "13:37"));
    }

    [Fact]
    public void A_clock_that_has_not_been_formatted_yet_sends_no_label_on_its_own()
    {
        Assert.Null(Segment(prefixTime: true, clock: string.Empty));
        Assert.Null(Segment(prefixTime: false, clock: string.Empty));
    }

    [Fact]
    public void The_integration_switch_still_wins()
    {
        var provider = new TimeOscProvider(
            new StubSettingsProvider<IntegrationSettings>(new IntegrationSettings { IntgrScanWindowTime = false }),
            new IntegrationDisplayState { CurrentTime = "13:37" },
            new StubSettingsProvider<TimeSettings>(new TimeSettings()));

        Assert.Null(provider.TryBuild(Context()));
    }

    #region Harness

    private static string Build(bool prefixTime, string clock)
        => Segment(prefixTime, clock)?.Text ?? string.Empty;

    private static OscSegment? Segment(bool prefixTime, string clock)
    {
        var provider = new TimeOscProvider(
            new StubSettingsProvider<IntegrationSettings>(new IntegrationSettings { IntgrScanWindowTime = true }),
            new IntegrationDisplayState { CurrentTime = clock },
            new StubSettingsProvider<TimeSettings>(new TimeSettings { PrefixTime = prefixTime }));

        return provider.TryBuild(Context());
    }

    private static OscBuildContext Context() => new()
    {
        Separator = " ┆ ",
        Prefix = string.Empty,
        Suffix = string.Empty,
    };

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
