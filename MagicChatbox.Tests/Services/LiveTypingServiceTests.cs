using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.State;
using Xunit;

namespace MagicChatbox.Tests.Services;

/// <summary>
/// Live typing: the rate limiter that decides what of an unfinished line reaches VRChat.
/// </summary>
public class LiveTypingServiceTests : IDisposable
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

    private sealed class RecordingSender : IOscSender
    {
        private readonly Lock _gate = new();
        private readonly List<string> _sent = [];

        public int Clears { get; private set; }
        public bool AnySoundRequested { get; private set; }

        public IReadOnlyList<string> Sent
        {
            get { lock (_gate) return _sent.ToArray(); }
        }

        public Task<bool> SendOSCMessage(bool fx, int delay = 0, bool force = false, string? explicitText = null)
        {
            lock (_gate)
            {
                if (fx) AnySoundRequested = true;
                _sent.Add(explicitText ?? string.Empty);
            }

            return Task.FromResult(true);
        }

        public void SendOscParam(string address, float value) { }
        public void SendOscParam(string address, int value) { }
        public void SendOscParam(string address, bool value) { }
        public void SendTypingIndicatorAsync() { }
        public void StopTypingIndicator() { }

        public Task SentClearMessage(int delay)
        {
            lock (_gate) Clears++;
            return Task.CompletedTask;
        }

        public Task ToggleVoice(bool force = false) => Task.CompletedTask;
    }

    private readonly StubSettingsProvider<ChatSettings> _settings = new();
    private readonly FakeAppState _appState = new();
    private readonly RecordingSender _sender = new();
    private readonly OscDisplayState _oscDisplay = new();
    private readonly LiveTypingService _live;

    public LiveTypingServiceTests()
    {
        _settings.Value.ChatLiveTyping = true;
        _settings.Value.ChatLiveTypingRateMs = ChatSettings.ChatLiveTypingRateMinMs;

        _live = new LiveTypingService(
            _settings,
            _appState,
            new Lazy<IOscSender>(() => _sender),
            _oscDisplay);
    }

    public void Dispose() => _live.Dispose();

    [Fact]
    public void Nothing_goes_out_while_the_feature_is_off()
    {
        _settings.Value.ChatLiveTyping = false;

        _live.Show("hello");

        Assert.False(_live.IsHolding);
        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public void The_first_keystroke_goes_straight_out()
    {
        _live.Show("h");

        Assert.True(_live.IsHolding);
        Assert.Equal(["h"], _sender.Sent);
    }

    [Fact]
    public void A_burst_of_keystrokes_collapses_to_one_push()
    {
        _live.Show("h");
        _live.Show("he");
        _live.Show("hel");
        _live.Show("hell");
        _live.Show("hello");

        // Only the first is out. The rest are waiting on the interval - which is the whole point,
        // because VRChat would throw away a push per keystroke.
        Assert.Equal(["h"], _sender.Sent);
    }

    [Fact]
    public async Task What_was_typed_during_the_wait_is_not_lost()
    {
        _live.Show("h");
        _live.Show("hello there");

        // The trailing push is what the person is left looking at. Without it the chatbox would sit
        // on "h" forever, because nothing else is coming once they stop typing.
        await WaitForSendCount(2);

        Assert.Equal(["h", "hello there"], _sender.Sent);
    }

    [Fact]
    public async Task Typing_the_same_thing_again_does_not_re_send_it()
    {
        _live.Show("hello");
        _live.Show("hello ");

        await Task.Delay(ChatSettings.ChatLiveTypingRateMinMs + 400);

        // "hello " trims to "hello", which is already showing. Nothing new to say.
        Assert.Equal(["hello"], _sender.Sent);
    }

    [Fact]
    public void Emptying_the_box_hands_the_chatbox_back()
    {
        _live.Show("hello");
        _live.Show(string.Empty);

        Assert.False(_live.IsHolding);
        Assert.Equal(1, _sender.Clears);
        Assert.Equal(string.Empty, _oscDisplay.OscToSent);
    }

    [Fact]
    public void Releasing_for_a_send_does_not_wipe_what_is_about_to_replace_it()
    {
        _live.Show("hello");

        _live.Release(clearChatbox: false);

        Assert.False(_live.IsHolding);
        Assert.Equal(0, _sender.Clears);
    }

    [Fact]
    public void Switching_the_feature_off_takes_the_half_written_line_down()
    {
        _live.Show("half a thought");

        _settings.Value.ChatLiveTyping = false;

        Assert.False(_live.IsHolding);
        Assert.Equal(1, _sender.Clears);
    }

    [Fact]
    public void The_master_switch_still_wins()
    {
        _appState.MasterSwitch = false;

        _live.Show("hello");

        Assert.False(_live.IsHolding);
        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public void Turning_sending_off_mid_line_does_not_park_the_integrations_forever()
    {
        _live.Show("hello");
        Assert.True(_live.IsHolding);

        _appState.MasterSwitch = false;

        // The hold is only ever refreshed by a keystroke. If it survived the master switch, someone
        // who stopped typing and turned sending off would come back to a chatbox their integrations
        // could never reach again.
        Assert.False(_live.IsHolding);
    }

    [Fact]
    public void The_notification_sound_is_never_asked_for()
    {
        _live.Show("hello");

        // Once a second, for as long as someone is writing a sentence.
        Assert.False(_sender.AnySoundRequested);
    }

    [Fact]
    public void A_line_longer_than_the_chatbox_is_clipped_rather_than_dropped()
    {
        string tooLong = new('x', vrcosc_magicchatbox.Core.Constants.OscMaxMessageLength + 20);

        _live.Show(tooLong);

        Assert.Single(_sender.Sent);
        Assert.Equal(vrcosc_magicchatbox.Core.Constants.OscMaxMessageLength, _sender.Sent[0].Length);
    }

    [Fact]
    public void The_rate_cannot_be_set_below_what_VRChat_accepts()
    {
        var settings = new ChatSettings { ChatLiveTypingRateMs = 1 };
        Assert.Equal(ChatSettings.ChatLiveTypingRateMinMs, settings.ChatLiveTypingRateMs);

        settings.ChatLiveTypingRateMs = 100_000;
        Assert.Equal(ChatSettings.ChatLiveTypingRateMaxMs, settings.ChatLiveTypingRateMs);
    }

    [Fact]
    public void The_fastest_allowed_rate_stays_inside_the_chatbox_meter()
    {
        // VRChat meters the chatbox and puts you on a cooldown for going over. Its sustained
        // allowance works out at about a message a second, so a floor below that would eventually
        // silence the chatbox rather than merely stutter.
        Assert.True(
            ChatSettings.ChatLiveTypingRateMinMs >= 1000,
            $"a {ChatSettings.ChatLiveTypingRateMinMs}ms floor is {5000.0 / ChatSettings.ChatLiveTypingRateMinMs:N1} messages every 5 seconds");
    }

    [Fact]
    public async Task Stopping_typing_asks_for_the_line_to_be_finished()
    {
        _settings.Value.ChatLiveTypingFinalizeMs = ChatSettings.ChatLiveTypingFinalizeMinMs;
        int asked = 0;
        _live.FinalizeRequested += () => Interlocked.Increment(ref asked);

        _live.Show("all done");
        await WaitFor(() => Volatile.Read(ref asked) > 0, ChatSettings.ChatLiveTypingFinalizeMinMs + 2000);

        Assert.Equal(1, Volatile.Read(ref asked));
    }

    [Fact]
    public async Task Carrying_on_typing_pushes_the_finish_back()
    {
        _settings.Value.ChatLiveTypingFinalizeMs = ChatSettings.ChatLiveTypingFinalizeMinMs;
        int asked = 0;
        _live.FinalizeRequested += () => Interlocked.Increment(ref asked);

        // Keep typing across what would otherwise have been the deadline. A pause is measured from
        // the last thing typed, or a slow sentence would be cut in half while it was being written.
        _live.Show("still");
        await Task.Delay(ChatSettings.ChatLiveTypingFinalizeMinMs / 2);
        _live.Show("still going");
        await Task.Delay(ChatSettings.ChatLiveTypingFinalizeMinMs / 2 + 200);

        Assert.Equal(0, Volatile.Read(ref asked));
    }

    [Fact]
    public async Task A_line_that_was_sent_is_not_finished_again()
    {
        _settings.Value.ChatLiveTypingFinalizeMs = ChatSettings.ChatLiveTypingFinalizeMinMs;
        int asked = 0;
        _live.FinalizeRequested += () => Interlocked.Increment(ref asked);

        _live.Show("sent by hand");
        _live.Release(clearChatbox: false);

        await Task.Delay(ChatSettings.ChatLiveTypingFinalizeMinMs + 500);

        Assert.Equal(0, Volatile.Read(ref asked));
    }

    [Fact]
    public async Task Nothing_finishes_on_its_own_when_that_is_switched_off()
    {
        _settings.Value.ChatLiveTypingAutoFinalize = false;
        _settings.Value.ChatLiveTypingFinalizeMs = ChatSettings.ChatLiveTypingFinalizeMinMs;
        int asked = 0;
        _live.FinalizeRequested += () => Interlocked.Increment(ref asked);

        _live.Show("this one waits for Enter");
        await Task.Delay(ChatSettings.ChatLiveTypingFinalizeMinMs + 500);

        Assert.Equal(0, Volatile.Read(ref asked));
        Assert.True(_live.IsHolding);
    }

    [Fact]
    public void The_pause_that_counts_as_finished_cannot_be_a_thinking_pause()
    {
        var settings = new ChatSettings { ChatLiveTypingFinalizeMs = 1 };
        Assert.Equal(ChatSettings.ChatLiveTypingFinalizeMinMs, settings.ChatLiveTypingFinalizeMs);

        settings.ChatLiveTypingFinalizeMs = 100_000;
        Assert.Equal(ChatSettings.ChatLiveTypingFinalizeMaxMs, settings.ChatLiveTypingFinalizeMs);

        Assert.True(ChatSettings.ChatLiveTypingFinalizeMinMs >= 2000);
    }

    private static async Task WaitFor(Func<bool> condition, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(25);
    }

    private async Task WaitForSendCount(int count)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (_sender.Sent.Count < count && DateTime.UtcNow < deadline)
            await Task.Delay(25);
    }
}
