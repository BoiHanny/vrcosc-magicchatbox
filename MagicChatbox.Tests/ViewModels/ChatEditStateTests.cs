using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;
using Xunit;

namespace MagicChatbox.Tests.ViewModels;

/// <summary>
/// Editing a message that is still showing in VRChat.
/// </summary>
public class ChatEditStateTests
{
    private sealed class StubSettingsProvider<T> : ISettingsProvider<T> where T : class, new()
    {
        public T Value { get; } = new T();
        public void Save() { }
        public void FlushPendingSave() { }
        public void Reload() { }
        public event EventHandler SettingsChanged { add { } remove { } }
    }

    private sealed class FakeAppState : IAppState
    {
        public bool MasterSwitch { get; set; } = true;
        public bool IsVRRunning { get; set; }
        public bool BussyBoysMode { get; set; }
        public bool Egg_Dev { get; set; }
        public bool PulsoidAuthConnected { get; set; }
        public PulsoidAuthState PulsoidAuthState { get; set; }
        public int MainWindowBlurEffect { get; set; }
        public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
    }

    /// <summary>
    /// Live typing is wired up when the page's view model is built, so it cannot be one of the
    /// collaborators below that throw on touch. Editing a message must still not reach it.
    /// </summary>
    private sealed class SilentLiveTyping : ILiveTypingService
    {
        public event Action? FinalizeRequested { add { } remove { } }

        public bool IsHolding { get; set; }

        public int Interactions { get; private set; }

        public void Show(string text) => Interactions++;

        public void Release(bool clearChatbox) => Interactions++;
    }

    private readonly SilentLiveTyping _liveTyping = new();
    private readonly ChatStatusDisplayState _chatStatus = new();
    private readonly StubSettingsProvider<ChatSettings> _chatSettings = new();
    private readonly ChattingPageViewModel _vm;

    public ChatEditStateTests()
    {
        // Only the edit state machine is under test here and it touches nothing but the message and
        // the chat settings, so the collaborators behind it are never resolved.
        _vm = new ChattingPageViewModel(
            _chatStatus,
            new FakeAppState(),
            Unused<IModuleHost>(),
            _chatSettings,
            new StubSettingsProvider<TtsSettings>(),
            Unused<ScanLoopService>(),
            Unused<OSCController>(),
            Unused<IChatHistoryService>(),
            Unused<IAudioService>(),
            Unused<IOscSender>(),
            Unused<ITtsPlaybackService>(),
            new Lazy<ILiveTypingService>(() => _liveTyping),
            null!,
            null!);
    }

    private static Lazy<T> Unused<T>() where T : class
        => new(() => throw new InvalidOperationException(typeof(T).Name + " should not be needed to edit a message."));

    private ChatItem Running(string message)
    {
        var item = new ChatItem(_chatStatus)
        {
            Msg = message,
            MainMsg = message,
            IsRunning = true,
            CanLiveEdit = true,
            Opacity = "1",
        };

        _chatStatus.LastMessages.Add(item);
        return item;
    }

    [Fact]
    public void Opening_an_edit_leaves_the_caret_a_space_to_carry_on_from()
    {
        var item = Running("hello");

        _vm.BeginChatEdit(item);

        Assert.Equal("hello ", item.MsgReplace);
    }

    [Fact]
    public void Closing_an_edit_untouched_does_not_smuggle_that_space_into_the_message()
    {
        // The space is scaffolding for the edit box. Committing it spends a character of the 141 on
        // something nobody can see, and makes an unchanged message look changed.
        var item = Running("hello");
        _vm.BeginChatEdit(item);

        _vm.ConfirmChatEdit(item);

        Assert.Equal("hello", item.Msg);
        Assert.Equal("hello", item.MainMsg);
    }

