using System;
using System.Globalization;
using vrcosc_magicchatbox.Classes.Modules;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules;

public class ComponentStatsStatusTests
{
    private static readonly DateTime Reading = new(2026, 8, 12, 22, 49, 0, DateTimeKind.Local);

    [Fact]
    public void Off_shows_nothing()
    {
        Assert.Equal(string.Empty, ComponentStatsStatus.Describe(ComponentStatsPhase.Off, null));
    }

    [Fact]
    public void Off_shows_nothing_even_when_a_reading_is_still_remembered()
    {
        // The stop path clears the timestamp, but the tile must not fall back to a stale reading if
        // it ever arrives here with one: switched off means nothing to report.
        Assert.Equal(string.Empty, ComponentStatsStatus.Describe(ComponentStatsPhase.Off, Reading));
    }

    [Fact]
    public void Starting_says_so_instead_of_showing_a_time()
    {
        Assert.Equal(ComponentStatsStatus.StartingText, ComponentStatsStatus.Describe(ComponentStatsPhase.Starting, null));
    }

    [Fact]
    public void Stopping_says_so()
    {
        Assert.Equal(ComponentStatsStatus.StoppingText, ComponentStatsStatus.Describe(ComponentStatsPhase.Stopping, Reading));
    }

    [Fact]
    public void Running_shows_the_reading_time()
    {
        string expected = Reading.ToString("T", CultureInfo.CurrentCulture);
        Assert.Equal(expected, ComponentStatsStatus.Describe(ComponentStatsPhase.Running, Reading));
    }

    [Fact]
    public void Running_before_the_first_reading_still_reads_as_starting()
    {
        // The gap between the sensors coming up and the first set of values landing. Showing a blank
        // or a zero time here is what made the tile look broken.
        Assert.Equal(ComponentStatsStatus.StartingText, ComponentStatsStatus.Describe(ComponentStatsPhase.Running, null));
    }

    [Fact]
    public void No_phase_ever_renders_a_default_date()
    {
        // Guards the regression this replaced: a DateTime that defaulted to "now" at construction,
        // so the tile displayed the time the app started as though the stats had just refreshed.
        foreach (ComponentStatsPhase phase in Enum.GetValues<ComponentStatsPhase>())
        {
            string text = ComponentStatsStatus.Describe(phase, null);
            Assert.DoesNotContain(default(DateTime).ToString("T", CultureInfo.CurrentCulture), text);
        }
    }
}
