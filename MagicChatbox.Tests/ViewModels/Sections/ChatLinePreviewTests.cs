using vrcosc_magicchatbox.ViewModels.Sections;
using Xunit;

namespace MagicChatbox.Tests.ViewModels.Sections;

/// <summary>
/// The preview has to agree with EmojiService.GetNextEmoji about which icon goes out, or it is
/// worse than showing nothing: it teaches the reader the wrong thing about their own settings.
/// </summary>
public class ChatLinePreviewTests
{
    private static readonly string[] Mine = ["🌙", "🍰", "🛸"];

    [Fact]
    public void No_prefix_means_the_message_and_nothing_else()
    {
        string line = ChatLinePreview.Build(prefixIcon: false, shuffleEnabled: true, shuffleInChats: true, Mine, "hello");

        Assert.Equal("hello", line);
    }

    [Fact]
    public void The_icon_sits_in_front_with_one_space_the_way_the_sender_builds_it()
    {
        string line = ChatLinePreview.Build(prefixIcon: true, shuffleEnabled: false, shuffleInChats: false, Mine, "hello");

        Assert.Equal("💬 hello", line);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Without_both_shuffle_switches_the_stock_icon_is_what_goes_out(bool shuffleEnabled, bool shuffleInChats)
    {
        // GetNextEmoji only reaches the user's own icons when the shuffle is on AND, for chat,
        // switched on for chat as well. Anything short of that falls back to the stock icon.
        Assert.Equal("💬", ChatLinePreview.ResolveIcon(shuffleEnabled, shuffleInChats, Mine));
    }

    [Fact]
    public void With_both_switches_on_it_stands_in_the_first_of_your_own_icons()
    {
        Assert.Equal("🌙", ChatLinePreview.ResolveIcon(shuffleEnabled: true, shuffleInChats: true, Mine));
    }

    [Theory]
    [InlineData()]
    [InlineData("", "  ")]
    public void An_empty_or_blank_icon_list_falls_back_rather_than_previewing_nothing(params string[] icons)
    {
        // EmojiService does the same: an empty collection is answered with the stock icon, so a
        // preview showing a bare message here would be a lie.
        Assert.Equal("💬", ChatLinePreview.ResolveIcon(shuffleEnabled: true, shuffleInChats: true, icons));
    }

    [Fact]
    public void A_null_list_is_survivable()
    {
        Assert.Equal("💬", ChatLinePreview.ResolveIcon(shuffleEnabled: true, shuffleInChats: true, icons: null));
    }

    [Fact]
    public void The_sample_message_is_short_enough_to_leave_the_reader_room_to_judge()
    {
        // A sample that filled the line would put the chip in the red on a fresh install and tell
        // the reader their settings are the problem.
        Assert.True(ChatLinePreview.SampleMessage.Length < 40, ChatLinePreview.SampleMessage);
    }
}
