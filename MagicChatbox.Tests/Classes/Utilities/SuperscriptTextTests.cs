using System.Linq;
using vrcosc_magicchatbox.Classes.Utilities;
using Xunit;

namespace MagicChatbox.Tests.Classes.Utilities;

public class SuperscriptTextTests
{
    [Fact]
    public void Every_lowercase_letter_but_q_has_a_raised_form()
    {
        string missing = new("abcdefghijklmnopqrstuvwxyz".Where(c => !SuperscriptText.CanRaise(c)).ToArray());

        Assert.Equal("q", missing);
    }

    [Fact]
    public void Every_digit_has_a_raised_form()
        => Assert.All("0123456789", c => Assert.True(SuperscriptText.CanRaise(c), $"no raised '{c}'"));

    [Theory]
    [InlineData('+')]
    [InlineData('-')]
    [InlineData('=')]
    [InlineData('(')]
    [InlineData(')')]
    [InlineData('?')]
    [InlineData('!')]
    [InlineData('<')]
    [InlineData('>')]
    [InlineData('~')]
    [InlineData('^')]
    [InlineData(':')]
    [InlineData('*')]
    [InlineData('|')]
    [InlineData('"')]
    [InlineData('\'')]
    public void The_symbols_worth_raising_are_covered(char value)
        => Assert.True(SuperscriptText.CanRaise(value), $"no raised '{value}'");

    [Fact]
    public void Every_raised_form_is_a_single_basic_plane_character()
    {
        // Two of the tempting candidates live outside the basic plane and would silently cost two
        // characters of the 144 apiece.
        foreach (char c in "abcdefghijklmnopqrstuvwxyz0123456789+-=()?!<>~^:*|\"'`")
        {
            if (SuperscriptText.TryMap(c, out char raised))
                Assert.False(char.IsSurrogate(raised), $"'{c}' maps outside the basic plane");
        }
    }

    [Fact]
    public void Nothing_maps_to_itself()
    {
        // A character that maps to itself is a gap pretending to be covered.
        foreach (char c in "abcdefghijklmnopqrstuvwxyz0123456789+-=()?!<>~^:*|\"'")
        {
            if (SuperscriptText.TryMap(c, out char raised))
                Assert.NotEqual(c, raised);
        }
    }

    [Fact]
    public void Both_kinds_of_apostrophe_land_on_the_same_raised_form()
    {
        Assert.True(SuperscriptText.TryMap('\'', out char straight));
        Assert.True(SuperscriptText.TryMap('’', out char curly));

        Assert.Equal(straight, curly);
    }

    [Theory]
    [InlineData('@')]
    [InlineData('#')]
    [InlineData('$')]
    [InlineData('&')]
    [InlineData('_')]
    [InlineData('q')]
    public void Characters_with_no_dependable_raised_form_are_left_out(char value)
        => Assert.False(SuperscriptText.CanRaise(value), $"'{value}' has no form that can be relied on");

    [Theory]
    [InlineData('%', '⁒')]
    [InlineData('/', '·')]
    public void Two_small_lookalikes_stand_in_where_no_raised_form_exists(char value, char expected)
    {
        // Both already ship in the readouts, so they are known to draw.
        Assert.True(SuperscriptText.TryMap(value, out char raised));
        Assert.Equal(expected, raised);
    }
}
