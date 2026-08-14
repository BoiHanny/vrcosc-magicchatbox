using vrcosc_magicchatbox.Classes.Modules.Lyrics;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules.Lyrics;

public class LyricAsidesTests
{
    [Fact]
    public void An_aside_is_raised_and_loses_its_brackets()
        => Assert.Equal("walking home ʷᵃˡᵏⁱⁿᵍ ʰᵒᵐᵉ in the rain",
            LyricAsides.Apply("walking home (walking home) in the rain"));

    [Fact]
    public void Several_asides_in_one_line_are_all_raised()
        => Assert.Equal("ᵒʰ yeah ᵒʰ", LyricAsides.Apply("(oh) yeah (oh)"));

    [Fact]
    public void An_aside_that_is_the_whole_line_still_works()
        => Assert.Equal("ᵃᵃᵃʰ", LyricAsides.Apply("(aaah)"));

    [Fact]
    public void Case_does_not_matter()
        => Assert.Equal("ᵒʰ ʸᵉᵃʰ", LyricAsides.Apply("(OH YEAH)"));

    [Fact]
    public void Digits_are_raised_too()
        => Assert.Equal("count ¹²³", LyricAsides.Apply("count (123)"));

    [Fact]
    public void A_comma_is_left_alone_because_its_raised_form_is_an_apostrophe()
    {
        // Swapping it would change the words, and a comma already sits low enough to pass.
        Assert.Equal("ᵒʰ, ʸᵉᵃʰꜝ", LyricAsides.Apply("(oh, yeah!)"));
    }

    [Theory]
    [InlineData("(what?)", "ʷʰᵃᵗˀ")]
    [InlineData("(hey!)", "ʰᵉʸꜝ")]
    [InlineData("(what?!)", "ʷʰᵃᵗˀꜝ")]
    [InlineData("expecting (what?)", "expecting ʷʰᵃᵗˀ")]
    public void A_question_or_exclamation_mark_is_raised_with_the_rest(string raw, string expected)
    {
        // Left full size these tower over the raised letters beside them and the aside looks broken.
        Assert.Equal(expected, LyricAsides.Apply(raw));
    }

    [Theory]
    [InlineData("(a+b=c)", "ᵃ⁺ᵇ⁼ᶜ")]
    [InlineData("(2x4)", "²ˣ⁴")]
    [InlineData("(a<b>c)", "ᵃ˂ᵇ˃ᶜ")]
    [InlineData("(rock*roll)", "ʳᵒᶜᵏ˟ʳᵒˡˡ")]
    [InlineData("(up~down)", "ᵘᵖ˜ᵈᵒʷⁿ")]
    [InlineData("(9:30)", "⁹˸³⁰")]
    public void Symbols_with_a_raised_form_use_it(string raw, string expected)
        => Assert.Equal(expected, LyricAsides.Apply(raw));

    [Fact]
    public void A_raised_run_never_touches_a_full_size_word()
    {
        Assert.Equal("word ᵃˢⁱᵈᵉ word", LyricAsides.Apply("word(aside)word"));
        Assert.Equal("before ᵃˢⁱᵈᵉ", LyricAsides.Apply("before(aside)"));
        Assert.Equal("ᵃˢⁱᵈᵉ after", LyricAsides.Apply("(aside)after"));
    }

    [Fact]
    public void Trailing_punctuation_still_hugs_the_raised_run()
    {
        // A space before a comma would be wrong wherever it appeared.
        Assert.Equal("down ˡᵃᵃᵍ, up", LyricAsides.Apply("down (laag), up"));
        Assert.Equal("end ᵒʰ.", LyricAsides.Apply("end (oh)."));
    }

    [Fact]
    public void A_space_that_was_already_there_is_not_doubled()
        => Assert.Equal("hey ᵒʰ there", LyricAsides.Apply("hey (oh) there"));

    [Fact]
    public void A_line_with_no_aside_is_untouched()
        => Assert.Equal("just a normal line", LyricAsides.Apply("just a normal line"));

    [Fact]
    public void One_letter_without_a_raised_form_does_not_cost_the_whole_aside()
    {
        // Unicode has no raised "q". Refusing the group over it would leave one aside in a song
        // bracketed while the rest were raised.
        Assert.Equal("hey qᵘᵉᵉⁿ", LyricAsides.Apply("hey (queen)"));
    }

    [Fact]
    public void Another_script_keeps_its_brackets()
    {
        // Nothing here can be raised, so raising it would only lose what the brackets said.
        Assert.Equal("hey (こんにちは)", LyricAsides.Apply("hey (こんにちは)"));
    }

    [Fact]
    public void An_accented_letter_rides_along_at_full_size()
        => Assert.Equal("hey ᶜᵃᶠé", LyricAsides.Apply("hey (café)"));

