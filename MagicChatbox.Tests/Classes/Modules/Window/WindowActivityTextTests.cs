using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core;
using vrcosc_magicchatbox.Core.Osc.Text;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules.Window;

/// <summary>
/// A window title is whatever the app decided to put in its title bar, and a browser tab alone can
/// be longer than the whole chatbox line. These pin the bound and the shape of the cut.
/// </summary>
public class WindowActivityTextTests
{
    [Fact]
    public void A_title_that_fits_is_left_exactly_as_it_is()
        => Assert.Equal("Inbox", SegmentWriter.Truncate("Inbox", WindowActivityText.TitleCap(true, 35)));

    [Fact]
    public void A_cut_title_spends_one_character_saying_so_not_three()
    {
        int cap = WindowActivityText.TitleCap(true, 20);
        string cut = SegmentWriter.Truncate(new string('a', 200), cap);

        Assert.True(cut.Length <= 20);
        Assert.EndsWith(OscGlyphs.Ellipsis, cut);
        Assert.DoesNotContain("...", cut);
    }

    [Fact]
    public void A_cut_never_lands_between_the_two_halves_of_an_emoji()
    {
        // The old Substring could, and half a pair draws as a replacement box.
        string title = "ab🎵🎵🎵🎵🎵🎵🎵🎵";

        for (int cap = 1; cap <= title.Length; cap++)
        {
            string cut = SegmentWriter.Truncate(title, WindowActivityText.TitleCap(true, cap));

            Assert.False(cut.Length > 0 && char.IsHighSurrogate(cut[^1]), $"cap {cap} left a dangling high surrogate");
            Assert.True(cut.Length <= cap, $"cap {cap} produced {cut.Length} characters");
        }
    }

    [Fact]
    public void Turning_the_limit_off_still_caps_at_the_length_of_the_whole_line()
    {
        // "Off" used to mean unbounded, and one title took the entire chatbox with it.
        Assert.Equal(Constants.OscMaxMessageLength, WindowActivityText.TitleCap(false, 35));
    }

    [Fact]
    public void A_limit_larger_than_the_line_is_brought_back_to_the_line()
        => Assert.Equal(Constants.OscMaxMessageLength, WindowActivityText.TitleCap(true, 5000));

    [Fact]
    public void A_negative_limit_does_not_come_back_as_a_negative_budget()
        => Assert.Equal(0, WindowActivityText.TitleCap(true, -10));

    [Fact]
    public void The_app_name_is_quoted_and_the_title_bracketed_after_it()
        => Assert.Equal("'Firefox' (Inbox)", WindowActivityText.Compose("Firefox", "Inbox"));

    [Fact]
    public void There_is_no_stray_space_inside_the_bracket()
    {
        // The renamed-app branch wrote "(title)" and the plain branch wrote "( title)".
        Assert.DoesNotContain("( ", WindowActivityText.Compose("Firefox", "Inbox"));
    }

    [Fact]
    public void An_app_with_no_title_shown_is_just_the_app()
        => Assert.Equal("'Firefox'", WindowActivityText.Compose("Firefox", null));

    [Fact]
    public void An_empty_title_does_not_leave_empty_brackets_behind()
        => Assert.Equal("'Firefox'", WindowActivityText.Compose("Firefox", "   "));

    [Fact]
    public void An_app_name_out_of_a_uwp_window_cannot_run_away_with_the_line()
    {
        string composed = WindowActivityText.Compose(new string('x', 300), null);

        Assert.True(composed.Length <= WindowActivityText.MaxAppNameChars + 2);
        Assert.EndsWith(OscGlyphs.Ellipsis + "'", composed);
    }

    [Fact]
    public void The_app_name_is_never_raised()
    {
        // It is the value - which app - and shrinking it hides the only part worth reading.
        Assert.Equal("'Firefox' (Inbox)", WindowActivityText.Compose("  Firefox  ", "  Inbox  "));
    }
}
