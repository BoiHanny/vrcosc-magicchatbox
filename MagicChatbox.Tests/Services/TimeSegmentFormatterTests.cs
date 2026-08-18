using vrcosc_magicchatbox.Services;
using Xunit;

namespace MagicChatbox.Tests.Services;

/// <summary>
/// The Time settings preview and the OSC provider compose through this, so what the section shows
/// and what the chatbox receives are the same string by construction.
/// </summary>
public sealed class TimeSegmentFormatterTests
{
    [Fact]
    public void WithoutTheLabelTheLineIsJustTheClock()
    {
        Assert.Equal("13:37", TimeSegmentFormatter.Compose("13:37", showLabel: false));
    }

    [Fact]
    public void TheClockStaysFullSizeAndOnlyTheLabelIsRaised()
    {
        string line = TimeSegmentFormatter.Compose("13:37", showLabel: true);

        Assert.Contains("13:37", line);
        Assert.DoesNotContain("¹³", line);
        Assert.Contains("ᵐʸ ᵗⁱᵐᵉ", line);
    }

    [Fact]
    public void TheLabelIsFollowedByOneSpaceRatherThanAColon()
    {
        string line = TimeSegmentFormatter.Compose("13:37", showLabel: true);

        Assert.Equal("ᵐʸ ᵗⁱᵐᵉ 13:37", line);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NoClockMeansNoLineRatherThanALabelOnItsOwn(string? clock)
    {
        Assert.Equal(string.Empty, TimeSegmentFormatter.Compose(clock, showLabel: true));
    }

    [Fact]
    public void TheTwelveHourClockKeepsItsMeridiem()
    {
        Assert.Equal("01:37 PM", TimeSegmentFormatter.Compose("01:37 PM", showLabel: false));
    }
}