    [Fact]
    public void Confirming_an_edit_puts_the_new_text_on_the_message()
    {
        var item = Running("hello");
        _vm.BeginChatEdit(item);
        item.MsgReplace = "hello there ";

        _vm.ConfirmChatEdit(item);

        Assert.Equal("hello there", item.Msg);
        Assert.Equal("hello there", item.MainMsg);
        Assert.False(item.CanLiveEditRun);
    }

    [Fact]
    public void Closing_an_edit_puts_the_row_back_to_the_opacity_it_had()
    {
        var item = Running("hello");
        item.Opacity = "0.68";
        _vm.BeginChatEdit(item);
        Assert.Equal("1", item.Opacity);

        _vm.ConfirmChatEdit(item);

        Assert.Equal("0.68", item.Opacity);
    }

    [Fact]
    public void Escape_puts_the_message_back_the_way_it_was()
    {
        _chatSettings.Value.RealTimeChatEdit = true;
        var item = Running("hello");
        _vm.BeginChatEdit(item);

        _vm.HandleEditTextChanged(item, "hello wor");
        Assert.Equal("hello wor", item.Msg);

        _vm.HandleEditEscape(item);
        _vm.ConfirmChatEdit(item);

        Assert.Equal("hello", item.Msg);
        Assert.False(item.CancelLiveEdit);
    }

    [Fact]
    public void An_edit_never_lands_on_a_message_other_than_the_one_that_was_opened()
    {
        // Only the running message is editable, so in practice these are the same row. The guard is
        // for when they are not: silently rewriting a different message is the worst thing this
        // could do, and it is invisible when it happens.
        var running = Running("the live one");
        var old = new ChatItem(_chatStatus) { Msg = "an older one", MainMsg = "an older one" };
        _chatStatus.LastMessages.Add(old);

        Assert.False(_vm.HandleEditEnter(old, "rewritten"));

        Assert.Equal("an older one", old.Msg);
        Assert.Equal("the live one", running.Msg);
    }

    [Fact]
    public void Enter_commits_to_the_message_it_was_typed_in()
    {
        var item = Running("hello");
        _vm.BeginChatEdit(item);

        Assert.True(_vm.HandleEditEnter(item, "hello there "));

        Assert.Equal("hello there", item.Msg);
        Assert.False(item.CanLiveEditRun);
    }

    [Fact]
    public void The_edit_button_only_promises_live_when_edits_really_go_out_live()
    {
        Assert.Equal("Live edit", ChatStateManager.EditLabel(new ChatSettings { RealTimeChatEdit = true }));
        Assert.Equal("Edit", ChatStateManager.EditLabel(new ChatSettings { RealTimeChatEdit = false }));
    }

    [Fact]
    public void The_newest_message_is_never_pre_faded_and_the_oldest_stays_readable()
    {
        // The ladder used to start at 0.82 and run down to 0.10, and the row applied it twice - so
        // a message arrived at 0.67 and the bottom of the list rendered at 0.01.
        var messages = Enumerable.Range(0, 5)
            .Select(_ => new ChatItem(_chatStatus))
            .ToList();

        ChatStateManager.FadeOlderMessages(messages);

        var values = messages
            .Select(m =>
            {
                string? opacity = m.Opacity;
                Assert.NotNull(opacity);
                return double.Parse(opacity, CultureInfo.InvariantCulture);
            })
            .ToList();

        Assert.Equal(1.0, values[^1]);
        Assert.True(values[0] >= 0.3, "the oldest message faded to " + values[0]);
        Assert.True(values.SequenceEqual(values.OrderBy(v => v)), "the fade is not monotonic: " + string.Join(", ", values));
    }

