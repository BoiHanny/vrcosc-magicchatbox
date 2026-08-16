using MagicChatbox.Tests.TestDoubles;
using System;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Core.Vrc;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.State;
using Xunit;

namespace MagicChatbox.Tests.Core.Vrc;

// Routing every avatar write through the sink was supposed to change the code and nothing else.
// These assert that on real bytes off a real socket, because "it still compiles" is not evidence
// that somebody's avatar still reacts.
public class AvatarParameterWireTests : IDisposable
{
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

    private readonly UdpClient _listener;
    private readonly OscSenderService _sender;
    private readonly AvatarParameterRouter _router;

    public AvatarParameterWireTests()
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
            new OscDisplayState(),
            new TtsAudioDisplayState(ttsSettings));

        _router = new AvatarParameterRouter(_sender, () => null);
    }

    public void Dispose()
    {
        _sender.Dispose();
        _listener.Dispose();
    }

    private async Task<string> ReceiveAsync()
    {
        Task<UdpReceiveResult> receive = _listener.ReceiveAsync();
        Task completed = await Task.WhenAny(receive, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.True(completed == receive, "No OSC datagram arrived within 5 seconds.");
        return Encoding.UTF8.GetString((await receive).Buffer);
    }

    [Fact]
    public async Task A_bare_name_becomes_the_full_avatar_parameter_address()
    {
        _router.Set("MCB_Heartrate_Hot", true);

        Assert.Contains("/avatar/parameters/MCB_Heartrate_Hot", await ReceiveAsync());
    }

    [Fact]
    public async Task A_name_that_is_already_an_address_is_left_alone()
    {
        // The camera flash parameter has always been stored as a whole address and is user editable,
        // so double-prefixing it would silently break every existing setup.
        _router.Set("/avatar/parameters/CameraFlash", true);

        string payload = await ReceiveAsync();

        Assert.Contains("/avatar/parameters/CameraFlash", payload);
        Assert.DoesNotContain("/avatar/parameters//avatar", payload);
    }

    [Fact]
    public async Task A_bool_still_goes_out_as_an_int_exactly_as_before()
    {
        // Shipping avatars are bound to this encoding. The type tag says int, and changing it to a
        // real OSC boolean is a wire-format change that needs its own compatibility switch.
        _router.Set("MCB_Heartrate_Hot", true);

        Assert.Contains(",i", await ReceiveAsync());
    }

    [Fact]
    public async Task An_int_goes_out_as_an_int()
    {
        _router.Set("HR", 72);

        Assert.Contains(",i", await ReceiveAsync());
    }

    [Fact]
    public async Task A_float_goes_out_as_a_float()
    {
        _router.Set("HRPercent", 0.5f);

        Assert.Contains(",f", await ReceiveAsync());
    }

    [Fact]
    public async Task A_pulse_sends_true_and_then_false()
    {
        _router.Pulse("CameraFlash", 30);

        string first = await ReceiveAsync();
        string second = await ReceiveAsync();

        Assert.Contains("/avatar/parameters/CameraFlash", first);
        Assert.Contains("/avatar/parameters/CameraFlash", second);
    }

    [Fact]
    public async Task An_empty_name_sends_nothing()
    {
        _router.Set(string.Empty, true);
        _router.Set("   ", 1);

        Task<UdpReceiveResult> receive = _listener.ReceiveAsync();
        Task completed = await Task.WhenAny(receive, Task.Delay(TimeSpan.FromMilliseconds(300)));

        Assert.True(completed != receive, "an empty parameter name reached the wire");
    }
}
