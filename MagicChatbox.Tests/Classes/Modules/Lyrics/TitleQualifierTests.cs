using vrcosc_magicchatbox.Classes.Modules.Lyrics;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules.Lyrics;

public class TitleQualifierTests
{
    // No list of words is involved: whatever trails the song name is the version, whether that is a
    // remix, an edit, a live take, a remaster or a guest credit.
    [Theory]
    [InlineData("Song (Some Remix)", "Song", "Some Remix")]
    [InlineData("Song - Some Remix", "Song", "Some Remix")]
    [InlineData("Song [Some Remix]", "Song", "Some Remix")]
    [InlineData("Song - Remastered", "Song", "Remastered")]
    [InlineData("Song (Live at Somewhere)", "Song", "Live at Somewhere")]
    [InlineData("Song (feat. Someone)", "Song", "feat. Someone")]
    [InlineData("Song (Radio Edit)", "Song", "Radio Edit")]
    [InlineData("Song", "Song", "")]
    [InlineData("", "", "")]
    public void Split_separates_the_song_from_the_version(string title, string expectedBase, string expectedQualifier)
    {
        var (actualBase, actualQualifier) = TitleQualifier.Split(title);

        Assert.Equal(expectedBase, actualBase);
        Assert.Equal(expectedQualifier, actualQualifier);
    }

    [Fact]
    public void Split_peels_every_trailing_bracket()
    {
        var (song, qualifier) = TitleQualifier.Split("Song (Live) (Remastered 2011)");

        Assert.Equal("Song", song);
        Assert.Equal("Live Remastered 2011", qualifier);
    }

    [Fact]
    public void A_title_that_is_nothing_but_a_bracket_keeps_it()
    {
        // Stripping would leave no song to search for at all.
        var (song, qualifier) = TitleQualifier.Split("(Untitled)");

        Assert.Equal("(Untitled)", song);
        Assert.Equal("", qualifier);
    }

    [Theory]
    [InlineData("Lead, Second", "Lead")]
    [InlineData("Lead, Second & Third", "Lead")]
    [InlineData("Lead & Second", "Lead")]
    [InlineData("Solo Artist", "Solo Artist")]
    [InlineData("", "")]
    public void PrimaryArtist_takes_the_lead_name(string artist, string expected)
        => Assert.Equal(expected, TitleQualifier.PrimaryArtist(artist));
}
