using vrcosc_magicchatbox.Classes.Modules.Lyrics;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules.Lyrics;

public class LyricsSourceSelectionTests
{
    [Fact]
    public void Turning_one_source_off_leaves_the_other_alone()
    {
        // The whole point: the Media link switch must not drag Spotify with it.
        var sources = LyricsSourceSelection.Both.WithMediaLink(false);

        Assert.True(sources.Spotify);
        Assert.False(sources.MediaLink);
        Assert.True(sources.Any);
    }

    [Fact]
    public void Master_is_on_while_either_source_is_on()
    {
        Assert.True(new LyricsSourceSelection(true, false).Any);
        Assert.True(new LyricsSourceSelection(false, true).Any);
        Assert.True(LyricsSourceSelection.Both.Any);
        Assert.False(LyricsSourceSelection.None.Any);
    }

    [Fact]
    public void Settings_from_before_the_split_keep_following_both_players()
    {
        // Migration: the old settings file has the master on and no per-source flags at all, which
        // arrive at their defaults. Following both is what that single switch used to do.
        var sources = LyricsSourceSelection.Reconcile(master: true, spotify: false, mediaLink: false);

        Assert.Equal(LyricsSourceSelection.Both, sources);
    }

    [Fact]
    public void Settings_from_before_the_split_with_lyrics_off_stay_off()
    {
        var sources = LyricsSourceSelection.Reconcile(master: false, spotify: false, mediaLink: false);

        Assert.Equal(LyricsSourceSelection.None, sources);
    }

    [Fact]
    public void Master_off_silences_both_sources_whatever_they_say()
    {
        // The privacy guard and the Lyrics row express "no lyrics" through the master alone and know
        // nothing about sources, so the master has to win.
        var sources = LyricsSourceSelection.Reconcile(master: false, spotify: true, mediaLink: true);

        Assert.Equal(LyricsSourceSelection.None, sources);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void A_recorded_selection_survives_reconciling(bool spotify, bool mediaLink)
    {
        var sources = LyricsSourceSelection.Reconcile(master: true, spotify, mediaLink);

        Assert.Equal(spotify, sources.Spotify);
        Assert.Equal(mediaLink, sources.MediaLink);
    }

    [Fact]
    public void Reconciling_is_stable_when_run_again()
    {
        // It runs on every load, so a second pass must not change what the first one decided.
        foreach (var start in new[]
                 {
                     LyricsSourceSelection.None,
                     LyricsSourceSelection.Both,
                     new LyricsSourceSelection(true, false),
                     new LyricsSourceSelection(false, true),
                 })
        {
            var once = LyricsSourceSelection.Reconcile(start.Any, start.Spotify, start.MediaLink);
            var twice = LyricsSourceSelection.Reconcile(once.Any, once.Spotify, once.MediaLink);

            Assert.Equal(once, twice);
        }
    }

    [Fact]
    public void Switching_the_last_source_off_turns_lyrics_off_entirely()
    {
        var sources = new LyricsSourceSelection(true, false).WithSpotify(false);

        Assert.False(sources.Any);
        Assert.Equal(LyricsSourceSelection.None, sources);
    }
}
