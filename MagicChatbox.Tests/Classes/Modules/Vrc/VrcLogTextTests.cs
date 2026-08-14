using System;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.Utilities;
using vrcosc_magicchatbox.Core.Osc.Text;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules.Vrc;

/// <summary>
/// The radar prints text it read out of a log file - world names, display names - so nothing it
/// emits has a length of its own until something gives it one.
/// </summary>
public class VrcLogTextTests
{
    #region Names out of the log

    [Fact]
    public void A_normal_world_name_passes_through_untouched()
        => Assert.Equal("The Black Cat", VrcLogText.Name("The Black Cat"));

    [Fact]
    public void A_name_long_enough_to_fill_the_line_is_cut()
    {
        string cut = VrcLogText.Name(new string('w', 200));

        Assert.True(cut.Length <= VrcLogText.MaxNameChars);
        Assert.EndsWith(OscGlyphs.Ellipsis, cut);
        Assert.DoesNotContain("...", cut);
    }

    [Fact]
    public void A_cut_name_never_leaves_half_an_emoji_behind()
    {
        string cut = VrcLogText.Name("🎵" + new string('w', 60));

        Assert.False(char.IsHighSurrogate(cut[^1]));
    }

    [Fact]
    public void A_name_is_trimmed_and_collapsed_before_anything_else()
        => Assert.Equal("The Black Cat", VrcLogText.Name("  The   Black  Cat  "));

    #endregion

    #region The value/label rule

    [Fact]
    public void A_duration_keeps_its_number_full_size_and_raises_the_unit()
    {
        string text = VrcLogText.Duration(TimeSpan.FromSeconds(42));

        Assert.Equal("42" + TextUtilities.TransformToSuperscript("s"), text);
    }

    [Fact]
    public void A_unit_is_glued_to_the_number_it_belongs_to()
        => Assert.DoesNotContain(" ", VrcLogText.Duration(TimeSpan.FromMinutes(7)));

    [Fact]
    public void An_hour_carries_its_minutes_with_both_units_raised()
    {
        string text = VrcLogText.Duration(new TimeSpan(1, 5, 0));

        Assert.Equal(
            "1" + TextUtilities.TransformToSuperscript("h") + "05" + TextUtilities.TransformToSuperscript("m"),
            text);
    }

    [Fact]
    public void No_digit_in_a_duration_is_ever_raised()
    {
        string text = VrcLogText.Duration(new TimeSpan(2, 34, 56));

        Assert.All(text, c => Assert.False(
            "0123456789".Any(d => SuperscriptText.TryMap(d, out char raised) && raised == c),
            $"a digit came back raised in \"{text}\""));
    }

    #endregion

    #region Fitting a rendered line

    private static string Render(string world) => $"🌎 {world} | 👥 12 | Public EU";

    [Fact]
    public void A_line_that_fits_is_rendered_once_and_left_alone()
        => Assert.Equal(Render("The Black Cat"), VrcLogText.FitToBudget(Render, "The Black Cat", 144));

    [Fact]
    public void The_world_name_is_what_gives_way_not_the_line()
    {
        string text = VrcLogText.FitToBudget(Render, new string('w', 120), 60);

        Assert.True(text.Length <= 60, $"came back at {text.Length}");
        // The template's own words are the part that says what the numbers mean, so they survive.
        Assert.EndsWith("| 👥 12 | Public EU", text);
        Assert.Contains(OscGlyphs.Ellipsis, text);
    }

    [Fact]
    public void A_template_with_no_room_for_its_own_words_is_still_cut_to_size()
    {
        string text = VrcLogText.FitToBudget(Render, "The Black Cat", 12);

        Assert.True(text.Length <= 12, $"came back at {text.Length}");
    }

    [Fact]
    public void A_budget_of_nothing_produces_nothing_rather_than_a_stray_mark()
        => Assert.Equal(string.Empty, VrcLogText.FitToBudget(Render, "The Black Cat", 0));

    [Fact]
    public void A_negative_budget_is_treated_as_no_room_at_all()
        => Assert.Equal(string.Empty, VrcLogText.FitToBudget(Render, "The Black Cat", -20));

    [Fact]
    public void A_template_that_never_mentions_the_world_is_cut_as_a_whole()
    {
        string text = VrcLogText.FitToBudget(_ => new string('x', 200), "The Black Cat", 40);

        Assert.True(text.Length <= 40);
        Assert.EndsWith(OscGlyphs.Ellipsis, text);
    }

    #endregion
}
