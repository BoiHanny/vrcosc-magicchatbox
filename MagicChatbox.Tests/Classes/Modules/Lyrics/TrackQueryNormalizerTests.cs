using System;
using vrcosc_magicchatbox.Classes.Modules.Lyrics;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules.Lyrics;

public class TrackQueryNormalizerTests
{
    [Theory]
    [InlineData("Circles (Official Music Video)", "Circles")]
    [InlineData("Circles [Official Audio]", "Circles")]
    [InlineData("Circles (Lyrics)", "Circles")]
    [InlineData("Circles (Official Video) [4K Remaster]", "Circles")]
    [InlineData("Circles (feat. Somebody)", "Circles")]
    [InlineData("Circles - Remastered 2019", "Circles - Remastered 2019")]
    [InlineData("Circles", "Circles")]
    public void YouTubeStyleTitleNoiseIsRemoved(string raw, string expected)
    {
        Assert.Equal(expected, TrackQueryNormalizer.CleanTitle(raw));
    }

    [Fact]
    public void TopicChannelSuffixIsRemovedFromArtist()
    {
        Assert.Equal("Post Malone", TrackQueryNormalizer.CleanArtist("Post Malone - Topic"));
    }

    [Fact]
    public void FullWidthPunctuationIsNormalized()
    {
        Assert.Equal("アイドル", TrackQueryNormalizer.CleanTitle("アイドル（Official Music Video）"));
    }

    [Fact]
    public void ArtistIsSplitOutOfTheTitleWhenMissing()
    {
        var query = TrackQueryNormalizer.Normalize(
            "Post Malone - Circles (Official Music Video)", null, null, TimeSpan.FromSeconds(215));

        Assert.Equal("Post Malone", query.Artist);
        Assert.Equal("Circles", query.Title);
    }

    [Fact]
    public void AnExistingArtistIsNotOverwritten()
    {
        var query = TrackQueryNormalizer.Normalize(
            "Some - Song", "Real Artist", "Album", TimeSpan.FromSeconds(100));

        Assert.Equal("Real Artist", query.Artist);
        Assert.Equal("Some - Song", query.Title);
    }

    [Fact]
    public void TrailingSeparatorsAreTrimmed()
    {
        Assert.Equal("Circles", TrackQueryNormalizer.CleanTitle("Circles - "));
    }

    [Fact]
    public void AQueryWithoutArtistOrTitleIsNotUsable()
    {
        Assert.False(TrackQueryNormalizer.Normalize("", "", "", TimeSpan.Zero).IsUsable);
        Assert.False(TrackQueryNormalizer.Normalize("Title only", "", "", TimeSpan.Zero).IsUsable);
    }

    [Fact]
    public void CacheKeyIgnoresCaseButNotDuration()
    {
        var a = TrackQueryNormalizer.Normalize("Circles", "Post Malone", "", TimeSpan.FromSeconds(215));
        var b = TrackQueryNormalizer.Normalize("CIRCLES", "POST MALONE", "", TimeSpan.FromSeconds(215));
        var c = TrackQueryNormalizer.Normalize("Circles", "Post Malone", "", TimeSpan.FromSeconds(240));

        Assert.Equal(a.CacheKey, b.CacheKey);
        Assert.NotEqual(a.CacheKey, c.CacheKey);
    }
}
