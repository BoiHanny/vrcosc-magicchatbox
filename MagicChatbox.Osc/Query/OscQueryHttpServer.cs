using System.Net;
using System.Net.Sockets;

namespace MagicChatbox.Osc.Query;

/// <summary>
/// Our half of the OSCQuery handshake: an HTTP server serving <c>?HOST_INFO</c> and the node tree.
/// </summary>
/// <remarks>
/// <para>
/// <b>D15 — the concurrency bug this port exists to fix.</b> v2's <c>OscQueryServer.UpdateParameterNodes</c>
/// carried a doc-comment promising "the query data root reference is swapped atomically" above a body
/// that assigned <c>parametersNode.Contents = nodes</c> — an in-place mutation of a nested node, with no
/// lock, while the HTTP handler serialized the same object graph on an arbitrary thread. An avatar load
/// concurrent with a peer's GET could hand <c>JsonSerializer</c> a dictionary mid-replacement: a torn
/// document at best, an <see cref="InvalidOperationException"/> inside the handler at worst.
/// </para>
/// <para>
/// The fix goes one step past what that comment promised. Publishing builds a complete new tree,
/// serializes it once, and <see cref="Volatile.Write{T}"/>s a single immutable snapshot object holding
/// both. Requests read that snapshot and write already-encoded bytes, so there is no serializer walking
/// a live graph to race with at all — and the per-request serialization cost disappears as a side
/// effect.
/// </para>
/// <para>
/// <b>HttpListener, not EmbedIO.</b> v2 hosted this on EmbedIO. VRCOSC uses <see cref="HttpListener"/>,
/// and so do we: it is in the BCL, so the assembly that owns the wire keeps its dependency list at one
/// package. It also makes the platform's real constraint explicit rather than hidden behind a framework
/// — binding a non-loopback prefix needs an administrator, which is why LAN OSCQuery is a
/// <see cref="OscTransportReason.LoopbackOnly"/> degraded mode here rather than a mystery.
/// </para>
/// </remarks>
public sealed class OscQueryHttpServer : IDisposable
{
    /// <summary>
    /// How many times we re-pick a port before giving up on this attempt. A port found free can be taken
    /// in the microseconds before we bind it; that is a race, not a failure, and §12.3 is explicit that
    /// startup never fails over it.
    /// </summary>
    private const int PortAttempts = 8;

    private readonly string _serviceName;
    private readonly int _oscReceivePort;
    private Snapshot _snapshot;
    private HttpListener? _listener;
    private bool _disposed;

    /// <param name="serviceName">The instance name we advertise. Peers see it in <c>?HOST_INFO</c>.</param>
    /// <param name="oscReceivePort">The UDP port our receiver bound. This is what makes VRChat send to us.</param>
    /// <param name="address">Where to bind. Loopback unless someone deliberately asks for LAN.</param>
    public OscQueryHttpServer(string serviceName, int oscReceivePort, IPAddress? address = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentOutOfRangeException.ThrowIfNegative(oscReceivePort);

        _serviceName = serviceName;
        _oscReceivePort = oscReceivePort;
        Address = address ?? IPAddress.Loopback;

        _snapshot = Snapshot.Build(OscQueryAdvertisedTree.Build(), BuildHostInfo(serviceName, Address, oscReceivePort));
    }

    /// <summary>The address the HTTP server is bound to.</summary>
    public IPAddress Address { get; private set; }

    /// <summary>The TCP port bound, or 0 before <see cref="Start"/>. This is what we advertise as <c>_oscjson._tcp</c>.</summary>
    public int Port { get; private set; }

    /// <summary>The tree currently being served. Never mutate it; publish a new one instead.</summary>
    public OscQueryNode CurrentRoot => Volatile.Read(ref _snapshot).Root;

    /// <summary>The host-info document currently being served.</summary>
    public OscQueryHostInfo CurrentHostInfo => Volatile.Read(ref _snapshot).HostInfo;

