using System;
using System.Collections.Generic;
using vrcosc_magicchatbox.Classes.Modules.Lyrics;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules.Lyrics;

public class LyricSchedulerTests
{
    private static readonly TimeSpan Gap = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan Hold = TimeSpan.FromSeconds(6);

    private static LyricTrack Track(params (int Seconds, string Text)[] lines) => new()
    {
        Lines = new List<LyricLine>(Array.ConvertAll(lines,
            l => new LyricLine(TimeSpan.FromSeconds(l.Seconds), l.Text))),
    };

    private static LyricCursor Resolve(LyricTrack track, double seconds, double offsetMs = 0)
        => LyricScheduler.Resolve(
            track, TimeSpan.FromSeconds(seconds), TimeSpan.FromMilliseconds(offsetMs), Gap, Hold);

    [Fact]
    public void BeforeTheFirstLineNothingIsShown()
    {
        var cursor = Resolve(Track((10, "first")), 3);
        Assert.Equal(LyricCursorKind.BeforeFirstLine, cursor.Kind);
    }

    [Fact]
    public void PicksTheMostRecentLine()
    {
        var track = Track((0, "one"), (10, "two"), (20, "three"));

        Assert.Equal("two", Resolve(track, 15).Text);
        Assert.Equal("three", Resolve(track, 25).Text);
    }

    [Fact]
    public void ALineIsShownExactlyAtItsTimestamp()
    {
        var track = Track((0, "one"), (10, "two"));
        Assert.Equal("two", Resolve(track, 10).Text);
    }

    [Fact]
    public void OffsetShiftsWhichLineIsCurrent()
    {
        var track = Track((0, "one"), (5, "two"));

        Assert.Equal("one", Resolve(track, 4.5).Text);
        Assert.Equal("two", Resolve(track, 4.5, offsetMs: 600).Text);
    }

    [Fact]
    public void EmbeddedOffsetIsApplied()
    {
        var track = Track((0, "one"), (5, "two")) with { EmbeddedOffset = TimeSpan.FromMilliseconds(600) };

        Assert.Equal("two", Resolve(track, 4.5).Text);
    }

    [Fact]
    public void ALongSilenceAfterALineBecomesAGap()
    {
        var track = Track((0, "sing"), (30, "sing again"));

        Assert.Equal(LyricCursorKind.Line, Resolve(track, 3).Kind);
        Assert.Equal(LyricCursorKind.InstrumentalGap, Resolve(track, 20).Kind);
    }

    [Fact]
    public void AShortGapNeverBlanksTheLine()
    {
        var track = Track((0, "sing"), (5, "sing again"));

        Assert.Equal(LyricCursorKind.Line, Resolve(track, 4.5).Kind);
    }

    [Fact]
    public void TheFinalLineIsHeldRatherThanTreatedAsAGap()
    {
        var track = Track((0, "one"), (10, "last"));

        var cursor = Resolve(track, 300);
        Assert.Equal(LyricCursorKind.Line, cursor.Kind);
        Assert.Equal("last", cursor.Text);
    }

    [Fact]
    public void AnEmptyTrackResolvesToNothing()
    {
        Assert.Equal(LyricCursorKind.None, Resolve(LyricTrack.Empty, 10).Kind);
    }

    [Fact]
    public void BinarySearchAgreesWithLinearScanAcrossTheWholeTrack()
    {
        var lines = new List<LyricLine>();
        for (int i = 0; i < 200; i++)
            lines.Add(new LyricLine(TimeSpan.FromSeconds(i * 3), $"line {i}"));

        var track = new LyricTrack { Lines = lines };

        for (int probe = 0; probe < 600; probe += 7)
        {
            var at = TimeSpan.FromSeconds(probe);

            int expected = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Start <= at)
                    expected = i;
            }

            Assert.Equal(expected, LyricScheduler.FindLineIndex(track, at));
        }
    }
}
