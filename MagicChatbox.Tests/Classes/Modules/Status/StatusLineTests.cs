using System.Linq;
using vrcosc_magicchatbox.Classes.Modules.Status;
using vrcosc_magicchatbox.Classes.Utilities;
using vrcosc_magicchatbox.Core.Osc.Text;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules.Status;

public class StatusLineTests
{
    /// <summary>The status editor accepts this many characters, so a saved status can be this long.</summary>
    private const int EditorMaxLength = 144;

    private const int Line = 144;

    /// <summary>Two characters, because it is outside the basic plane.</summary>
    private const string Icon = "💬";

    [Fact]
    public void The_longest_saveable_status_no_longer_overruns_the_line()
    {
        string message = new string('a', EditorMaxLength);

        // What the provider used to hand over: the icon, a space, and every character the editor
        // allowed. Three over the limit before any other integration or separator existed.
        Assert.Equal(147, $"{Icon} {message}".Length);

        string composed = StatusLine.Compose(message, Icon, prefixIcon: true, Line);

        Assert.True(composed.Length <= Line, $"composed {composed.Length} characters");
    }

    [Fact]
    public void The_icon_goes_before_the_users_words_do()
    {
        string message = new string('a', Line);

        string composed = StatusLine.Compose(message, Icon, prefixIcon: true, Line);

        // Dropping the icon buys back three characters at once, which is exactly enough here, so
        // the message survives whole.
        Assert.Equal(message, composed);
    }

    [Fact]
    public void The_icon_stays_when_there_is_room_for_it()
    {
        string composed = StatusLine.Compose("having a nice time", Icon, prefixIcon: true, Line);

        Assert.Equal($"{Icon} having a nice time", composed);
    }

    [Fact]
    public void A_message_too_long_even_without_the_icon_is_cut_and_marked()
    {
        string message = new string('a', 200);

        string composed = StatusLine.Compose(message, Icon, prefixIcon: true, Line);

        Assert.Equal(Line, composed.Length);
        Assert.EndsWith(OscGlyphs.Ellipsis, composed);
    }

    [Fact]
    public void What_the_user_typed_is_never_restyled()
    {
        // This is the one segment whose words are the user's own. Bounding it is fair; shrinking it
        // is not.
        const string message = "GPU is at 99% and I am 100% fine";

        string composed = StatusLine.Compose(message, Icon, prefixIcon: false, Line);

        Assert.Equal(message, composed);
        Assert.All(composed, c => Assert.False(IsRaised(c), $"'{c}' came back raised"));
    }

    [Fact]
    public void The_icon_is_placed_exactly_as_given()
    {
        // Whether a replacement renders has never been tested, so nothing here may swap or strip it.
        string composed = StatusLine.Compose("hello", "🏋️", prefixIcon: true, Line);

        Assert.StartsWith("🏋️", composed);
    }

    [Fact]
    public void Turning_the_icon_off_leaves_no_gap_where_it_was()
    {
        Assert.Equal("hello", StatusLine.Compose("hello", Icon, prefixIcon: false, Line));
        Assert.Equal("hello", StatusLine.Compose("  hello  ", null, prefixIcon: true, Line));
    }

    [Fact]
    public void An_empty_status_produces_nothing_rather_than_a_lone_icon()
    {
        Assert.Equal(string.Empty, StatusLine.Compose("   ", Icon, prefixIcon: true, Line));
        Assert.Equal(string.Empty, StatusLine.Compose(null, Icon, prefixIcon: true, Line));
    }

    [Fact]
    public void No_room_left_means_no_segment()
        => Assert.Equal(string.Empty, StatusLine.Compose("anything", Icon, prefixIcon: true, 0));

    [Fact]
    public void A_cut_never_lands_inside_an_emoji()
    {
        string message = string.Concat(Enumerable.Repeat("🎵", 40));

        for (int budget = 1; budget <= 90; budget++)
        {
            string composed = StatusLine.Compose(message, Icon, prefixIcon: true, budget);

            Assert.True(composed.Length <= budget, $"budget {budget} produced {composed.Length}");
            Assert.False(
                composed.Length > 0 && char.IsHighSurrogate(composed[^1]),
                $"budget {budget} left a dangling high surrogate");
        }
    }

    [Fact]
    public void Every_budget_is_honoured()
    {
        string message = new string('a', 300);

        for (int budget = 0; budget <= Line; budget++)
            Assert.True(
                StatusLine.Compose(message, Icon, prefixIcon: true, budget).Length <= budget,
                $"budget {budget} was exceeded");
    }

    private static bool IsRaised(char c)
        => "abcdefghijklmnopqrstuvwxyz0123456789%".Any(plain =>
            SuperscriptText.TryMap(plain, out char raised) && raised == c);
}
