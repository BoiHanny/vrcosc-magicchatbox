using vrcosc_magicchatbox.Classes.Modules.Media;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules.Media;

public class MediaTitleCleanerTests
{
    [Theory]
    [InlineData("Never Gonna Give You Up (Official Music Video)", "Never Gonna Give You Up")]
    [InlineData("Never Gonna Give You Up (Official Video)", "Never Gonna Give You Up")]
    [InlineData("Never Gonna Give You Up [Official Audio]", "Never Gonna Give You Up")]
    [InlineData("Never Gonna Give You Up (Lyrics)", "Never Gonna Give You Up")]
    [InlineData("Never Gonna Give You Up (Lyric Video)", "Never Gonna Give You Up")]
    [InlineData("Never Gonna Give You Up (Visualizer)", "Never Gonna Give You Up")]
    [InlineData("Never Gonna Give You Up (HD)", "Never Gonna Give You Up")]
    [InlineData("Never Gonna Give You Up [4K]", "Never Gonna Give You Up")]
    [InlineData("Never Gonna Give You Up (Official Video 2024)", "Never Gonna Give You Up")]
    public void Clean_removes_upload_noise(string raw, string expected)
        => Assert.Equal(expected, MediaTitleCleaner.Clean(raw, artist: null));

    [Theory]
    [InlineData("Song (Live)")]
    [InlineData("Song (Remix)")]
    [InlineData("Song (Acoustic)")]
    [InlineData("Song (Instrumental)")]
    [InlineData("Song (Radio Edit)")]
    [InlineData("Song (Extended Mix)")]
    [InlineData("Song (Slowed + Reverb)")]
    [InlineData("Song (Demo)")]
    public void Clean_keeps_anything_that_changes_what_you_hear(string raw)
        => Assert.Equal(raw, MediaTitleCleaner.Clean(raw, artist: null));

    [Fact]
    public void Clean_does_not_eat_a_bracket_that_merely_starts_with_a_noise_word()
    {
        // "video" leads the bracket but "Game Soundtrack" is real, so the whole group stays.
        Assert.Equal(
            "Main Theme (Video Game Soundtrack)",
            MediaTitleCleaner.Clean("Main Theme (Video Game Soundtrack)", artist: null));
    }

    [Theory]
    [InlineData("Rick Astley - Never Gonna Give You Up", "Rick Astley")]
    [InlineData("Rick Astley – Never Gonna Give You Up", "Rick Astley")]
    [InlineData("Rick Astley | Never Gonna Give You Up", "Rick Astley")]
    [InlineData("Rick Astley: Never Gonna Give You Up", "Rick Astley")]
    public void Clean_removes_the_artist_the_title_repeats(string raw, string artist)
        => Assert.Equal("Never Gonna Give You Up", MediaTitleCleaner.Clean(raw, artist));

    [Fact]
    public void Clean_removes_a_trailing_artist_too()
        => Assert.Equal(
            "Never Gonna Give You Up",
            MediaTitleCleaner.Clean("Never Gonna Give You Up - Rick Astley", "Rick Astley"));

    [Theory]
    [InlineData("RickAstleyVEVO")]
    [InlineData("Rick Astley - Topic")]
    [InlineData("Rick Astley Official")]
    public void Clean_sees_through_channel_name_decoration(string artist)
        => Assert.Equal(
            "Never Gonna Give You Up",
            MediaTitleCleaner.Clean("Rick Astley - Never Gonna Give You Up", artist));

    [Fact]
    public void Clean_matches_the_artist_against_one_credit_of_a_list()
        => Assert.Equal(
            "Save Your Tears",
            MediaTitleCleaner.Clean("The Weeknd - Save Your Tears", "The Weeknd, Ariana Grande"));

    [Fact]
    public void Clean_leaves_a_dash_alone_when_the_head_is_not_the_artist()
        => Assert.Equal(
            "Blinding Lights - Chapter 2",
            MediaTitleCleaner.Clean("Blinding Lights - Chapter 2", "The Weeknd"));

    [Fact]
    public void Clean_never_empties_a_title_that_is_only_the_artist()
    {
        // Nothing would be left, so the original stands rather than a blank line.
        Assert.Equal("Rick Astley", MediaTitleCleaner.Clean("Rick Astley", "Rick Astley"));
    }

    [Fact]
    public void Clean_handles_the_full_youtube_shape_in_one_go()
        => Assert.Equal(
            "Never Gonna Give You Up",
            MediaTitleCleaner.Clean(
                "Rick Astley - Never Gonna Give You Up (Official Music Video) [4K]",
                "RickAstleyVEVO"));

    [Theory]
    [InlineData("Song (feat. Someone)", "Song")]
    [InlineData("Song [ft. Someone]", "Song")]
    [InlineData("Song (featuring Someone)", "Song")]
    [InlineData("Song feat. Someone", "Song")]
    [InlineData("Song ft. Someone Else", "Song")]
    public void StripFeatured_drops_the_guest(string raw, string expected)
        => Assert.Equal(expected, MediaTitleCleaner.StripFeatured(raw));

    [Theory]
    [InlineData("Daft Punk Anthem")]
    [InlineData("Kraftwerk Live")]
    [InlineData("Shiftless")]
    public void StripFeatured_does_not_fire_on_letters_inside_a_word(string raw)
        => Assert.Equal(raw, MediaTitleCleaner.StripFeatured(raw));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Clean_handles_a_missing_title(string? raw)
        => Assert.Equal(string.Empty, MediaTitleCleaner.Clean(raw, "Someone"));
}
