using System;
using System.Collections.Generic;
using vrcosc_magicchatbox.Classes.Modules.Lyrics;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules.Lyrics;

public class LyricsSourceStatusTests
{
    private static List<LyricsSourceCandidate> None => new();

    [Fact]
    public void WithNoHostIntegrationTheUserIsToldWhichSwitchToTurnOn()
    {
        Assert.Equal(
            LyricsSourceStatus.NoHost,
            LyricsSourceStatus.Describe(mediaLinkEnabled: false, spotifyEnabled: false, None));
    }

    [Fact]
    public void SpotifyOnItsOwnReportsWaitingRatherThanNothingPlaying()
    {
        Assert.Equal(
            LyricsSourceStatus.SpotifyIdle,
            LyricsSourceStatus.Describe(mediaLinkEnabled: false, spotifyEnabled: true, None));
    }

    [Fact]
    public void MediaLinkWithNoSessionsReportsNothingPlaying()
    {
        Assert.Equal(
            LyricsSourceStatus.NothingPlaying,
            LyricsSourceStatus.Describe(mediaLinkEnabled: true, spotifyEnabled: false, None));
    }

    [Fact]
    public void KnownSessionsAreNamedWithTheirState()
    {
        string text = LyricsSourceStatus.Describe(true, false, new List<LyricsSourceCandidate>
        {
            new("burger", "Paused"),
        });

        Assert.Contains("burger (Paused)", text);
    }

    [Fact]
    public void AtMostThreeSessionsAreListed()
    {
        var many = new List<LyricsSourceCandidate>();
        for (int i = 0; i < 6; i++)
            many.Add(new LyricsSourceCandidate($"track {i}", "Paused"));

        string text = LyricsSourceStatus.Describe(true, true, many);

        Assert.Contains("track 2", text);
        Assert.DoesNotContain("track 3", text);
    }

    [Fact]
    public void AnUntitledSessionStillReadsAsSomething()
    {
        string text = LyricsSourceStatus.Describe(true, false, new List<LyricsSourceCandidate>
        {
            new("   ", "Playing"),
        });

        Assert.Contains("untitled (Playing)", text);
    }

    [Fact]
    public void ANullSessionListIsTreatedAsEmpty()
    {
        Assert.Equal(
            LyricsSourceStatus.NothingPlaying,
            LyricsSourceStatus.Describe(mediaLinkEnabled: true, spotifyEnabled: true, null));
    }
}
