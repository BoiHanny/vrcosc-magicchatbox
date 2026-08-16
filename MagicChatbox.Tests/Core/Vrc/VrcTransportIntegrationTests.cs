using MagicChatbox.Vrc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Vrc;
using Xunit;

namespace MagicChatbox.Tests.Core.Vrc;

// Everything else about the bridge is tested against fakes. This drives the real transport on this
// machine: a real UDP socket, a real HTTP server, the real OSCQuery tree, and the real ingress and
// guard chain. VRChat itself is the only piece still standing in - a hand-built datagram takes its
// place, which is exactly what VRChat would put on the wire.
//
// If mDNS is blocked on the machine running these (firewall, VPN adapter, no multicast), discovery
// cannot work but binding and serving still do, so these assert the parts that do not need a peer.
public class VrcTransportIntegrationTests : IAsyncLifetime
{
    private VrcTransport? _transport;
    private CancellationTokenSource? _cts;
    private Task? _run;
    private AvatarCommandReceiver _receiver = null!;
    private readonly List<string> _fired = [];

    public Task InitializeAsync()
    {
        var settings = new VrcBridgeSettings { EnableBridge = true, EnableParameterInput = true };

        var command = new InboundCommand(
            "MCB/Ctrl/Tts/Stop", InboundTrigger.RisingEdge, InboundRisk.Safe, "test",
            _ => { lock (_fired) _fired.Add("stop"); })
        {
            MinInterval = TimeSpan.Zero,
        };

        _receiver = new AvatarCommandReceiver(new[] { command }, () => true, action => action());

        _transport = VrcTransport.Create(
            new AppWorldPolicy(settings, () => null, () => false),
            new AppProfanityPolicy(settings),
            observations: _receiver,
            options: new VrcTransportOptions
            {
                ServiceName = "MagicChatbox.Tests",
                Address = IPAddress.Loopback,
                OscReceivePort = 0,
            });

        _cts = new CancellationTokenSource();
        _run = Task.Run(async () =>
        {
            try
            {
                await _transport.RunAsync(_cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                // mDNS may be unavailable on a build agent. The binding assertions below still hold.
            }
        });

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _cts?.Cancel();

        if (_run != null)
        {
            try { await _run.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (TimeoutException) { }
        }

        _transport?.Dispose();
        _cts?.Dispose();
    }

    private async Task<bool> Eventually(Func<bool> condition, int timeoutMs = 10000)
    {
        var clock = Stopwatch.StartNew();

        while (clock.ElapsedMilliseconds < timeoutMs)
        {
            if (condition())
                return true;

            await Task.Delay(25);
        }

        return condition();
    }

    [Fact]
    public async Task The_transport_binds_a_port_the_operating_system_chose()
    {
        // Never 9001. That is the whole reason this app can run next to the user's other OSC tools.
        Assert.True(await Eventually(() => _transport!.OscReceivePort != 0));

        Assert.NotEqual(9001, _transport!.OscReceivePort);
        Assert.NotEqual(9000, _transport.OscReceivePort);
    }

    [Fact]
    public async Task The_query_server_answers_HOST_INFO_with_the_port_it_bound()
    {
        Assert.True(await Eventually(() => _transport!.HttpPort != 0 && _transport.OscReceivePort != 0));

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        string body = await http.GetStringAsync($"http://127.0.0.1:{_transport!.HttpPort}/?HOST_INFO");

        Assert.Contains("OSC_PORT", body, StringComparison.Ordinal);
        Assert.Contains(_transport.OscReceivePort.ToString(), body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_served_tree_registers_a_concrete_child_node()
    {
        // A bare root advertises correctly, answers HOST_INFO, and then receives nothing at all with
        // no error anywhere. Three independent implementations hit this and all fixed it the same
        // way, by registering /avatar/change.
        Assert.True(await Eventually(() => _transport!.HttpPort != 0));

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        string body = await http.GetStringAsync($"http://127.0.0.1:{_transport!.HttpPort}/");

        Assert.Contains("/avatar/change", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_served_tree_does_not_advertise_the_parameters_container()
    {
        Assert.True(await Eventually(() => _transport!.HttpPort != 0));

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        string body = await http.GetStringAsync($"http://127.0.0.1:{_transport!.HttpPort}/");

        Assert.DoesNotContain("\"/avatar/parameters\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_real_datagram_travels_the_whole_path_and_fires_a_command()
    {
        // The end-to-end proof: bytes on a socket, through the decoder, the ingress projection, the
        // epoch and edge guards, and out the other side as a dispatched command.
        Assert.True(await Eventually(() => _transport!.OscReceivePort != 0));

        using var client = new UdpClient();
        var target = new IPEndPoint(IPAddress.Loopback, _transport!.OscReceivePort);

        // Establish the avatar epoch and let the settling window elapse, exactly as a real avatar
        // load would.
        byte[] warmup = OscBool("/avatar/parameters/MCB/Ctrl/Warmup", false);
        await client.SendAsync(warmup, warmup.Length, target);

        Assert.True(await Eventually(() => _transport.Ingress.Counters.Parameters >= 1));
        await Task.Delay(1200);

        byte[] press = OscBool("/avatar/parameters/MCB/Ctrl/Tts/Stop", true);
        await client.SendAsync(press, press.Length, target);

        bool fired = await Eventually(() =>
        {
            lock (_fired) return _fired.Count == 1;
        });

        Assert.True(fired, $"command never fired; ingress saw {_transport.Ingress.Counters.Parameters} parameters");
    }

    [Fact]
    public async Task A_malformed_datagram_is_counted_rather_than_thrown()
    {
        Assert.True(await Eventually(() => _transport!.OscReceivePort != 0));

        using var client = new UdpClient();
        var target = new IPEndPoint(IPAddress.Loopback, _transport!.OscReceivePort);

        byte[] rubbish = Encoding.ASCII.GetBytes("this is not an OSC packet at all");
        await client.SendAsync(rubbish, rubbish.Length, target);

        byte[] good = OscBool("/avatar/parameters/MCB/Ctrl/Warmup", true);
        await client.SendAsync(good, good.Length, target);

        Assert.True(
            await Eventually(() => _transport.Ingress.Counters.Parameters >= 1),
            "the receiver stopped after a malformed packet");
    }

    private static byte[] OscBool(string address, bool value)
    {
        // OSC 1.0: null-terminated strings padded to a four byte boundary. T and F carry no payload,
        // which is what VRChat sends for a bool.
        var bytes = new List<byte>();
        bytes.AddRange(PaddedString(address));
        bytes.AddRange(PaddedString(value ? ",T" : ",F"));
        return bytes.ToArray();
    }

    private static byte[] PaddedString(string value)
    {
        byte[] raw = Encoding.ASCII.GetBytes(value);
        int total = ((raw.Length / 4) + 1) * 4;
        var padded = new byte[total];
        Array.Copy(raw, padded, raw.Length);
        return padded;
    }
}
