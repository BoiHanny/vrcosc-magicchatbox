using System.Linq;
using vrcosc_magicchatbox.Classes.Utilities;
using vrcosc_magicchatbox.Core.Osc.Text;
using Xunit;

namespace MagicChatbox.Tests.Core.Osc.Text;

public class SegmentWriterTests
{
    #region The value/label rule

    [Theory]
    [InlineData("45")]
    [InlineData("128.5")]
    [InlineData("1:23")]
    [InlineData("The Weeknd")]
    [InlineData("90 fps")]
    public void A_value_is_never_raised(string text)
    {
        // The ratchet. Half the integrations shrank the number and left the label full size, which
        // hides the only part the reader wants.
        Assert.Equal(text, OscText.Value(text).Rendered);
    }

    [Fact]
    public void No_value_ever_contains_a_raised_character()
    {
        string[] values = ["45", "128.5", "gpu", "1:23", "0%", "-12"];

        foreach (string value in values)
        {
            string rendered = OscText.Value(value).Rendered;
            Assert.All(rendered, c => Assert.False(
                IsRaised(c),
                $"\"{value}\" came back with the raised character '{c}'"));
        }
    }

    [Fact]
    public void A_label_is_raised()
        => Assert.Equal(TextUtilities.TransformToSuperscript("gpu"), OscText.Label("gpu").Rendered);

    [Fact]
    public void A_unit_is_raised()
        => Assert.Equal(TextUtilities.TransformToSuperscript("fps"), OscText.Unit("fps").Rendered);

    [Fact]
    public void Raw_text_is_placed_exactly_as_given()
        => Assert.Equal("🎵", OscText.Raw("🎵").Rendered);

    #endregion

    #region Joining

    [Fact]
    public void A_unit_glues_to_the_value_in_front_of_it()
    {
        string text = new SegmentWriter().Field(OscText.Value("45"), OscText.Unit("%")).Text;

        Assert.Equal("45" + TextUtilities.TransformToSuperscript("%"), text);
    }

    [Fact]
    public void A_label_takes_a_space()
    {
        string text = new SegmentWriter().Field(OscText.Label("gpu"), OscText.Value("45")).Text;

        Assert.Equal(TextUtilities.TransformToSuperscript("gpu") + " 45", text);
    }

    [Fact]
    public void Fields_are_joined_by_the_field_glyph()
    {
        string text = new SegmentWriter()
            .Field(OscText.Label("gpu"), OscText.Value("45"), OscText.Unit("%"))
            .Field(OscText.Value("62"), OscText.Unit("°C"))
            .Text;

        Assert.Contains(OscGlyphs.FieldJoin, text);
        Assert.Equal(2, text.Split(OscGlyphs.FieldJoin).Length);
    }

    [Fact]
    public void An_empty_part_does_not_leave_a_gap_behind_it()
    {
        string text = new SegmentWriter()
            .Field(OscText.Label(""), OscText.Value("45"), OscText.Unit(null))
            .Text;

        Assert.Equal("45", text);
    }

    [Fact]
    public void A_field_with_nothing_in_it_is_not_joined_at_all()
    {
        string text = new SegmentWriter()
            .Field(OscText.Value("45"))
            .Field(OscText.Value(""))
            .Field(OscText.Value("62"))
            .Text;

        Assert.Equal("45" + OscGlyphs.FieldJoin + "62", text);
    }

    [Fact]
    public void FieldIf_keeps_a_call_site_flat()
    {
        string text = new SegmentWriter()
            .Field(OscText.Value("45"))
            .FieldIf(false, OscText.Value("62"))
            .Text;

        Assert.Equal("45", text);
    }

    [Fact]
    public void Cost_is_what_the_text_actually_measures()
    {
        var writer = new SegmentWriter().Field(OscText.Label("gpu"), OscText.Value("45"));

        Assert.Equal(writer.Text.Length, writer.Cost);
    }

    #endregion

    #region Whitespace

    [Theory]
    [InlineData("  a  b  ", "a b")]
    [InlineData("a\t\tb", "a b")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    public void Whitespace_is_collapsed_and_trimmed(string? input, string expected)
        => Assert.Equal(expected, SegmentWriter.Tidy(input));

    [Fact]
    public void A_newline_survives_because_it_is_meaningful_in_a_chatbox_line()
        => Assert.Equal("a\nb", SegmentWriter.Tidy("a\nb"));

    [Fact]
    public void A_segment_never_comes_out_with_an_edge_space()
    {
        string text = new SegmentWriter().Field(OscText.Value("  padded  ")).Text;

        Assert.Equal("padded", text);
    }

    #endregion

    #region Fitting and cutting

    [Fact]
    public void Fit_takes_the_longest_rung_that_still_fits()
        => Assert.Equal("bb", SegmentWriter.Fit(2, "aaaa", "bb", "c"));

    [Fact]
    public void Fit_falls_back_to_the_shortest_rung_cut_to_size()
    {
        string text = SegmentWriter.Fit(4, "aaaaaaaa", "bbbbbb");

        Assert.True(text.Length <= 4);
        Assert.EndsWith(OscGlyphs.Ellipsis, text);
    }

    [Fact]
    public void Fit_with_room_to_spare_changes_nothing()
        => Assert.Equal("aaaa", SegmentWriter.Fit(100, "aaaa", "bb"));

    [Fact]
    public void Truncate_marks_the_cut_with_one_character()
    {
        string text = SegmentWriter.Truncate("abcdefghij", 5);

        Assert.True(text.Length <= 5);
        Assert.EndsWith(OscGlyphs.Ellipsis, text);
        Assert.DoesNotContain("...", text);
    }

    [Fact]
    public void Truncate_never_splits_a_surrogate_pair()
    {
        // A lone half of a pair renders as a replacement box and costs a character for nothing.
        string text = "ab🎵🎵🎵🎵🎵";

        for (int budget = 1; budget <= text.Length; budget++)
        {
            string cut = SegmentWriter.Truncate(text, budget);

            Assert.All(cut, c => Assert.False(
                char.IsLowSurrogate(c) && cut.IndexOf(c) == 0,
                $"budget {budget} produced a lone low surrogate"));
            Assert.False(cut.Length > 0 && char.IsHighSurrogate(cut[^1]), $"budget {budget} left a dangling high surrogate");
            Assert.True(cut.Length <= budget, $"budget {budget} produced {cut.Length} characters");
        }
    }

    [Fact]
    public void Truncate_prefers_a_word_boundary_when_one_is_close()
        => Assert.Equal("hello" + OscGlyphs.Ellipsis, SegmentWriter.Truncate("hello world", 8));

    [Fact]
    public void Truncate_leaves_text_that_already_fits_alone()
        => Assert.Equal("short", SegmentWriter.Truncate("short", 40));

    [Fact]
    public void Truncate_with_no_room_gives_back_nothing()
        => Assert.Equal(string.Empty, SegmentWriter.Truncate("anything", 0));

    #endregion

    private static bool IsRaised(char c)
        => "abcdefghijklmnopqrstuvwxyz0123456789".Any(plain =>
            SuperscriptText.TryMap(plain, out char raised) && raised == c);
}
