using System;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.State;
using Xunit;

namespace MagicChatbox.Tests.Services;

public class OscSenderExplicitTextTests : IDisposable
{
    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }

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
        public vrcosc_magicchatbox.ViewModels.State.PulsoidAuthState PulsoidAuthState { get; set; }
        public int MainWindowBlurEffect { get; set; }
        public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
    }

    private readonly UdpClient _listener;
    private readonly OscDisplayState _oscDisplay = new();
    private readonly OscSenderService _sender;

    public OscSenderExplicitTextTests()
    {
        _listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int port = ((IPEndPoint)_listener.Client.LocalEndPoint!).Port;

        var oscSettings = new StubSettingsProvider<OscSettings>();
        oscSettings.Value.OscIP = "127.0.0.1";
        oscSettings.Value.OscPortOut = port;

        var ttsSettings = new StubSettingsProvider<TtsSettings>();

        _sender = new OscSenderService(
            oscSettings,
            new StubSettingsProvider<AppSettings>(),
            ttsSettings,
            new FakeAppState(),
            new ChatStatusDisplayState(),
            _oscDisplay,
            new TtsAudioDisplayState(ttsSettings));
    }

    public void Dispose()
    {
        _sender.Dispose();
        _listener.Dispose();
    }

    private async Task<string> ReceiveTextAsync()
    {
        Task<UdpReceiveResult> receive = _listener.ReceiveAsync();
        Task completed = await Task.WhenAny(receive, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.True(completed == receive, "No OSC datagram arrived within 5 seconds.");
        return Encoding.UTF8.GetString((await receive).Buffer);
    }

    [Fact]
    public async Task Explicit_text_is_transmitted_even_when_the_shared_preview_differs()
    {
        _oscDisplay.OscToSent = "integration status line";

        Assert.True(await _sender.SendOSCMessage(false, explicitText: "the chat message"));

        string payload = await ReceiveTextAsync();
        Assert.Contains("the chat message", payload);
        Assert.DoesNotContain("integration status line", payload);
    }

    [Fact]
    public async Task Without_explicit_text_the_shared_preview_is_transmitted()
    {
        _oscDisplay.OscToSent = "integration status line";

        Assert.True(await _sender.SendOSCMessage(false));

        string payload = await ReceiveTextAsync();
        Assert.Contains("integration status line", payload);
    }

    [Fact]
    public async Task A_silent_send_tells_VRChat_not_to_play_the_notification()
    {
        // The third argument of /chatbox/input is the notification SFX, and it is the only thing
        // standing between live typing and a chime once a second for as long as someone is writing.
        // Asserting it on the wire rather than on the call: this is what VRChat actually reads.
        Assert.True(await _sender.SendOSCMessage(fx: false, explicitText: "still writing this"));

        Assert.Contains(",sTF", await ReceiveTextAsync());
    }

    [Fact]
    public async Task A_real_send_still_asks_for_it()
    {
        Assert.True(await _sender.SendOSCMessage(fx: true, explicitText: "and that is the message"));

        Assert.Contains(",sTT", await ReceiveTextAsync());
    }

    [Fact]
    public async Task Duplicate_suppression_tracks_the_text_that_was_actually_sent()
    {
        Assert.True(await _sender.SendOSCMessage(false, explicitText: "hello there"));
        await ReceiveTextAsync();

        _oscDisplay.OscToSent = "hello there";
        Assert.False(await _sender.SendOSCMessage(false));

        Assert.True(await _sender.SendOSCMessage(false, force: true));
        await ReceiveTextAsync();
    }

    [Fact]
    public async Task Failed_hostnames_are_retried_after_the_cooldown()
    {
        var clock = new ManualTimeProvider();
        var oscSettings = new StubSettingsProvider<OscSettings>();
        oscSettings.Value.OscIP = "does-not-exist.invalid";
        oscSettings.Value.OscPortOut = 9000;
        var ttsSettings = new StubSettingsProvider<TtsSettings>();
        using var sender = new OscSenderService(
            oscSettings,
            new StubSettingsProvider<AppSettings>(),
            ttsSettings,
            new FakeAppState(),
            new ChatStatusDisplayState(),
            new OscDisplayState(),
            new TtsAudioDisplayState(ttsSettings),
            clock);
        FieldInfo? retryAfterField = typeof(OscSenderService).GetField(
            "_lastFailedEndpointRetryAfterUtc",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(retryAfterField);
        Assert.False(await sender.SendOSCMessage(false, force: true, explicitText: "first"));
        var firstRetryAfter = (DateTimeOffset)retryAfterField!.GetValue(sender)!;

        Assert.False(await sender.SendOSCMessage(false, force: true, explicitText: "blocked"));
        Assert.Equal(firstRetryAfter, retryAfterField.GetValue(sender));

        clock.Advance(TimeSpan.FromSeconds(31));
        Assert.False(await sender.SendOSCMessage(false, force: true, explicitText: "retried"));

        Assert.True((DateTimeOffset)retryAfterField.GetValue(sender)! > firstRetryAfter);
    }
}
