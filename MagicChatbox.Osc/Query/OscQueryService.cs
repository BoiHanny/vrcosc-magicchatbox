using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace MagicChatbox.Osc.Query;

/// <summary>Everything about the handshake a caller might reasonably want to change.</summary>
public sealed record OscQueryServiceOptions
{
    /// <summary>The product name, for anything a person reads. Stable across launches.</summary>
    public string ServiceName { get; init; } = "MagicChatbox";

    /// <summary>
    /// The mDNS instance name, unique to this launch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This must not be constant, and that is a correctness requirement rather than a nicety.</b>
    /// VRChat's OSCQuery library keys discovered services by instance name: it adds one only when
    /// that name is not already present, removes it only on a zero-TTL SRV record, and has no
    /// time-based expiry at all. So if we exit without sending a goodbye — a crash, or End Task —
    /// VRChat keeps "MagicChatbox → the port we used last time" for the rest of its session, and
    /// silently ignores the relaunched app advertising the same name on a new port.
    /// </para>
    /// <para>
    /// The failure is worse than not connecting, because it is not symmetric: we still learn
    /// VRChat's port by querying, so we go on sending happily while nothing ever arrives, and the
    /// status reads Connected. A per-launch name makes a stale entry harmless rather than fatal.
    /// </para>
    /// <para>
    /// Random rather than the process id: PID reuse is most likely in exactly the kill-then-relaunch
    /// case this exists to survive. VRChat does the same thing for the same reason, which is why
    /// <c>VrcTransportOptions.VrchatPeerInstancePrefix</c> matches a prefix and not a whole name.
    /// </para>
    /// </remarks>
    /// <remarks>
    /// Four hex digits rather than six. This name is not internal — VRChat prints it back to the user
    /// on its own OSC screen ("Sending OSC data to: MagicChatbox-4C7A"), so it is the app's name in the
    /// one place a person sees the two programs meet. Six characters read as a serial number; four
    /// still give 65,536 values, which is far past what a stale entry from one earlier run needs.
    /// </remarks>
    public string InstanceName { get; init; } =
        $"MagicChatbox-{Random.Shared.Next(0x1000, 0xFFFF):X4}";

    /// <summary>
    /// The instance-name prefix that identifies a peer worth talking to — <c>"VRChat-Client-"</c> in
    /// practice.
    /// </summary>
    /// <remarks>
    /// A parameter, not a constant, because <c>MagicChatbox.Osc</c> speaks OSCQuery and knows nothing
    /// about VRChat (§12). <c>MagicChatbox.Vrc</c> is where "whose client are we looking for" belongs.
    /// </remarks>
    public string PeerInstancePrefix { get; init; } = "VRChat-Client-";

    /// <summary>Where to bind. Loopback unless someone deliberately wants LAN and accepts the caveats.</summary>
    public IPAddress Address { get; init; } = IPAddress.Loopback;

    /// <summary>The UDP port to bind for receive. 0 means "ask the OS", which is the correct value.</summary>
    public int OscReceivePort { get; init; }

    /// <summary>How often to re-query mDNS while nothing is connected. VRCOSC uses 2.5s.</summary>
    public TimeSpan QueryInterval { get; init; } = TimeSpan.FromSeconds(2.5);

    /// <summary>
    /// How long total silence means "mDNS is broken" rather than "VRChat is not running".
    /// </summary>
    /// <remarks>
    /// The distinction matters more than it looks: both symptoms are "nothing arrives", but one is fixed
    /// by starting VRChat and the other by turning off a VPN adapter. Hearing <i>no advertisements at
    /// all</i> — not even other applications' — is the evidence that separates them.
    /// </remarks>
    public TimeSpan SilenceBeforeNoDiscovery { get; init; } = TimeSpan.FromSeconds(20);
}