    [Fact]
    public void Fading_a_single_message_leaves_it_alone()
    {
        var only = new List<ChatItem> { new(_chatStatus) };

        ChatStateManager.FadeOlderMessages(only);

        string? opacity = only[0].Opacity;
        Assert.NotNull(opacity);
        Assert.Equal(1.0, double.Parse(opacity, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Sending_the_next_message_does_not_blank_the_one_being_edited()
    {
        // The exact order the UI tears an open edit down in when a new message is sent. The last two
        // steps are what the markup does on its own: clearing the scratch buffer empties the edit
        // box, and the box reports that as a text change.
        _chatSettings.Value.RealTimeChatEdit = true;
        var item = Running("hello");
        _vm.BeginChatEdit(item);
        item.CanLiveEditRun = true;

        item.CanLiveEdit = false;
        item.CanLiveEditRun = false;
        _vm.ConfirmChatEdit(item);
        item.MsgReplace = string.Empty;
        _vm.HandleEditTextChanged(item, string.Empty);
        item.IsRunning = false;

        Assert.Equal("hello", item.Msg);
        Assert.Equal("hello", item.MainMsg);
    }

    [Fact]
    public void An_edit_committed_empty_leaves_the_message_alone()
    {
        // The backstop for the same failure: if the commit ever runs after the scratch buffer has
        // been cleared rather than before, it must not take that for "make this message blank".
        var item = Running("hello");
        _vm.BeginChatEdit(item);
        item.MsgReplace = "   ";

        _vm.ConfirmChatEdit(item);

        Assert.Equal("hello", item.Msg);
        Assert.Equal("hello", item.MainMsg);
    }

    [Fact]
    public void A_closed_edit_box_cannot_rewrite_the_message()
    {
        // Nothing types into an edit box that is not open. A text change arriving then is the markup
        // resetting itself, and taking it for a person's edit is how the message ended up blank.
        _chatSettings.Value.RealTimeChatEdit = true;
        var item = Running("hello");
        item.CanLiveEditRun = false;

        _vm.HandleEditTextChanged(item, "not typed by anyone");

        Assert.Equal("hello", item.Msg);
    }

    [Fact]
    public void An_open_edit_box_still_rewrites_as_it_is_typed()
    {
        _chatSettings.Value.RealTimeChatEdit = true;
        var item = Running("hello");
        _vm.BeginChatEdit(item);
        item.CanLiveEditRun = true;

        _vm.HandleEditTextChanged(item, "hello there");

        Assert.Equal("hello there", item.Msg);
    }

    [Fact]
    public void Editing_a_message_never_touches_the_chatbox()
    {
        // Editing rewrites a message that is already out there; the update goes through the running
        // message, not by shoving text at the chatbox behind the send path's back.
        var item = Running("hello");

        _vm.BeginChatEdit(item);
        _vm.HandleEditTextChanged(item, "hello there");
        _vm.HandleEditEnter(item, "hello there");
        _vm.ConfirmChatEdit(item);

        Assert.Equal(0, _liveTyping.Interactions);
    }

    [Theory]
    [InlineData(true, true, true, "", "an empty box")]
    [InlineData(true, true, true, "   ", "a box holding only spaces")]
    [InlineData(true, true, false, "real words", "a line that never reached VRChat")]
    [InlineData(true, false, true, "real words", "finishing on its own switched off")]
    [InlineData(false, true, true, "real words", "live typing switched off")]
    public void Nothing_is_posted_when_there_is_no_live_line_to_finish(
        bool liveTyping, bool autoFinalize, bool holding, string text, string because)
    {
        // FinishLiveLine fires on a timer and on losing focus, so it runs constantly in situations
        // where there is nothing to send. Every collaborator behind the send path throws on touch,
        // so reaching it at all fails this - which is the point: an accidental send is a message to
        // other people that nobody asked for.
        _chatSettings.Value.ChatLiveTyping = liveTyping;
        _chatSettings.Value.ChatLiveTypingAutoFinalize = autoFinalize;
        _liveTyping.IsHolding = holding;
        _chatStatus.NewChattingTxt = text;

        _vm.FinishLiveLine();

        Assert.True(_chatStatus.LastMessages.Count == 0, "something was posted for " + because);
    }
}
