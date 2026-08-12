using System;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules.Lyrics;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules.Lyrics;

public class LrcParserTests
{
    [Fact]
    public void ParsesTimestampsAndText()
    {
        var track = LrcParser.Parse("[00:12.34]first line\n[00:15.00]second line");

        Assert.Equal(2, track.Lines.Count);
        Assert.Equal(TimeSpan.FromMilliseconds(12340), track.Lines[0].Start);
        Assert.Equal("first line", track.Lines[0].Text);
    }

    [Theory]
    [InlineData("[00:12.3]x", 12300)]
    [InlineData("[00:12.34]x", 12340)]
    [InlineData("[00:12.345]x", 12345)]
    [InlineData("[01:02.00]x", 62000)]
    [InlineData("[00:12]x", 12000)]
    public void HandlesEveryFractionWidth(string line, int expectedMs)
    {
        var track = LrcParser.Parse(line);
        Assert.Equal(TimeSpan.FromMilliseconds(expectedMs), track.Lines[0].Start);
    }

    [Fact]
    public void RepeatedTimestampsOnOneLineBecomeSeparateEntries()
    {
        var track = LrcParser.Parse("[00:10.00][00:40.00]same words");

        Assert.Equal(2, track.Lines.Count);
        Assert.All(track.Lines, l => Assert.Equal("same words", l.Text));
    }

    [Fact]
    public void MetadataTagsAreDropped()
    {
        var track = LrcParser.Parse("[ti:Title]\n[ar:Artist]\n[00:05.00]real line");

        Assert.Single(track.Lines);
        Assert.Equal("real line", track.Lines[0].Text);
    }

    [Fact]
    public void OffsetTagIsCaptured()
    {
        var track = LrcParser.Parse("[offset:-500]\n[00:05.00]line");

        Assert.Equal(TimeSpan.FromMilliseconds(-500), track.EmbeddedOffset);
    }

    [Fact]
    public void DualLanguageBlocksKeepOnlyTheFirst()
    {
        string content =
            "[00:10.00]日本語の一行目\n" +
            "[00:20.00]日本語の二行目\n" +
            "[00:10.00]romaji line one\n" +
            "[00:20.00]romaji line two";

        var track = LrcParser.Parse(content);

        Assert.Equal(2, track.Lines.Count);
        Assert.All(track.Lines, l => Assert.DoesNotContain("romaji", l.Text));
    }

    [Fact]
    public void NetEaseCreditBlocksAreDropped()
    {
        var track = LrcParser.Parse("[00:00.00]{\"t\":0,\"c\":[{\"tx\":\"作词: someone\"}]}\n[00:05.00]real line");

        Assert.Single(track.Lines);
        Assert.Equal("real line", track.Lines[0].Text);
    }

    [Fact]
    public void ARecordThatIsMostlyStageDirectionsIsRejected()
    {
        string captions =
            "[00:01.00][Music]\n[00:04.00][Applause]\n[00:07.00][Music]\n[00:10.00]one real line";

        var track = LrcParser.Parse(captions);

        Assert.False(track.IsSynced);
    }

    [Fact]
    public void OccasionalStageDirectionsAreKept()
    {
        string content = string.Join("\n",
            Enumerable.Range(0, 10).Select(i => $"[00:{i:D2}.00]lyric line {i}")) +
            "\n[00:11.00][Chorus]";

        var track = LrcParser.Parse(content);

        Assert.True(track.IsSynced);
        Assert.Equal(11, track.Lines.Count);
    }

    [Fact]
    public void PlainTextWithoutTimestampsIsNotSynced()
    {
        Assert.False(LrcParser.Parse("just some words\nand more words").IsSynced);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyInputIsHandled(string? content)
    {
        Assert.False(LrcParser.Parse(content).IsSynced);
    }
}
