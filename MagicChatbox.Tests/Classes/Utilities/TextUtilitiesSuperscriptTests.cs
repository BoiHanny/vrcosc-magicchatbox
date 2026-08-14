using vrcosc_magicchatbox.Classes.Utilities;
using Xunit;

namespace MagicChatbox.Tests.Classes.Utilities;

/// <summary>
/// This runs on the component stats, network, Pulsoid, tracker battery, Twitch, VR performance,
/// weather and seekbar readouts, so a mistake here is visible in eight places at once.
/// </summary>
public class TextUtilitiesSuperscriptTests
{
    [Theory]
    [InlineData("abc", "ᵃᵇᶜ")]
    [InlineData("ABC", "ᵃᵇᶜ")]
    [InlineData("123", "¹²³")]
    [InlineData("cpu 45", "ᶜᵖᵘ ⁴⁵")]
    public void Letters_and_digits_are_raised(string input, string expected)
        => Assert.Equal(expected, TextUtilities.TransformToSuperscript(input));

    [Fact]
    public void A_degree_sign_survives_instead_of_being_deleted()
        => Assert.Equal("°ᶜ", TextUtilities.TransformToSuperscript("°C"));

    [Fact]
    public void A_hardware_name_keeps_its_punctuation()
    {
        Assert.Equal("ⁱ⁷⁻⁹⁷⁰⁰ᵏ", TextUtilities.TransformToSuperscript("i7-9700K"));
        Assert.Equal("ⁱⁿᵗᵉˡ⁽ʳ⁾", TextUtilities.TransformToSuperscript("Intel(R)"));
    }

    [Fact]
    public void Q_stays_a_q_instead_of_turning_into_an_o()
    {
        // The old table mapped it to the raised "o", so any word with a q in it was misspelt.
        Assert.Equal("qᵘᵉᵘᵉ", TextUtilities.TransformToSuperscript("queue"));
    }

    [Fact]
    public void A_decimal_point_stays_a_decimal_point()
    {
        // The old table turned it into an apostrophe, so "1.5" read as "1'5".
        Assert.Equal("¹.⁵", TextUtilities.TransformToSuperscript("1.5"));
    }

    [Fact]
    public void A_comma_stays_a_comma()
        => Assert.Equal("ᵃ,ᵇ", TextUtilities.TransformToSuperscript("a,b"));

    [Fact]
    public void A_colon_becomes_a_raised_colon_rather_than_an_apostrophe()
        => Assert.Equal("⁰˸³⁰", TextUtilities.TransformToSuperscript("0:30"));

    [Fact]
    public void Whitespace_is_kept_as_a_single_space()
        => Assert.Equal("ᵃ ᵇ", TextUtilities.TransformToSuperscript("a\tb"));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Missing_text_is_handled(string? input)
        => Assert.Equal(string.Empty, TextUtilities.TransformToSuperscript(input!));

    [Fact]
    public void Nothing_is_ever_silently_dropped()
    {
        const string input = "a@b#c$d%e&f_g/h";

        string raised = TextUtilities.TransformToSuperscript(input);

        Assert.Equal(input.Length, raised.Length);
    }

    [Fact]
    public void Percent_and_slash_keep_their_small_lookalikes()
    {
        // Neither has a true raised form, but both have been in the readouts long enough to prove
        // they draw, and a full-size one in the middle of raised text is what this avoids.
        Assert.Equal("⁵⁰⁒", TextUtilities.TransformToSuperscript("50%"));
        Assert.Equal("ᵏᵇ·ˢ", TextUtilities.TransformToSuperscript("KB/s"));
    }

    [Fact]
    public void The_result_never_costs_more_characters_than_it_started_with()
    {
        // Everything in the table is a single basic-plane character, so raising text cannot push a
        // line over the 144 limit.
        const string input = "GPU 65°C 120W i7-9700K 1.5 0:30 (x)";

        Assert.Equal(input.Length, TextUtilities.TransformToSuperscript(input).Length);
    }
}
