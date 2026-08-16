using MagicChatbox.Osc;
using MagicChatbox.Osc.Query;
using System;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MagicChatbox.Tests.Core.Vrc;

// The half of discovery that is ours to get right. VRChat announces itself over mDNS, we decide
// whether that announcement is VRChat, fetch its HOST_INFO, and take the OSC port out of it - and
// from then on everything the app sends goes to that port. Multicast is not involved here on
// purpose: an advertisement is handed straight to the service, so this asserts the decision and the
// handshake rather than whether the build agent forwards multicast.
//
// What still needs the real game is whether VRChat announces itself the way this stands in for.
public class OscQueryDiscoveryTests : IAsyncLifetime
{
    private sealed class FakeDiscovery : IOscQueryDiscovery
    {
        public event Action<OscQueryAdvertisement>? AdvertisementReceived;

        public string? AdvertisedInstance { get; private set; }
        public int AdvertisedHttpPort { get; private set; }
        public int AdvertisedOscPort { get; private set; }
        public int Queries { get; private set; }

        public void Start() { }

        public void Advertise(string instanceName, IPAddress address, int httpPort, int oscPort)
        {
            AdvertisedInstance = instanceName;
            AdvertisedHttpPort = httpPort;
            AdvertisedOscPort = oscPort;
        }

        public void Query() => Queries++;

        public void Announce(OscQueryAdvertisement advertisement) => AdvertisementReceived?.Invoke(advertisement);

        public void Dispose() { }
    }

    // Stands in for VRChat's own OSCQuery server: answers ?HOST_INFO with a port, which is the
    // number the app has to end up sending to.
    private sealed class FakePeer : IDisposable
    {
        private readonly HttpListener _listener = new();

        public FakePeer(int oscPort)
        {
            OscPort = oscPort;

            Port = FreeTcpPort();
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            _listener.Start();
            _ = Task.Run(ServeAsync);
        }

        public int Port { get; }

        public int OscPort { get; }

        public int Requests { get; private set; }

        private async Task ServeAsync()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;

                try
                {
                    context = await _listener.GetContextAsync().ConfigureAwait(false);
                }
                catch
                {
                    return;
                }

                Requests++;

                string body =
                    $"{{\"NAME\":\"VRChat-Client-Test\",\"OSC_IP\":\"127.0.0.1\",\"OSC_PORT\":{OscPort}," +
                    "\"OSC_TRANSPORT\":\"UDP\",\"EXTENSIONS\":{\"ACCESS\":true,\"VALUE\":true}}";

