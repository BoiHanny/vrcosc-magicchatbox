using vrcosc_magicchatbox.Services;
using Xunit;

namespace MagicChatbox.Tests.Services;

public sealed class SoundpadStatusParserTests
{
    [Theory]
    [InlineData("R-200", true)]
    [InlineData("R-200 additional info", true)]
    [InlineData("R-404", false)]
    [InlineData("PLAYING", false)]
    [InlineData(null, false)]
    public void IsSuccessResponse_DetectsAcceptedCommands(string? response, bool expected)
    {
        Assert.Equal(expected, SoundpadStatusParser.IsSuccessResponse(response));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("R-404", true)]
    [InlineData("R-200", true)]
    [InlineData("PLAYING", false)]
    [InlineData("Soundpad - airhorn.mp3", false)]
    [InlineData("42", false)]
    public void IsErrorResponse_DetectsQueryErrors(string? response, bool expected)
    {
        Assert.Equal(expected, SoundpadStatusParser.IsErrorResponse(response));
    }

    [Theory]
    [InlineData("PLAYING", SoundpadPlayStatus.Playing)]
    [InlineData("PAUSED", SoundpadPlayStatus.Paused)]
    [InlineData("SEEKING", SoundpadPlayStatus.Seeking)]
    [InlineData("STOPPED", SoundpadPlayStatus.Stopped)]
    [InlineData(" playing ", SoundpadPlayStatus.Playing)]
    public void ParsePlayStatus_MapsKnownStates(string response, SoundpadPlayStatus expected)
    {
        Assert.Equal(expected, SoundpadStatusParser.ParsePlayStatus(response));
    }

    [Fact]
    public void ParsePlayStatus_NoResponse_IsUnknown_SoCallersCanFallBack()
    {
        Assert.Equal(SoundpadPlayStatus.Unknown, SoundpadStatusParser.ParsePlayStatus(null));
        Assert.Equal(SoundpadPlayStatus.Unknown, SoundpadStatusParser.ParsePlayStatus("R-404"));
    }

    [Fact]
    public void ParsePlayStatus_UnparseablePayload_MapsToStopped_LikeOfficialClient()
    {
        Assert.Equal(SoundpadPlayStatus.Stopped, SoundpadStatusParser.ParsePlayStatus("SOMETHING_NEW"));
    }

    [Theory]
    [InlineData("Soundpad - airhorn.mp3", "airhorn.mp3")]
    [InlineData("Soundpad", "")]
    [InlineData("Soundpad - ", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("R-404: Command not found.", "")]
    [InlineData("Soundpad - my sound [0:12]", "my sound")]
    // Live-verified paused format on Soundpad 4.0.30: marker precedes "Soundpad".
    [InlineData(" II  Soundpad - my sound", "my sound")]
    [InlineData(" II  Soundpad", "")]
    public void ParseNowPlayingTitle_ExtractsSoundName(string? title, string expected)
    {
        Assert.Equal(expected, SoundpadStatusParser.ParseNowPlayingTitle(title));
    }

    [Fact]
    public void ParseNowPlayingTitle_TitleWithoutSoundpadPrefix_IsReturnedAsIs()
    {
        Assert.Equal("Soundpadder - foo", SoundpadStatusParser.ParseNowPlayingTitle("Soundpadder - foo"));
    }

    [Theory]
    [InlineData("Soundpad - Rocky II theme", "Rocky II theme")]
    [InlineData("Soundpad - Rocky II", "Rocky II")]
    [InlineData(" II  Soundpad - Rocky II", "Rocky II")]
    [InlineData("Soundpad - My - Song.mp3", "My - Song.mp3")]
    public void ParseNowPlayingTitle_NeverStripsMarkerLikeTextFromSoundNames(string title, string expected)
    {
        Assert.Equal(expected, SoundpadStatusParser.ParseNowPlayingTitle(title));
    }

    [Theory]
    [InlineData(" II  Soundpad - my sound", true)]
    [InlineData(" II  Soundpad", true)]
    [InlineData("Soundpad - my sound", false)]
    [InlineData("Soundpad - II my sound", false)]
    [InlineData("Soundpad", false)]
    [InlineData(null, false)]
    public void IsPausedTitle_DetectsLeadingPauseMarker(string? title, bool expected)
    {
        Assert.Equal(expected, SoundpadStatusParser.IsPausedTitle(title));
    }
}
