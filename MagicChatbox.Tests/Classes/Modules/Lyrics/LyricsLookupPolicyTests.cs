using System;
using vrcosc_magicchatbox.Classes.Modules.Lyrics;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules.Lyrics;

public class LyricsLookupPolicyTests
{
    private static LyricsQuery Query(string title = "Circles", string artist = "Post Malone", double minutes = 3.5, string album = "")
        => new()
        {
            Title = title,
            Artist = artist,
            Album = album,
            Duration = TimeSpan.FromMinutes(minutes),
        };

    [Fact]
    public void AnOrdinarySongIsLookedUp()
    {
        Assert.True(LyricsLookupPolicy.ShouldLookUp(Query(), out _));
    }

    [Fact]
    public void APodcastNeverHitsTheNetwork()
    {
        var podcast = Query(
            title: "(NEW!) Emergency Interview Before The Arrest | Some Podcast",
            artist: "Some Channel",
            minutes: 74);

        Assert.False(LyricsLookupPolicy.ShouldLookUp(podcast, out string reason));
        Assert.NotEqual(string.Empty, reason);
    }

    [Fact]
    public void ALongUploadIsSkippedOnDurationAlone()
    {
        Assert.False(LyricsLookupPolicy.ShouldLookUp(Query(minutes: 62), out _));
    }

    [Fact]
    public void AShortClipIsSkipped()
    {
        Assert.False(LyricsLookupPolicy.ShouldLookUp(Query(minutes: 0.2), out _));
    }

    [Fact]
    public void SpokenWordMarkersAreCaughtEvenAtSongLength()
    {
        Assert.False(LyricsLookupPolicy.ShouldLookUp(Query(title: "Episode 12 - podcast", minutes: 9), out _));
        Assert.False(LyricsLookupPolicy.ShouldLookUp(Query(title: "Chapter one audiobook", minutes: 12), out _));
    }

    [Fact]
    public void ALongButRealSongIsStillAllowed()
    {
        Assert.True(LyricsLookupPolicy.ShouldLookUp(Query(title: "Stairway to Heaven", artist: "Led Zeppelin", minutes: 8), out _));
        Assert.True(LyricsLookupPolicy.ShouldLookUp(Query(title: "Echoes", artist: "Pink Floyd", minutes: 14), out _));
    }

    [Fact]
    public void AnUnknownDurationDoesNotBlockTheLookup()
    {
        var noDuration = new LyricsQuery { Title = "Circles", Artist = "Post Malone" };

        Assert.True(LyricsLookupPolicy.ShouldLookUp(noDuration, out _));
    }

    [Fact]
    public void AnUnusableQueryIsRejectedWithAReason()
    {
        var empty = new LyricsQuery { Title = "", Artist = "" };

        Assert.False(LyricsLookupPolicy.ShouldLookUp(empty, out string reason));
        Assert.NotEqual(string.Empty, reason);
    }

    [Fact]
    public void SongTitlesContainingMixOrLiveAreNotBlocked()
    {
        Assert.True(LyricsLookupPolicy.ShouldLookUp(Query(title: "Levels - Extended Mix"), out _));
        Assert.True(LyricsLookupPolicy.ShouldLookUp(Query(title: "Alive"), out _));
    }
}
