using vrcosc_magicchatbox.Core.Osc;
using vrcosc_magicchatbox.Core.Osc.Providers;
using Xunit;

namespace MagicChatbox.Tests.Core.Osc;

/// <summary>
/// Soundpad's segment is one clip title, and a clip title is whatever the file was called - there
/// was no limit on it at all. These pin the bound, and that the title is the one thing in the
/// segment that keeps its full size.
/// </summary>
public class SoundpadSegmentTests
{
    private const int Line = OscBuildContext.MaxOscLength;
    private const string Icon = "🎶";

    [Fact]
    public void A_short_clip_name_is_written_exactly_as_it_always_was()
        => Assert.Equal("🎶 'clip name'", SoundpadOscProvider.BuildSegment("clip name", withIcon: true, Line));

    [Fact]
    public void The_icon_can_still_be_turned_off()
        => Assert.Equal("'clip name'", SoundpadOscProvider.BuildSegment("clip name", withIcon: false, Line));

    [Theory]
    [InlineData(6)]
    [InlineData(12)]
    [InlineData(40)]
    [InlineData(Line)]
    public void The_segment_never_exceeds_the_room_it_was_given(int budget)
    {
        string text = SoundpadOscProvider.BuildSegment(new string('c', 300), withIcon: true, budget);

        Assert.True(text.Length <= budget, $"budget {budget} produced {text.Length}: {text}");
    }

    [Fact]
    public void A_clip_name_longer_than_the_line_is_cut_instead_of_dropping_the_segment()
    {
        string text = SoundpadOscProvider.BuildSegment(new string('c', 300), withIcon: true, Line);

        Assert.Equal(Line, text.Length);
        Assert.StartsWith(Icon, text);

        // The closing quote is part of the shape, not part of the title, so the cut lands inside it.
        Assert.EndsWith("…'", text);
    }

    [Fact]
    public void With_almost_no_room_the_icon_is_given_up_so_the_title_keeps_something()
    {
        string text = SoundpadOscProvider.BuildSegment(new string('c', 40), withIcon: true, budget: 5);

        Assert.Equal("'cc…'", text);
    }

    [Fact]
    public void The_title_is_never_raised()
    {
        string text = SoundpadOscProvider.BuildSegment("audio clip", withIcon: true, Line);

        Assert.Contains("audio clip", text);
    }

    [Fact]
    public void A_ragged_clip_name_is_tidied_before_it_is_measured()
        => Assert.Equal(
            "🎶 'clip name'",
            SoundpadOscProvider.BuildSegment("  clip   name  ", withIcon: true, Line));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Nothing_playing_means_no_segment(string? title)
        => Assert.Equal(string.Empty, SoundpadOscProvider.BuildSegment(title, withIcon: true, Line));

    [Fact]
    public void No_room_left_means_no_segment()
        => Assert.Equal(string.Empty, SoundpadOscProvider.BuildSegment("clip name", withIcon: true, budget: 0));
}
