using vrcosc_magicchatbox.Classes.Modules.Afk;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules.Afk;

public class UnicodeTextStylerTests
{
    [Fact]
    public void Superscript_reproduces_the_long_standing_default_exactly()
    {
        // The shipped default was hand-typed as "ᶜᵘʳʳᵉⁿᵗˡʸ AFK ᶠᵒʳ ". Typing it into the composer has to
        // give back the same characters, or the feature quietly changes what people already had.
        string styled = UnicodeTextStyler.Apply("currently AFK for ", AfkTextStyle.Superscript);

        Assert.Equal("ᶜᵘʳʳᵉⁿᵗˡʸ AFK ᶠᵒʳ ", styled);
    }

    [Fact]
    public void Superscript_leaves_capitals_upright()
    {
        // The block has no C, F, Q, S, X, Y or Z, so styling capitals would render AFK as "ᴬFᴷ".
        Assert.Equal("AFK", UnicodeTextStyler.Apply("AFK", AfkTextStyle.Superscript));
    }

    [Fact]
    public void A_letter_with_no_fancy_form_is_left_alone_rather_than_dropped()
    {
        // 'q' has no superscript anywhere in Unicode. Losing it mid-word would be worse than plain.
        Assert.Equal("ᵃq", UnicodeTextStyler.Apply("aq", AfkTextStyle.Superscript));

        // Small caps has no 'x', and the real small capital Q is missing from too many fonts to use.
        Assert.Equal("ǫx", UnicodeTextStyler.Apply("qx", AfkTextStyle.SmallCaps));
    }

    [Fact]
    public void Small_caps_q_uses_the_stand_in_every_generator_uses()
    {
        // U+A7AF would be the correct character and renders as a blank box in too many fonts.
        Assert.Equal("ǫᴜɪᴄᴋ", UnicodeTextStyler.Apply("quick", AfkTextStyle.SmallCaps));
        Assert.DoesNotContain("ꞯ", UnicodeTextStyler.Apply("quick", AfkTextStyle.SmallCaps));
    }

    [Fact]
    public void Small_caps_maps_lowercase_and_passes_capitals_through()
    {
        Assert.Equal("ᴀᴡᴀʏ", UnicodeTextStyler.Apply("away", AfkTextStyle.SmallCaps));
        Assert.Equal("ᴀᴡᴀʏ", UnicodeTextStyler.Apply("AWAY", AfkTextStyle.SmallCaps));
    }

    [Theory]
    [InlineData(AfkTextStyle.Bold)]
    [InlineData(AfkTextStyle.Italic)]
    [InlineData(AfkTextStyle.Monospace)]
    public void The_astral_alphabets_spend_two_of_the_line_per_letter(AfkTextStyle style)
    {
        // These live outside the basic plane, so each letter is a surrogate pair and costs two of the
        // 144. Someone needs to be told that before their other integrations start getting trimmed.
        Assert.Equal(8, UnicodeTextStyler.CostInChatbox("away", style));
        Assert.Equal(4, UnicodeTextStyler.CostInChatbox("away", AfkTextStyle.Plain));
    }

    [Fact]
    public void The_single_width_alphabets_cost_the_same_as_plain_text()
    {
        Assert.Equal(4, UnicodeTextStyler.CostInChatbox("away", AfkTextStyle.Superscript));
        Assert.Equal(4, UnicodeTextStyler.CostInChatbox("away", AfkTextStyle.SmallCaps));
        Assert.Equal(4, UnicodeTextStyler.CostInChatbox("away", AfkTextStyle.Wide));
    }

    [Fact]
    public void Italic_leaves_digits_alone_because_the_block_has_none()
    {
        Assert.Equal("12", UnicodeTextStyler.Apply("12", AfkTextStyle.Italic));
        Assert.NotEqual("12", UnicodeTextStyler.Apply("12", AfkTextStyle.Bold));
    }

    [Fact]
    public void Wide_uses_the_ideographic_space_so_words_stay_apart()
    {
        Assert.Equal("ａ　ｂ", UnicodeTextStyler.Apply("a b", AfkTextStyle.Wide));
    }

    [Fact]
    public void Plain_and_empty_input_are_returned_untouched()
    {
        Assert.Equal("currently AFK", UnicodeTextStyler.Apply("currently AFK", AfkTextStyle.Plain));
        Assert.Equal(string.Empty, UnicodeTextStyler.Apply("", AfkTextStyle.Bold));
        Assert.Equal(string.Empty, UnicodeTextStyler.Apply(null, AfkTextStyle.Bold));
    }

    [Fact]
    public void Emoji_survive_every_style()
    {
        // Prefixes are emoji, and a surrogate pair must not be mangled by per-char mapping.
        foreach (var style in UnicodeTextStyler.All)
            Assert.Contains("💤", UnicodeTextStyler.Apply("💤 away", style));
    }

    [Fact]
    public void Every_style_has_a_label_to_show_in_the_picker()
    {
        foreach (var style in UnicodeTextStyler.All)
            Assert.False(string.IsNullOrWhiteSpace(UnicodeTextStyler.Describe(style)));
    }
}