/// <summary>
/// Runs the OSCQuery handshake: bind, serve, advertise, discover, connect, and say out loud when any of
/// that stops working.
/// </summary>
/// <remarks>
/// <para>
/// The sequence is §12.3's, in order: we pick a free TCP port and a free UDP port; we serve
/// <c>?HOST_INFO</c> and the fixed node tree on the TCP one; we advertise <c>_oscjson._tcp</c> and
/// <c>_osc._udp</c>; VRChat reads our host info and starts sending to our UDP port; we watch for its SRV
/// record and fetch its host info to learn where to send.
/// </para>
/// <para>
/// Every collaborator that touches a socket is injected, so the whole orchestration is exercisable
/// against a loopback peer with no VRChat, no LAN and no multicast.
/// </para>
/// </remarks>
public sealed class OscQueryService : IDisposable
{
    private readonly IOscReceiver _receiver;
    private readonly IOscQueryDiscovery _discovery;
    private readonly OscQueryClient _client;
    private readonly DiscoveredOscEndpointProvider _endpoints;
    private readonly IOscTransportStatusSink _status;
    private readonly OscQueryServiceOptions _options;
    private readonly OscQueryPeerSelector _selector;
    private readonly NeighbourRegistry _neighbours;
    private readonly Channel<IPEndPoint> _pendingPeers;

    private OscQueryHttpServer? _httpServer;
    private OscTransportStatus _lastPublished = new(OscTransportReason.Stopped, "Not started.");
    private IPEndPoint? _peerHttp;
    private long _advertisementsHeard;
    private int _consecutiveFailures;
    private bool _disposed;

    /// <summary>Wires the subsystem. Nothing binds or transmits until <see cref="RunAsync"/>.</summary>
    public OscQueryService(
        IOscReceiver receiver,
        IOscQueryDiscovery discovery,
        OscQueryClient client,
        DiscoveredOscEndpointProvider endpoints,
        IOscTransportStatusSink status,
        OscQueryServiceOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(status);

        _receiver = receiver;
        _discovery = discovery;
        _client = client;
        _endpoints = endpoints;
        _status = status;
        _options = options ?? new OscQueryServiceOptions();
        // The self-ignore has to use the advertised name, not the product name, or we stop
        // recognising our own advertisement and try to hand-shake with ourselves.
        _selector = new OscQueryPeerSelector(_options.PeerInstancePrefix, _options.InstanceName);

        // Same instance name, opposite job. The selector answers "is this the peer"; this keeps everything
        // the selector was throwing away, so the application can name the other OSC program on the machine
        // instead of leaving the user to close things until it works.
        _neighbours = new NeighbourRegistry(_options.InstanceName);

        // The mDNS callback runs on the multicast stack's thread and must return immediately. Handing the
        // endpoint to the loop keeps every HTTP fetch, retry and status change on one thread.
        _pendingPeers = Channel.CreateUnbounded<IPEndPoint>(new UnboundedChannelOptions { SingleReader = true });
        _discovery.AdvertisementReceived += OnAdvertisement;
    }

    /// <summary>The TCP port our OSCQuery HTTP server bound, or 0 before it started.</summary>
    public int HttpPort => _httpServer?.Port ?? 0;

    /// <summary>The UDP port our receiver bound. This is the number in our <c>?HOST_INFO</c>.</summary>
    public int OscReceivePort => _receiver.Port;

    /// <summary>The most recent status published to the sink.</summary>
    public OscTransportStatus LastStatus => _lastPublished;

    /// <summary>Everything else on the local network that has announced itself over OSCQuery.</summary>
    /// <remarks>
    /// Observational and nothing acts on it. We are a guest on this machine: naming the other program is
    /// the whole of what this is for, and there is no arbitration, no port negotiation and no "close
    /// VRCFaceTracking" anywhere behind it. Not filtered to this machine — see
    /// <see cref="NeighbourRegistry"/> for why, and for what the address column is doing.
    /// </remarks>
    public NeighbourRegistry Neighbours => _neighbours;

    /// <summary>The peer's OSCQuery HTTP endpoint, once a handshake has succeeded.</summary>
    /// <remarks>
    /// Not the same address as the one we send OSC to. The peer serves its node tree over TCP on a port
    /// it picks per session — 63943 one run, 57780 the next — which is exactly why it has to be captured
    /// from the advertisement rather than assumed.
    /// </remarks>
    public IPEndPoint? PeerHttpEndpoint => Volatile.Read(ref _peerHttp);

