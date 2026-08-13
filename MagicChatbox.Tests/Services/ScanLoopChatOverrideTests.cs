using System.Collections.Generic;
using System.Collections.ObjectModel;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;
using Xunit;

namespace MagicChatbox.Tests.Services;

public class ScanLoopChatOverrideTests
{
    private static ChatItem Item(bool isRunning) =>
        new ChatItem(new ChatStatusDisplayState()) { IsRunning = isRunning };

    [Fact]
    public void Pause_with_a_running_chat_claims_the_box()
    {
        Assert.True(ScanLoopService.IsChatOverrideActive(true, new[] { Item(false), Item(true) }));
    }

    [Fact]
    public void Pause_without_a_running_chat_does_not_block_the_scan()
    {
        Assert.False(ScanLoopService.IsChatOverrideActive(true, new[] { Item(false), Item(false) }));
    }

    [Fact]
    public void A_running_chat_without_pause_does_not_block_the_scan()
    {
        Assert.False(ScanLoopService.IsChatOverrideActive(false, new[] { Item(true) }));
    }

    [Fact]
    public void Missing_or_empty_history_never_blocks()
    {
        Assert.False(ScanLoopService.IsChatOverrideActive(true, null));
        Assert.False(ScanLoopService.IsChatOverrideActive(true, new List<ChatItem>()));
    }

    [Fact]
    public void Works_against_the_live_collection_type()
    {
        var messages = new ObservableCollection<ChatItem> { Item(true) };

        Assert.True(ScanLoopService.IsChatOverrideActive(true, messages));

        messages[0].IsRunning = false;
        Assert.False(ScanLoopService.IsChatOverrideActive(true, messages));
    }
}