                byte[] bytes = Encoding.UTF8.GetBytes(body);
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
                context.Response.Close();
            }
        }

        private static int FreeTcpPort()
        {
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        public void Dispose()
        {
            try { _listener.Stop(); } catch { }
            try { _listener.Close(); } catch { }
        }
    }

    private sealed class SilentStatusSink : IOscTransportStatusSink
    {
        private readonly System.Collections.Generic.List<string> _statuses = [];

        public void OnStatus(OscTransportStatus status)
        {
            lock (_statuses) _statuses.Add($"{status.Reason}: {status.Detail}");
        }

        public string Describe()
        {
            lock (_statuses) return string.Join(" | ", _statuses);
        }
    }

    private SilentStatusSink _status = null!;
    private FakeDiscovery _discovery = null!;
    private DiscoveredOscEndpointProvider _endpoints = null!;
    private OscQueryService _service = null!;
    private UdpOscReceiver _receiver = null!;
    private CancellationTokenSource _cts = null!;
    private Task _run = null!;

    public Task InitializeAsync()
    {
        _status = new SilentStatusSink();
        _discovery = new FakeDiscovery();
        _endpoints = new DiscoveredOscEndpointProvider();
        _receiver = new UdpOscReceiver(new BufferedOscMessageSink(), IPAddress.Loopback, 0);

        _service = new OscQueryService(
            _receiver,
            _discovery,
            OscQueryClient.CreateDefault(),
            _endpoints,
            _status,
            new OscQueryServiceOptions
            {
                ServiceName = "MagicChatbox.Tests",
                InstanceName = "MagicChatbox-Test",
                PeerInstancePrefix = "VRChat-Client-",
                Address = IPAddress.Loopback,
                OscReceivePort = 0,
                QueryInterval = TimeSpan.FromMilliseconds(200),
            });

        _cts = new CancellationTokenSource();
        _run = Task.Run(async () =>
        {
            try { await _service.RunAsync(_cts.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        });

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _cts.Cancel();

        try { await _run.WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (TimeoutException) { }

        _service.Dispose();
        _cts.Dispose();
    }

    private static async Task<bool> Eventually(Func<bool> condition, int timeoutMs = 10000)
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

    // The service type has to be the exact constant. Only the OSCQuery HTTP record starts a
    // handshake, and a near-miss like "_oscjson._tcp.local." is silently never acted on.
    private OscQueryAdvertisement Advert(string instance, int httpPort)
        => new(instance, OscQueryServiceTypes.OscJsonTcp, IPAddress.Loopback, httpPort);

    [Fact]
    public async Task We_advertise_ourselves_so_VRChat_can_find_us()
    {
        Assert.True(await Eventually(() => _discovery.AdvertisedInstance != null));

        Assert.Equal("MagicChatbox-Test", _discovery.AdvertisedInstance);
        Assert.NotEqual(0, _discovery.AdvertisedOscPort);
        Assert.NotEqual(0, _discovery.AdvertisedHttpPort);

        // What we advertise has to be the port we actually bound, or VRChat sends into nothing.
        Assert.Equal(_service.OscReceivePort, _discovery.AdvertisedOscPort);
        Assert.Equal(_service.HttpPort, _discovery.AdvertisedHttpPort);
    }

    [Fact]
    public async Task VRChat_is_recognised_and_its_OSC_port_becomes_our_send_target()
    {
        // The whole handshake: hear the announcement, fetch HOST_INFO, and start sending to the port
        // it names. 45678 is arbitrary and deliberately not 9000, so a default cannot pass this.
        using var peer = new FakePeer(oscPort: 45678);

        Assert.True(await Eventually(() => _service.OscReceivePort != 0));

        _discovery.Announce(Advert("VRChat-Client-8f2a1c", peer.Port));

        Assert.True(
            await Eventually(() => _endpoints.Discovered != null),
            $"the peer was never resolved. statuses: {_status.Describe()} ; peer requests: {peer.Requests}");

        Assert.Equal(45678, _endpoints.Discovered!.Port);
        Assert.Equal(IPAddress.Loopback, _endpoints.Discovered.Address);
        Assert.True(peer.Requests >= 1, "we never asked the peer for its HOST_INFO");
    }

    [Fact]
    public async Task Another_OSC_application_is_remembered_but_never_mistaken_for_VRChat()
    {
        // This is what lets the app name the other program on the machine instead of leaving the
        // user to close things one at a time until it works.
        using var peer = new FakePeer(oscPort: 45679);

        Assert.True(await Eventually(() => _service.OscReceivePort != 0));

        _discovery.Announce(Advert("VRCFaceTracking-abc", peer.Port));

        Assert.True(await Eventually(() => _service.Neighbours.Count >= 1));

        Assert.Null(_endpoints.Discovered);
        Assert.Contains(_service.Neighbours.List(), n => n.InstanceName.Contains("VRCFaceTracking", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Our_own_advertisement_is_never_treated_as_a_peer()
    {
        // Handshaking with ourselves would report Connected while nothing worked.
        Assert.True(await Eventually(() => _service.HttpPort != 0));

        _discovery.Announce(Advert("MagicChatbox-Test", _service.HttpPort));

        await Task.Delay(400);

        Assert.Null(_endpoints.Discovered);
    }

    [Fact]
    public async Task A_peer_that_does_not_answer_leaves_us_unconnected_rather_than_wrong()
    {
        Assert.True(await Eventually(() => _service.OscReceivePort != 0));

        // Nothing is listening on this port.
        _discovery.Announce(Advert("VRChat-Client-dead", 1));

        await Task.Delay(1000);

        Assert.Null(_endpoints.Discovered);
    }

    [Fact]
    public async Task We_keep_asking_while_nothing_has_been_found()
    {
        // VRChat may start after we do. Giving up after one query would mean never connecting.
        Assert.True(await Eventually(() => _discovery.Queries >= 2, 5000));
    }
}