    /// <summary>Raised after a successful handshake, with the peer's HTTP endpoint.</summary>
    /// <remarks>
    /// Fires on <i>every</i> successful handshake, not only the first: a peer that re-advertises after a
    /// restart, or one re-queued by <see cref="RequeueAfterAsync"/> after a failed HOST_INFO read,
    /// produces another. A subscriber that harvests must therefore be idempotent and must re-check
    /// anything it captured before the fetch.
    /// </remarks>
    public event Action<IPEndPoint>? PeerConnected;

    /// <summary>
    /// Fetches the peer's whole node tree, or null when no peer is known or it is unreachable.
    /// </summary>
    /// <remarks>
    /// One GET of <c>/</c> is enough: a live client returns the complete tree rather than a depth-limited
    /// root, verified by counting <c>/avatar/parameters</c> leaves through both the root document and the
    /// subtree document and getting the same number.
    /// </remarks>
    public Task<OscQuerySnapshot?> TryFetchPeerSnapshotAsync(CancellationToken cancellationToken)
    {
        var peer = Volatile.Read(ref _peerHttp);
        return peer is null
            ? Task.FromResult<OscQuerySnapshot?>(null)
            : _client.TryFetchSnapshotAsync(peer, cancellationToken);
    }

    /// <summary>Runs the whole subsystem until cancelled.</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        int oscPort;
        try
        {
            oscPort = _receiver.Bind();
        }
        catch (SocketException ex)
        {
            // Nothing can arrive without this socket, so it is the one failure that ends the run.
            Publish(new OscTransportStatus(
                OscTransportReason.ReceiveBindFailed,
                $"Could not bind the OSC receive port: {ex.SocketErrorCode}."));
            return;
        }

        _httpServer = new OscQueryHttpServer(_options.ServiceName, oscPort, _options.Address);
        var startStatus = _httpServer.Start();
        Publish(startStatus);

        if (startStatus.Reason == OscTransportReason.HttpPortUnavailable)
        {
            // We can still receive; we simply cannot be discovered. Advertising a port we do not serve
            // would be worse than not advertising, so stop here and leave the status standing.
            return;
        }

        _discovery.Start();
        _discovery.Advertise(_options.InstanceName, _httpServer.Address, _httpServer.Port, oscPort);

        var startedAt = DateTimeOffset.UtcNow;

        await Task.WhenAll(
            _receiver.RunAsync(cancellationToken),
            _httpServer.RunAsync(cancellationToken),
            HandlePeersAsync(cancellationToken),
            QueryLoopAsync(startedAt, cancellationToken)).ConfigureAwait(false);

