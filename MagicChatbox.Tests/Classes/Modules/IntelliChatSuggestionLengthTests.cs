using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules;

public class IntelliChatSuggestionLengthTests
{
    // The chat send gate's own test, copied verbatim from ChattingPageViewModel.TrySendChatText.
    // Anything it rejects is dropped without a message, so a suggestion must never fail it.
    private static bool SendGateAccepts(string chat) => chat.Length <= Constants.MaxChatMessageLength;

    [Theory]
    [InlineData(142)]
    [InlineData(143)]
    [InlineData(160)]
    [InlineData(400)]
    public void A_trimmed_suggestion_survives_the_send_gate(int inputLength)
    {
        // The trim used to target 140 and then append three dots, landing on 143 - two over the
        // gate - so every long suggestion was built only to be thrown away in silence.
        string fitted = IntelliChatModule.FitToChatLimit(new string('a', inputLength));

        Assert.True(SendGateAccepts(fitted), $"{fitted.Length} characters is over the gate");
        Assert.EndsWith("…", fitted);
    }

    [Fact]
    public void The_trim_marker_costs_one_character_not_three()
    {
        string fitted = IntelliChatModule.FitToChatLimit(new string('a', 300));

        Assert.Equal(Constants.MaxChatMessageLength, fitted.Length);
        Assert.DoesNotContain("...", fitted);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(140)]
    [InlineData(141)]
    public void A_suggestion_that_already_fits_is_handed_over_untouched(int inputLength)
    {
        string input = new string('a', inputLength);

        Assert.Equal(input, IntelliChatModule.FitToChatLimit(input));
    }

    [Fact]
    public void A_surrogate_pair_is_never_cut_in_half()
    {
        // A lone surrogate renders as a replacement box in the chatbox. Place the pair so the
        // cut lands between its two halves.
        string input = new string('a', Constants.MaxChatMessageLength - 2) + "\U0001F600" + new string('b', 20);

        string fitted = IntelliChatModule.FitToChatLimit(input);

        Assert.True(SendGateAccepts(fitted));
        Assert.DoesNotContain(fitted, c => char.IsSurrogate(c));
    }

    [Fact]
    public void A_surrogate_pair_that_fits_whole_is_kept_whole()
    {
        string input = new string('a', Constants.MaxChatMessageLength - 3) + "\U0001F600" + new string('b', 20);

        string fitted = IntelliChatModule.FitToChatLimit(input);

        Assert.True(SendGateAccepts(fitted));
        Assert.Contains("\U0001F600", fitted);
    }

    [Fact]
    public void A_cut_that_lands_on_a_full_stop_does_not_leave_a_four_dot_tail()
    {
        string input = new string('a', Constants.MaxChatMessageLength - 4) + "... tail that pushes it over the limit";

        string fitted = IntelliChatModule.FitToChatLimit(input);

        Assert.True(SendGateAccepts(fitted));
        Assert.DoesNotContain(".…", fitted);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Nothing_in_gives_nothing_out(string? input)
    {
        Assert.Equal(string.Empty, IntelliChatModule.FitToChatLimit(input));
    }

    [Fact]
    public void The_gate_leaves_exactly_enough_room_for_the_emoji_prefix()
    {
        // ChatStateManager prepends a non-BMP emoji plus a space before checking the 144 OSC
        // budget, so the chat limit has to be three under it or a full-length message is dropped.
        Assert.Equal(Constants.OscMaxMessageLength - 3, Constants.MaxChatMessageLength);
    }
}
