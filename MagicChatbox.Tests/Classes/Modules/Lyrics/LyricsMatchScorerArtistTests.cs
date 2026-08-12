using System;
using vrcosc_magicchatbox.Classes.Modules.Lyrics;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules.Lyrics;

public class LyricsMatchScorerArtistTests
{
    private static LyricsQuery Query(string artist)
        => new()
        {
            Title = "Circles",
            Artist = artist,
            Duration = TimeSpan.FromSeconds(215),
        };

    private static LyricsCandidate Candidate(string artist)
        => new("Circles", artist, "Hollywood's Bleeding", 215, false, true);

    [Fact]
    public void AFeaturedArtistListStillMatchesTheMainArtist()
    {
        double score = LyricsMatchScorer.Score(Candidate("Post Malone, Swae Lee"), Query("Post Malone"));

        Assert.True(score >= LyricsMatchScorer.AcceptThreshold, $"expected a match, scored {score}");
    }

    [Fact]
    public void ArtistOrderDoesNotMatter()
    {
        double score = LyricsMatchScorer.Score(Candidate("Malone Post"), Query("Post Malone"));

        Assert.True(score >= LyricsMatchScorer.AcceptThreshold, $"expected a match, scored {score}");
    }

    [Fact]
    public void AnAmpersandCollaborationStillMatches()
    {
        double score = LyricsMatchScorer.Score(
            Candidate("Axwell & Ingrosso"),
            Query("Axwell /\\ Ingrosso"));

        Assert.True(score >= LyricsMatchScorer.AcceptThreshold, $"expected a match, scored {score}");
    }

    [Fact]
    public void AnUnrelatedArtistIsAlwaysRejectedEvenWithAPerfectTitle()
    {
        Assert.Equal(0, LyricsMatchScorer.Score(Candidate("Some Karaoke Band"), Query("Post Malone")));
        Assert.Equal(0, LyricsMatchScorer.Score(Candidate("Tribute Players"), Query("Post Malone")));
    }
}
