using System;
using System.Collections.Generic;
using vrcosc_magicchatbox.Classes.Modules.Lyrics;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules.Lyrics;

public class LyricsMatchScorerTests
{
    private static LyricsQuery Query(
        string title = "Circles",
        string artist = "Post Malone",
        double seconds = 215)
        => new()
        {
            Title = title,
            Artist = artist,
            Duration = TimeSpan.FromSeconds(seconds),
        };

    private static LyricsCandidate Candidate(
        string title = "Circles",
        string artist = "Post Malone",
        double seconds = 215,
        bool instrumental = false,
        bool synced = true)
        => new(title, artist, "Hollywood's Bleeding", seconds, instrumental, synced);

    [Fact]
    public void AnExactMatchScoresNearPerfect()
    {
        Assert.True(LyricsMatchScorer.Score(Candidate(), Query()) > 0.95);
    }

    [Fact]
    public void ADifferentSongWithACoincidentallySimilarDurationIsRejected()
    {
        var wrongSong = Candidate(title: "Rockstar", artist: "Nickelback", seconds: 215);

        Assert.Equal(0, LyricsMatchScorer.Score(wrongSong, Query()));
    }

    [Fact]
    public void TheRightSongByTheWrongArtistIsRejected()
    {
        var cover = Candidate(title: "Circles", artist: "Some Karaoke Band", seconds: 215);

        Assert.Equal(0, LyricsMatchScorer.Score(cover, Query()));
    }

    [Fact]
    public void InstrumentalRecordsAreNeverChosen()
    {
        Assert.Equal(0, LyricsMatchScorer.Score(Candidate(instrumental: true), Query()));
    }

    [Fact]
    public void RecordsWithoutSyncedLyricsAreNeverChosen()
    {
        Assert.Equal(0, LyricsMatchScorer.Score(Candidate(synced: false), Query()));
    }

    [Fact]
    public void AWildlyDifferentDurationIsRejectedEvenWithPerfectText()
    {
        Assert.Equal(0, LyricsMatchScorer.Score(Candidate(seconds: 400), Query()));
    }

    [Fact]
    public void ASlightlyOffDurationStillMatches()
    {
        Assert.True(LyricsMatchScorer.Score(Candidate(seconds: 219), Query()) >= LyricsMatchScorer.AcceptThreshold);
    }

    [Fact]
    public void TheClosestDurationWinsAmongOtherwiseEqualCandidates()
    {
        var candidates = new List<LyricsCandidate>
        {
            Candidate(seconds: 224),
            Candidate(seconds: 215),
            Candidate(seconds: 219),
        };

        Assert.Equal(1, LyricsMatchScorer.PickBest(candidates, Query()).Index);
    }

    [Fact]
    public void NothingIsPickedWhenEverythingIsWrong()
    {
        var candidates = new List<LyricsCandidate>
        {
            Candidate(title: "Something Else", artist: "Nobody"),
            Candidate(instrumental: true),
        };

        Assert.Equal(-1, LyricsMatchScorer.PickBest(candidates, Query()).Index);
    }

    [Fact]
    public void AnEmptyResultSetPicksNothing()
    {
        Assert.Equal(-1, LyricsMatchScorer.PickBest(new List<LyricsCandidate>(), Query()).Index);
        Assert.Equal(-1, LyricsMatchScorer.PickBest(null!, Query()).Index);
    }

    [Theory]
    [InlineData("Post Malone", "post malone", 1.0)]
    [InlineData("POST MALONE", "Post  Malone!", 1.0)]
    [InlineData("Beyoncé", "Beyonce", 1.0)]
    [InlineData("Måneskin", "Maneskin", 1.0)]
    public void NormalizationMakesCosmeticDifferencesIrrelevant(string a, string b, double expected)
    {
        Assert.Equal(expected, LyricsMatchScorer.Similarity(a, b), 3);
    }

    [Fact]
    public void ExtraTrailingWordsStillMatchStrongly()
    {
        double score = LyricsMatchScorer.Similarity("Circles", "Circles - Remastered");
        Assert.True(score > 0.5, $"expected a strong partial match, got {score}");
    }

    [Fact]
    public void CompletelyDifferentTextScoresZero()
    {
        Assert.Equal(0, LyricsMatchScorer.Similarity("Circles", "Bohemian Rhapsody"));
    }

    [Fact]
    public void EmptyTextNeverMatches()
    {
        Assert.Equal(0, LyricsMatchScorer.Similarity("", "Circles"));
        Assert.Equal(0, LyricsMatchScorer.Similarity(null, "Circles"));
    }

    [Fact]
    public void WithNoDurationKnownAGoodTextMatchStillWins()
    {
        var query = Query(seconds: 0);

        Assert.True(LyricsMatchScorer.Score(Candidate(seconds: 215), query) >= LyricsMatchScorer.AcceptThreshold);
    }
}
