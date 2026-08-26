using vrcosc_magicchatbox.Classes.Modules;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules;

public class IntegrationModeVisibilityLyricsTests
{
    [Fact]
    public void Lyrics_are_not_reported_as_hidden_when_no_music_source_is_on()
    {
        // Lyrics render inside a music line and produce nothing on their own, so with no source
        // switched on their mode switch changes nothing and pointing at it is wrong advice.
        var settings = new IntegrationSettings
        {
            IntgrLyrics = true,
            IntgrLyrics_VR = false,
            IntgrLyrics_DESKTOP = false,
            IntgrScanMediaLink = false,
            IntgrSpotify = false,
        };

        Assert.DoesNotContain("Lyrics", IntegrationModeVisibility.BuildWarning(settings, isVR: true) ?? string.Empty);
        Assert.DoesNotContain("Lyrics", IntegrationModeVisibility.BuildWarning(settings, isVR: false) ?? string.Empty);
    }

    [Fact]
    public void Lyrics_are_not_reported_as_hidden_when_the_source_itself_is_hidden_here()
    {
        // The source already carries its own warning; repeating it for what rides along adds noise
        // and a second switch to chase.
        var settings = new IntegrationSettings
        {
            IntgrLyrics = true,
            IntgrLyrics_VR = false,
            IntgrScanMediaLink = true,
            IntgrMediaLink_VR = false,
            IntgrSpotify = false,
        };

        Assert.DoesNotContain("Lyrics", IntegrationModeVisibility.BuildWarning(settings, isVR: true) ?? string.Empty);
    }

    [Fact]
    public void Lyrics_are_reported_as_hidden_when_the_source_is_showing_here()
    {
        // The one case worth saying out loud: music is on screen, lyrics are on, and only their own
        // switch is keeping them off.
        var settings = new IntegrationSettings
        {
            IntgrLyrics = true,
            IntgrLyrics_VR = false,
            IntgrScanMediaLink = true,
            IntgrMediaLink_VR = true,
        };

        Assert.Contains("Lyrics", IntegrationModeVisibility.BuildWarning(settings, isVR: true) ?? string.Empty);
    }

    [Fact]
    public void Spotify_counts_as_a_source_for_lyrics_too()
    {
        var settings = new IntegrationSettings
        {
            IntgrLyrics = true,
            IntgrLyrics_VR = false,
            IntgrScanMediaLink = false,
            IntgrSpotify = true,
            IntgrSpotify_VR = true,
        };

        Assert.Contains("Lyrics", IntegrationModeVisibility.BuildWarning(settings, isVR: true) ?? string.Empty);
    }
}
