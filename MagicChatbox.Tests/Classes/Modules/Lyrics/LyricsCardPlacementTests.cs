using vrcosc_magicchatbox.Classes.Modules.Lyrics;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules.Lyrics;

public class LyricsCardPlacementTests
{
    private static LyricsCardPlacement Resolve(
        bool lyrics = true,
        bool spotifySource = false,
        bool mediaSource = false,
        bool mediaLink = true,
        bool spotify = true)
        => LyricsCardPlacement.Resolve(lyrics, spotifySource, mediaSource, mediaLink, spotify);

    [Fact]
    public void WithLyricsOffNothingIsShownAnywhere()
    {
        Assert.Equal(LyricsCardPlacement.Nowhere, Resolve(lyrics: false, spotifySource: true));
    }

    [Fact]
    public void SpotifyDrivingPutsTheRibbonOnTheSpotifyCardOnly()
    {
        var placement = Resolve(spotifySource: true);

        Assert.True(placement.OnSpotifyCard);
        Assert.False(placement.OnMediaLinkCard);
    }

    [Fact]
    public void WindowsMediaDrivingPutsTheRibbonOnTheMediaLinkCardOnly()
    {
        var placement = Resolve(mediaSource: true);

        Assert.True(placement.OnMediaLinkCard);
        Assert.False(placement.OnSpotifyCard);
    }

    [Fact]
    public void TheRibbonNeverAppearsOnBothCardsAtOnce()
    {
        foreach (bool media in new[] { false, true })
        foreach (bool spotify in new[] { false, true })
        foreach (bool spotifySource in new[] { false, true })
        {
            var placement = Resolve(
                spotifySource: spotifySource,
                mediaSource: !spotifySource,
                mediaLink: media,
                spotify: spotify);

            Assert.False(placement.OnMediaLinkCard && placement.OnSpotifyCard);
        }
    }

    [Fact]
    public void WithNothingPlayingTheExplanationLandsOnMediaLinkWhenItIsOn()
    {
        var placement = Resolve(mediaLink: true, spotify: true);

        Assert.True(placement.OnMediaLinkCard);
        Assert.False(placement.OnSpotifyCard);
    }

    [Fact]
    public void WithOnlySpotifyOnTheExplanationLandsOnTheSpotifyCard()
    {
        var placement = Resolve(mediaLink: false, spotify: true);

        Assert.True(placement.OnSpotifyCard);
        Assert.False(placement.OnMediaLinkCard);
    }

    [Fact]
    public void WithNoHostAtAllTheExplanationStillHasSomewhereToLand()
    {
        var placement = Resolve(mediaLink: false, spotify: false);

        Assert.True(placement.OnMediaLinkCard || placement.OnSpotifyCard);
    }
}
