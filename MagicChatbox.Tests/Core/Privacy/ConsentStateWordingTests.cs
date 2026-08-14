using System;
using System.ComponentModel;
using System.Linq;
using vrcosc_magicchatbox.Core.Privacy;
using Xunit;

namespace MagicChatbox.Tests.Core.Privacy;

/// <summary>
/// The permission rows put this enum on screen. Without a description the badge printed the C#
/// identifier, so an ordinary "nobody has asked you yet" row read as the word Unknown.
/// </summary>
public class ConsentStateWordingTests
{
    [Fact]
    public void Every_state_carries_wording_meant_for_a_reader()
    {
        var missing = Enum.GetValues<ConsentState>()
            .Where(state => Description(state) == null)
            .ToList();

        Assert.True(missing.Count == 0, "no [Description]: " + string.Join(", ", missing));
    }

    [Fact]
    public void No_description_is_just_the_identifier_again()
    {
        var lazy = Enum.GetValues<ConsentState>()
            .Where(state => string.Equals(Description(state), state.ToString(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(lazy.Count == 0, "description repeats the identifier: " + string.Join(", ", lazy));
    }

    [Theory]
    [InlineData(ConsentState.Unknown, "Not asked yet")]
    [InlineData(ConsentState.Approved, "Allowed")]
    [InlineData(ConsentState.Denied, "Blocked")]
    public void The_badge_says_what_the_user_did(ConsentState state, string expected)
        => Assert.Equal(expected, Description(state));

    private static string? Description(ConsentState state)
        => typeof(ConsentState)
            .GetField(state.ToString())?
            .GetCustomAttributes(typeof(DescriptionAttribute), false)
            .OfType<DescriptionAttribute>()
            .FirstOrDefault()?.Description;
}
