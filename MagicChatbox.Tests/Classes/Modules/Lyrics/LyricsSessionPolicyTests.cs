using System.Collections.Generic;
using vrcosc_magicchatbox.Classes.Modules.Lyrics;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules.Lyrics;

public class LyricsSessionPolicyTests
{
    private static LyricsSessionCandidate Session(
        int index,
        LyricsSessionPlayback playback,
        bool isActive = true,
        bool hasTitle = true)
        => new(index, playback, isActive, hasTitle);

    [Fact]
    public void A_paused_session_is_still_followed_but_reported_as_not_playing()
    {
        // This is the bug: it stays the source so the loaded lyrics survive the pause, but the caller
        // has to know not to show a line, because position is frozen and the same lyric would sit
        // there forever while also hiding the song title.
        var choice = LyricsSessionPolicy.Choose(new List<LyricsSessionCandidate>
        {
            Session(0, LyricsSessionPlayback.Paused),
        });

        Assert.True(choice.HasSession);
        Assert.Equal(0, choice.Index);
        Assert.False(choice.IsPlaying);
    }

    [Fact]
    public void Something_playing_wins_over_something_paused()
    {
        var choice = LyricsSessionPolicy.Choose(new List<LyricsSessionCandidate>
        {
            Session(0, LyricsSessionPlayback.Paused),
            Session(1, LyricsSessionPlayback.Playing, isActive: false),
        });

        Assert.Equal(1, choice.Index);
        Assert.True(choice.IsPlaying);
    }

    [Fact]
    public void A_playing_session_does_not_need_to_be_the_active_one()
    {
        var choice = LyricsSessionPolicy.Choose(new List<LyricsSessionCandidate>
        {
            Session(0, LyricsSessionPlayback.Playing, isActive: false),
        });

        Assert.Equal(0, choice.Index);
        Assert.True(choice.IsPlaying);
    }

    [Fact]
    public void A_paused_session_has_to_be_the_active_one_to_be_followed()
    {
        var choice = LyricsSessionPolicy.Choose(new List<LyricsSessionCandidate>
        {
            Session(0, LyricsSessionPlayback.Paused, isActive: false),
        });

        Assert.False(choice.HasSession);
    }

    [Fact]
    public void A_stopped_active_session_is_followed_last_and_never_counts_as_playing()
    {
        var choice = LyricsSessionPolicy.Choose(new List<LyricsSessionCandidate>
        {
            Session(0, LyricsSessionPlayback.Stopped),
        });

        Assert.True(choice.HasSession);
        Assert.False(choice.IsPlaying);
    }

    [Fact]
    public void An_active_paused_session_beats_an_active_stopped_one()
    {
        var choice = LyricsSessionPolicy.Choose(new List<LyricsSessionCandidate>
        {
            Session(0, LyricsSessionPlayback.Stopped),
            Session(1, LyricsSessionPlayback.Paused),
        });

        Assert.Equal(1, choice.Index);
    }

    [Fact]
    public void A_session_with_no_title_is_never_chosen()
    {
        // Nothing to look lyrics up for, at any playback state.
        var choice = LyricsSessionPolicy.Choose(new List<LyricsSessionCandidate>
        {
            Session(0, LyricsSessionPlayback.Playing, hasTitle: false),
            Session(1, LyricsSessionPlayback.Paused, hasTitle: false),
        });

        Assert.False(choice.HasSession);
    }

    [Fact]
    public void Nothing_at_all_is_handled_without_throwing()
    {
        Assert.False(LyricsSessionPolicy.Choose(new List<LyricsSessionCandidate>()).HasSession);
        Assert.False(LyricsSessionPolicy.Choose(null!).HasSession);
    }

    [Fact]
    public void The_first_playing_session_wins_when_several_are_playing()
    {
        var choice = LyricsSessionPolicy.Choose(new List<LyricsSessionCandidate>
        {
            Session(0, LyricsSessionPlayback.Playing),
            Session(1, LyricsSessionPlayback.Playing),
        });

        Assert.Equal(0, choice.Index);
    }
}
