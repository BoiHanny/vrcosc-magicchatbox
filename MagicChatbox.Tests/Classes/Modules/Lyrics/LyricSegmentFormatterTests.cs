using System;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules.Lyrics;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules.Lyrics;

public class LyricSegmentFormatterTests
{
    private const string LongLine = "and I'm still not sure what I'm looking for in all of these places tonight";

    private static LyricsSettings Settings() => new() { ShowNoteIcon = false };

    private static LyricCursor Line(string text, double startSec = 0, double endSec = 6)
        => new(LyricCursorKind.Line, 0, text, TimeSpan.FromSeconds(startSec), TimeSpan.FromSeconds(endSec));

    [Fact]
    public void AShortLineIsShownWhole()
    {
        string text = LyricSegmentFormatter.Build(Line("hello world"), TimeSpan.Zero, 60, Settings());
        Assert.Equal("hello world", text);
    }

    [Fact]
    public void NothingIsShownBelowTheMinimumBudget()
    {
        var settings = Settings();
        settings.MinimumCharacters = 24;

        Assert.Equal(string.Empty, LyricSegmentFormatter.Build(Line("hello"), TimeSpan.Zero, 10, settings));
    }

    [Fact]
    public void ALongLineIsWindowedRatherThanCutOff()
    {
        string text = LyricSegmentFormatter.Build(Line(LongLine), TimeSpan.Zero, 40, Settings());

        Assert.True(text.Length <= 40, $"segment was {text.Length}: '{text}'");
        Assert.EndsWith(LyricSegmentFormatter.Ellipsis, text);
    }

    [Fact]
    public void TheWindowAdvancesAsThePositionMovesThroughTheLine()
    {
        var cursor = Line(LongLine, 0, 6);
        var settings = Settings();

        string start = LyricSegmentFormatter.Build(cursor, TimeSpan.Zero, 36, settings);
        string end = LyricSegmentFormatter.Build(cursor, TimeSpan.FromSeconds(5.5), 36, settings);

        Assert.NotEqual(start, end);
        Assert.StartsWith(LyricSegmentFormatter.Ellipsis, end);
    }

    [Fact]
    public void TheWindowIsAPureFunctionOfPosition()
    {
        var cursor = Line(LongLine, 0, 6);
        var settings = Settings();
        var at = TimeSpan.FromSeconds(2.5);

        string first = LyricSegmentFormatter.Build(cursor, at, 36, settings);
        string second = LyricSegmentFormatter.Build(cursor, at, 36, settings);
        string third = LyricSegmentFormatter.Build(cursor, at, 36, settings);

        Assert.Equal(first, second);
        Assert.Equal(second, third);
    }

    [Fact]
    public void EveryBudgetFromTheMinimumUpwardsProducesSomethingThatFits()
    {
        var cursor = Line(LongLine, 0, 6);
        var settings = Settings();

        for (int budget = settings.MinimumCharacters; budget <= 144; budget++)
        {
            for (double t = 0; t < 6; t += 0.5)
            {
                string text = LyricSegmentFormatter.Build(cursor, TimeSpan.FromSeconds(t), budget, settings);
                Assert.True(text.Length <= budget, $"budget {budget} at {t}s produced {text.Length}: '{text}'");
            }
        }
    }

    [Fact]
    public void AGapShowsTheMarkerWhenEnabled()
    {
        var gap = new LyricCursor(LyricCursorKind.InstrumentalGap, 0, string.Empty, TimeSpan.Zero, TimeSpan.FromSeconds(30));

        Assert.Equal(LyricSegmentFormatter.GapMark, LyricSegmentFormatter.Build(gap, TimeSpan.Zero, 60, Settings()));
    }

    [Fact]
    public void AGapShowsNothingWhenTheMarkerIsOff()
    {
        var settings = Settings();
        settings.ShowGapMarker = false;
        var gap = new LyricCursor(LyricCursorKind.InstrumentalGap, 0, string.Empty, TimeSpan.Zero, TimeSpan.FromSeconds(30));

        Assert.Equal(string.Empty, LyricSegmentFormatter.Build(gap, TimeSpan.Zero, 60, settings));
    }

    [Fact]
    public void NewlinesNeverReachTheChatbox()
    {
        string text = LyricSegmentFormatter.Build(Line("first\nsecond\r\nthird"), TimeSpan.Zero, 80, Settings());

        Assert.DoesNotContain("\n", text);
        Assert.DoesNotContain("\r", text);
    }

    [Fact]
    public void TheNoteIconIsPrefixedWhenEnabled()
    {
        var settings = new LyricsSettings { ShowNoteIcon = true };
        string text = LyricSegmentFormatter.Build(Line("hello world"), TimeSpan.Zero, 60, settings);

        Assert.StartsWith(LyricSegmentFormatter.GapMark, text);
    }

    [Fact]
    public void AWordLongerThanTheBudgetIsStillBroken()
    {
        string monster = new('x', 200);
        string text = LyricSegmentFormatter.Build(Line(monster), TimeSpan.Zero, 40, Settings());

        Assert.True(text.Length <= 40);
        Assert.NotEqual(string.Empty, text);
    }

    [Fact]
    public void ChunksCoverEveryWordExactlyOnce()
    {
        var chunks = LyricSegmentFormatter.Chunk(LongLine, 20);
        string rejoined = string.Join(" ", chunks);

        Assert.Equal(LongLine.Split(' ').Length, rejoined.Split(' ').Length);
    }
}