    /// <summary>Binds a free port and starts listening. Returns the resulting transport status.</summary>
    /// <remarks>
    /// Returns a status rather than throwing, because none of the ways this fails should stop the
    /// application: a taken port is retried, and a refused LAN binding falls back to loopback with
    /// <see cref="OscTransportReason.LoopbackOnly"/> so the user learns why their LAN setup is quiet.
    /// </remarks>
    public OscTransportStatus Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_listener is not null)
        {
            return OscTransportStatus.Connected($"OSCQuery HTTP already listening on {Address}:{Port}.");
        }

        var degraded = default(OscTransportStatus?);

        for (var attempt = 0; attempt < PortAttempts; attempt++)
        {
            var port = FindFreeTcpPort(Address);
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://{Address}:{port}/");

            try
            {
                listener.Start();
                _listener = listener;
                Port = port;
                Volatile.Write(ref _snapshot, _snapshot.WithHostInfo(BuildHostInfo(_serviceName, Address, _oscReceivePort)));
                return degraded ?? OscTransportStatus.Connected($"OSCQuery HTTP listening on {Address}:{Port}.");
            }
            catch (HttpListenerException ex) when (IsAccessDenied(ex) && !IPAddress.IsLoopback(Address))
            {
                // Windows refuses a non-loopback HTTP prefix to a non-administrator. VRChat documents
                // OSCQuery as loopback-only in practice for exactly this reason. Retreat to loopback and
                // NAME the retreat — a silently-loopback server on a LAN setup is an evening lost.
                listener.Close();
                degraded = new OscTransportStatus(
                    OscTransportReason.LoopbackOnly,
                    $"Binding OSCQuery HTTP to {Address} was refused; serving on loopback instead. " +
                    "LAN OSCQuery needs an administrator on Windows.");
                Address = IPAddress.Loopback;
            }
            catch (HttpListenerException)
            {
                // Someone took the port between finding it free and binding it. Pick another.
                listener.Close();
            }
        }

        return new OscTransportStatus(
            OscTransportReason.HttpPortUnavailable,
            $"Could not bind an OSCQuery HTTP port on {Address} after {PortAttempts} attempts.",
            AttemptCount: PortAttempts);
    }

    /// <summary>Serves requests until cancelled. Returns normally on cancellation or disposal.</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var listener = _listener ?? throw new InvalidOperationException($"Call {nameof(Start)} first.");

        // HttpListener predates cancellation tokens; stopping it is what unblocks the pending accept.
        using var stopOnCancel = cancellationToken.Register(() =>
        {
            try
            {
                listener.Stop();
            }
            catch (ObjectDisposedException)
            {
            }
        });

        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (HttpListenerException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (InvalidOperationException)
            {
                return;
            }

            // Handled off the accept path so a slow client cannot stall the next peer's handshake.
            _ = Task.Run(() => RespondAsync(context), CancellationToken.None);
        }
    }

    /// <summary>Publishes a complete new tree. The swap is atomic; the previous tree is never touched.</summary>
    public void PublishRoot(OscQueryNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        Volatile.Write(ref _snapshot, Volatile.Read(ref _snapshot).WithRoot(root));
    }

    /// <summary>
    /// Publishes a parameter branch under <c>/avatar/parameters</c>, as a whole-tree rebuild.
    /// </summary>
    /// <remarks>
    /// The D15 replacement for v2's <c>UpdateParameterNodes</c>. Same name in spirit, opposite mechanics:
    /// nothing already published is mutated, so a request in flight keeps serving the document it started
    /// with. The base advertised tree still never carries parameters of its own (§12.4) — this exists for
    /// the discovered-peer path, which is the one that made the v2 race reachable.
    /// </remarks>
    public void PublishParameterNodes(IReadOnlyDictionary<string, OscQueryNode> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var root = OscQueryAdvertisedTree.Build();
        var avatar = root.Contents!["avatar"];

        avatar.Contents!["parameters"] = new OscQueryNode
        {
            FullPath = "/avatar/parameters",
            Access = (int)OscQueryAccess.Write,
            Description = "Avatar Parameters",
            Contents = new Dictionary<string, OscQueryNode>(parameters, StringComparer.Ordinal),
        };

        PublishRoot(root);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _listener?.Close();
        _listener = null;
    }

    private async Task RespondAsync(HttpListenerContext context)
    {
        try
        {
            var snapshot = Volatile.Read(ref _snapshot);
            var rawUrl = context.Request.RawUrl ?? string.Empty;

            var payload = rawUrl.Contains("HOST_INFO", StringComparison.OrdinalIgnoreCase)
                ? snapshot.HostInfoUtf8
                : snapshot.RootUtf8;

            context.Response.ContentType = "application/json";
            context.Response.Headers.Add("Cache-Control", "no-cache");
            context.Response.ContentLength64 = payload.Length;

            await context.Response.OutputStream.WriteAsync(payload).ConfigureAwait(false);
            context.Response.Close();
        }
        catch (Exception)
        {
            // A peer that hung up mid-response is routine and must not take the server with it. There is
            // nothing to report: the peer will retry the handshake, and its own backoff owns that.
            try
            {
                context.Response.Abort();
            }
            catch (Exception)
            {
            }
        }
    }

    private static bool IsAccessDenied(HttpListenerException ex) => ex.ErrorCode == 5;

    private static OscQueryHostInfo BuildHostInfo(string serviceName, IPAddress address, int oscReceivePort) => new()
    {
        Name = serviceName,
        // Advertising 0.0.0.0 tells a peer to send to nowhere in particular; loopback is what it means.
        OscIp = address.Equals(IPAddress.Any) ? IPAddress.Loopback : address,
        OscPort = oscReceivePort,
        OscTransport = OscTransport.UDP,
        Extensions = new OscQueryExtensions(),
    };

    private static int FindFreeTcpPort(IPAddress address)
    {
        using var probe = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        probe.Bind(new IPEndPoint(address, 0));
        return ((IPEndPoint)probe.LocalEndPoint!).Port;
    }

    /// <summary>An immutable published pair: the documents and their encoded bytes.</summary>
    /// <remarks>One object so a request sees a consistent tree and host-info, never a mix of two publishes.</remarks>
    private sealed record Snapshot(
        OscQueryNode Root,
        OscQueryHostInfo HostInfo,
        byte[] RootUtf8,
        byte[] HostInfoUtf8)
    {
        public static Snapshot Build(OscQueryNode root, OscQueryHostInfo hostInfo) =>
            new(root, hostInfo, OscQueryJson.SerializeUtf8(root), OscQueryJson.SerializeUtf8(hostInfo));

        public Snapshot WithRoot(OscQueryNode root) => Build(root, HostInfo);

        public Snapshot WithHostInfo(OscQueryHostInfo hostInfo) => Build(Root, hostInfo);
    }
}