        Publish(new OscTransportStatus(OscTransportReason.Stopped, "Transport stopped."));
    }

    /// <summary>
    /// Decides what an idle transport should be reporting.
    /// </summary>
    /// <remarks>
    /// Pure and public because this is the judgement the whole degraded-status feature exists to make,
    /// and a judgement wired into a timer loop is a judgement nobody can test.
    /// </remarks>
    /// <param name="advertisementsHeard">Advertisements of any kind heard since start — evidence that multicast works.</param>
    /// <param name="silence">How long we have been running without connecting.</param>
    /// <param name="threshold">How much silence means mDNS is broken rather than VRChat being absent.</param>
    public static OscTransportStatus DescribeIdle(long advertisementsHeard, TimeSpan silence, TimeSpan threshold) =>
        advertisementsHeard == 0 && silence >= threshold
            ? new OscTransportStatus(
                OscTransportReason.NoDiscovery,
                "No mDNS advertisements at all — multicast is being blocked, most likely by a firewall or a " +
                "VPN adapter. The manual OSC port override is the workaround.")
            : new OscTransportStatus(
                OscTransportReason.NoClient,
                "No VRChat client discovered yet.");

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _discovery.AdvertisementReceived -= OnAdvertisement;
        _pendingPeers.Writer.TryComplete();
        _httpServer?.Dispose();
        _discovery.Dispose();
        _receiver.Dispose();
    }

    private void OnAdvertisement(OscQueryAdvertisement advertisement)
    {
        Interlocked.Increment(ref _advertisementsHeard);

        // Before the selector, and deliberately not inside its rejection branch: the peer's own records
        // are the ones a person most wants to see in the "also on this machine" list, and they are the
        // ones the selector accepts. See NeighbourRegistry's remarks.
        _neighbours.Record(advertisement);

        if (!_selector.TryAccept(advertisement, out var endpoint))
        {
            // An expiring advertisement lands here too: the selector takes its Expired branch, calls
            // Forget, and returns false. That cleared the selector's own fields and nothing else —
            // so the discovered endpoint stayed set, we went on sending to a port VRChat had already
            // released, and the status still read as connected to it.
            //
            // Re-arming the query loop is the other half. Its condition is exactly
            // `_endpoints.Discovered is null`, so leaving the endpoint set also meant discovery
            // stopped for good the first time it succeeded — VRChat could never be found again
            // without restarting the app.
            if (_selector.SelectedHttpEndpoint is null && Volatile.Read(ref _peerHttp) is not null)
            {
                Volatile.Write(ref _peerHttp, null);
                _endpoints.SetDiscovered(null);

                // The endpoint provider's own description, not a hand-rolled reason: under a
                // fallback override the egress endpoint is still live, and saying NoClient would be
                // a lie about a connection that still works.
                Publish(_endpoints.DescribeStatus());
            }

            return;
        }

        // The UDP record tells us the peer's raw OSC port, but HOST_INFO is authoritative for where to
        // send, so only the HTTP record starts a handshake.
        if (advertisement.ServiceType == OscQueryServiceTypes.OscJsonTcp)
        {
            _pendingPeers.Writer.TryWrite(endpoint);
        }
    }

    private async Task HandlePeersAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var peer in _pendingPeers.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                await ConnectToPeerAsync(peer, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ConnectToPeerAsync(IPEndPoint peer, CancellationToken cancellationToken)
    {
        var hostInfo = await _client.TryFetchHostInfoAsync(peer, cancellationToken).ConfigureAwait(false);

        if (hostInfo is null)
        {
            var failures = Interlocked.Increment(ref _consecutiveFailures);
            var delay = OscReconnectBackoff.CalculateDelay(failures, Random.Shared.NextDouble());

            Publish(new OscTransportStatus(
                OscTransportReason.HostInfoUnreachable,
                $"Discovered a peer at {peer} but could not read its HOST_INFO.",
                failures,
                DateTimeOffset.UtcNow + delay));

            // Re-queue rather than retry inline: the delay must not hold up a second peer's handshake.
            _ = RequeueAfterAsync(peer, delay, cancellationToken);
            return;
        }

        Interlocked.Exchange(ref _consecutiveFailures, 0);

        var sendTo = new IPEndPoint(
            hostInfo.OscIp.Equals(IPAddress.Any) ? IPAddress.Loopback : hostInfo.OscIp,
            hostInfo.OscPort);

        _endpoints.SetDiscovered(sendTo);

        // Keep the peer's HTTP endpoint. HOST_INFO tells us where to SEND (a UDP port); `peer` is where
        // to ASK (the OSCQuery HTTP server we just successfully read that host info from). Letting it fall
        // out of scope here is what left TryFetchSnapshotAsync with no caller and the application learning
        // parameters only when they happened to move.
        Volatile.Write(ref _peerHttp, peer);

        Publish(_endpoints.DescribeStatus());

        // After the status is published, so a handler that harvests sees a transport already reporting
        // itself connected.
        PeerConnected?.Invoke(peer);
    }

    private async Task RequeueAfterAsync(IPEndPoint peer, TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            _pendingPeers.Writer.TryWrite(peer);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task QueryLoopAsync(DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_endpoints.Discovered is null)
                {
                    _discovery.Query();
                    Publish(DescribeIdle(
                        Volatile.Read(ref _advertisementsHeard),
                        DateTimeOffset.UtcNow - startedAt,
                        _options.SilenceBeforeNoDiscovery));
                }

                await Task.Delay(_options.QueryInterval, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void Publish(OscTransportStatus status)
    {
        // Only changes are published: a status bar that re-renders "no client" every 2.5 seconds teaches
        // people to stop reading it.
        if (status == _lastPublished)
        {
            return;
        }

        _lastPublished = status;
        _status.OnStatus(status);
    }
}