    [Fact]
    public void A_group_that_is_mostly_unraisable_keeps_its_brackets()
        => Assert.Equal("hey (こんにちは ok)", LyricAsides.Apply("hey (こんにちは ok)"));

    [Fact]
    public void An_aside_of_only_punctuation_is_not_worth_raising()
        => Assert.Equal("hey (...)", LyricAsides.Apply("hey (...)"));

    [Fact]
    public void Empty_brackets_are_left_alone()
        => Assert.Equal("hey ()", LyricAsides.Apply("hey ()"));

    [Fact]
    public void Padding_inside_the_brackets_does_not_leak_out_as_double_spaces()
        => Assert.Equal("hey ᵒʰ there", LyricAsides.Apply("hey ( oh ) there"));

    [Fact]
    public void Raising_never_makes_the_line_longer()
    {
        const string raw = "walking home (walking home) in the rain (oh oh)";

        Assert.True(LyricAsides.Apply(raw).Length < raw.Length);
    }

    [Fact]
    public void Every_raised_character_costs_a_single_character_of_the_line()
    {
        // Surrogate pairs look like one glyph and spend two of the 144.
        string raised = LyricAsides.Apply("(the brown fox jumps over 1234567890)");

        Assert.All(raised, c => Assert.False(char.IsSurrogate(c)));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void Missing_text_is_handled(string? input, string expected)
        => Assert.Equal(expected, LyricAsides.Apply(input));

    [Fact]
    public void The_setting_turns_it_off()
    {
        var on = new LyricsSettings { SuperscriptAsides = true };
        var off = new LyricsSettings { SuperscriptAsides = false };

        Assert.Equal("ᵒʰ yeah", LyricSegmentFormatter.PrepareLine("(oh) yeah", on));
        Assert.Equal("(oh) yeah", LyricSegmentFormatter.PrepareLine("(oh) yeah", off));
    }

    [Fact]
    public void It_is_on_for_a_fresh_install()
        => Assert.True(new LyricsSettings().SuperscriptAsides);

    // The line shapes a lyric file actually throws at this: a whole line in brackets, an apostrophe,
    // a word with no raised letter, two asides on one line, two touching, a hyphen inside one, and a
    // comma straight after a closing bracket.
    [Theory]
    [InlineData(
        "(waitin' for the light)",
        "ʷᵃⁱᵗⁱⁿʼ ᶠᵒʳ ᵗʰᵉ ˡⁱᵍʰᵗ")]
    [InlineData(
        "(there is a long line of quiet words down here)",
        "ᵗʰᵉʳᵉ ⁱˢ ᵃ ˡᵒⁿᵍ ˡⁱⁿᵉ ᵒᶠ qᵘⁱᵉᵗ ʷᵒʳᵈˢ ᵈᵒʷⁿ ʰᵉʳᵉ")]
    [InlineData(
        "holding on to nothing much (yeah)",
        "holding on to nothing much ʸᵉᵃʰ")]
    [InlineData(
        "counting all the reasons why (last quarter)",
        "counting all the reasons why ˡᵃˢᵗ qᵘᵃʳᵗᵉʳ")]
    [InlineData(
        "first light, second wind (hey, up-down)",
        "first light, second wind ʰᵉʸ, ᵘᵖ⁻ᵈᵒʷⁿ")]
    [InlineData(
        "(loudly) then (softly) after",
        "ˡᵒᵘᵈˡʸ then ˢᵒᶠᵗˡʸ after")]
    [InlineData(
        "left side down (down), right side up (up)",
        "left side down ᵈᵒʷⁿ, right side up ᵘᵖ")]
    [InlineData(
        "over and over again (once) (twice)",
        "over and over again ᵒⁿᶜᵉ ᵗʷⁱᶜᵉ")]
    [InlineData(
        "up-up-up-and-away, no brackets here",
        "up-up-up-and-away, no brackets here")]
    public void Lines_come_out_the_way_they_should(string raw, string expected)
        => Assert.Equal(expected, LyricAsides.Apply(raw));

    [Fact]
    public void Every_aside_in_a_song_gets_the_same_treatment()
    {
        // One aside left bracketed while the others are raised looks like a bug, so no line that
        // had an aside may still be carrying a bracket afterwards.
        string[] lines =
        [
            "(waitin' for the light)",
            "(there is a long line of quiet words down here)",
            "holding on to nothing much (yeah)",
            "counting all the reasons why (last quarter)",
            "left side down (down), right side up (up)",
            "over and over again (once) (twice)",
        ];

        Assert.All(lines, line => Assert.DoesNotContain('(', LyricAsides.Apply(line)));
    }
}
