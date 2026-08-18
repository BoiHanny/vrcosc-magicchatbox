using vrcosc_magicchatbox.Core.Osc;
using Xunit;

namespace MagicChatbox.Tests.Core.Osc;

/// <summary>
/// The prefix, suffix, separator and ENTER settings shape every line the app sends, and App options
/// used to show four bare text boxes and nothing else. These pin the sample line the section now
/// draws, because a preview that disagrees with the builder is worse than no preview at all.
/// </summary>
public class OscLinePreviewTests
{
    private const string DefaultSeparator = " ┆ ";

    [Fact]
    public void The_separator_box_joins_the_segments_when_ENTER_mode_is_off()
    {
        string line = OscLinePreview.Build(prefix: "", suffix: "", separator: " | ", separateWithEnters: false);

        Assert.Equal("12:37 | 78 bpm", line);
    }

    [Fact]
    public void ENTER_mode_overrules_whatever_is_in_the_separator_box()
    {
        // The builder ignores the box entirely in this mode. A preview that still showed the
        // separator would have the user tuning a setting that is doing nothing.
        string line = OscLinePreview.Build(prefix: "", suffix: "", separator: " | ", separateWithEnters: true);

        Assert.Equal("12:37\n78 bpm", line);
    }

    [Fact]
    public void An_empty_separator_box_falls_back_to_the_shipped_default()
    {
        string line = OscLinePreview.Build(prefix: "", suffix: "", separator: "   ", separateWithEnters: false);

        Assert.Equal("12:37" + DefaultSeparator + "78 bpm", line);
    }

    [Fact]
    public void The_prefix_and_suffix_wrap_the_whole_line_rather_than_each_segment()
    {
        string line = OscLinePreview.Build(prefix: "[", suffix: "]", separator: " | ", separateWithEnters: false);

        Assert.Equal("[12:37 | 78 bpm]", line);
    }

    [Fact]
    public void The_newline_escape_is_expanded_the_way_the_builder_expands_it()
    {
        // The section's own tip promises this escape works. Both ends go through the same helper,
        // so the preview shows a break rather than the two characters the user typed.
        string line = OscLinePreview.Build(@"hi\n", @"\nbye", " | ", separateWithEnters: false);

        Assert.Equal("hi\n12:37 | 78 bpm\nbye", line);
        Assert.DoesNotContain(@"\n", line);
    }

    [Fact]
    public void A_long_prefix_shows_up_in_the_cost_the_chip_reports()
    {
        // This is the whole point of putting a preview here: prefix characters come out of the same
        // 144 the integrations are competing for, and nothing in the UI used to say so.
        string bare = OscLinePreview.Build("", "", " | ", separateWithEnters: false);
        string withPrefix = OscLinePreview.Build(new string('x', 40), "", " | ", separateWithEnters: false);

        Assert.Equal(bare.Length + 40, withPrefix.Length);
        Assert.True(withPrefix.Length < OscBuildContext.MaxOscLength);
    }

    [Fact]
    public void The_samples_are_short_enough_to_leave_the_settings_room_to_show_their_effect()
    {
        // Stand-ins that already filled the line would make every edit look like an overflow.
        string line = OscLinePreview.Build("", "", null, separateWithEnters: false);

        Assert.Equal(2, OscLinePreview.SampleSegments.Count);
        Assert.True(line.Length < 30, $"sample line is {line.Length} characters");
    }
}
