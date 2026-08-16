using System.Net;
using MagicChatbox.Osc;
using MagicChatbox.Osc.Query;

namespace MagicChatbox.Vrc;

/// <summary>Everything about the VRChat transport a host might reasonably want to change.</summary>
/// <remarks>
/// Deliberately smaller than <c>OscQueryServiceOptions</c>. The peer instance prefix is not here:
/// "whose client are we looking for" is VRChat knowledge, it is <c>Vrc</c>'s to answer, and making it a
/// setting would invite a support thread about the one value that is never wrong.
/// </remarks>
public sealed record VrcTransportOptions
{
    /// <summary>
    /// The product name reported in HOST_INFO and shown to people.
    /// </summary>
    /// <remarks>
    /// Not the mDNS instance name. That is
    /// <see cref="Osc.Query.OscQueryServiceOptions.InstanceName"/>, which is randomised per launch
    /// because VRChat's discovery cache keys on it and never expires entries — see the remarks
    /// there. This one is stable precisely because it is the one a person reads.
    /// </remarks>
    public string ServiceName { get; init; } = "MagicChatbox";

    /// <summary>Where to bind. Loopback: VRChat's OSC traffic is local.</summary>
    public IPAddress Address { get; init; } = IPAddress.Loopback;

    /// <summary>
    /// The UDP port to bind for receive. <b>0 means "ask the OS", which is the correct value.</b>
    /// </summary>
    /// <remarks>
    /// Hard-coding 9001 is how an application becomes silently deaf: the moment a second OSC application
    /// is running the port is taken, and the failure mode is nothing at all (§12.1). The bound port is
    /// advertised over OSCQuery instead.
    /// </remarks>
    public int OscReceivePort { get; init; }

    /// <summary>How often to re-query mDNS while nothing is connected.</summary>
    public TimeSpan QueryInterval { get; init; } = TimeSpan.FromSeconds(2.5);

    /// <summary>How long total mDNS silence means "multicast is blocked" rather than "VRChat is not running".</summary>
    public TimeSpan SilenceBeforeNoDiscovery { get; init; } = TimeSpan.FromSeconds(20);

    /// <summary>How long a dispatched avatar write waits for its echo before it is called unconfirmed.</summary>
    public TimeSpan EchoTimeout { get; init; } = VrcEchoTracker.DefaultTimeout;

    /// <summary>How long unchanged chatbox content may sit before it is resent (P4).</summary>
    public TimeSpan UnchangedResend { get; init; } = ChatboxCadence.UnchangedResend;
}

/// <summary>
/// The whole VRChat I/O path, assembled: discovery, ingress, egress, echo correlation and the heartbeat.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is what a caller in <c>Core</c> constructs, and it is the reason <c>Core</c> needs no
/// reference to <c>MagicChatbox.Osc</c> at all.</b> Every OSC type involved — the UDP sockets, the mDNS
/// stack, the OSCQuery HTTP server and client, the raw sender — is created in here and named nowhere on
/// this type's public surface. The one exception the fence permits, <c>IOscEndpointProvider</c>, is not
/// used either. That property is asserted by <c>VrcPublicSurface_LeaksNoOscType</c>.
/// </para>
/// <para>
/// The sequence it runs is §12.3's: bind a UDP port the OS chooses, serve <c>?HOST_INFO</c> and our node
/// tree over HTTP, advertise both service types over mDNS, watch for a <c>VRChat-Client-*</c> peer,
/// fetch its host info, and learn from that where to send. Nothing is hard-coded to 9000/9001; the
/// manual override exists for blocked multicast and is never the default.
/// </para>
/// <para>
/// <see cref="VrcEgressFactory"/> remains the seam for a host that has already negotiated an endpoint by
/// other means. This type is the seam for the ordinary case, where the endpoint is discovered.
/// </para>
/// </remarks>
public sealed class VrcTransport : IDisposable
{
    /// <summary>
    /// The mDNS instance-name prefix VRChat advertises under.
    /// </summary>
    /// <remarks>
    /// It lives here rather than in <c>MagicChatbox.Osc</c> on purpose: that assembly speaks OSCQuery and
    /// knows nothing about VRChat, and this string is the whole of the VRChat knowledge the handshake
    /// needs (§12.3).
    /// </remarks>
    public const string VrchatPeerInstancePrefix = "VRChat-Client-";

    private readonly OscQueryService _queryService;
    private readonly VrcSchemaHarvester _harvester;
    private readonly UdpOscSender _sender;
    private readonly VrcTransportStatusAdapter _status;
    private bool _disposed;

    internal VrcTransport(
        VrcTransportOptions options,
        DiscoveredOscEndpointProvider endpoints,
        Func<IOscMessageSink, IOscReceiver> receiverFactory,
        IOscQueryDiscovery discovery,
        OscQueryClient client,
        IVrcObservationSink observations,
        IVrcTransportStatusSink status,
        IWorldPolicy world,
        IProfanityPolicy profanity,
        IChatboxCadence? cadence,
        IEgressJournal? journal,
        IVrcSchemaSink? schema = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(receiverFactory);
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(observations);
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(profanity);

        AvatarEpoch = new VrcAvatarEpoch();
        Echo = new VrcEchoTracker(AvatarEpoch, options.EchoTimeout);
        Ingress = new VrcAvatarIngress(observations, AvatarEpoch, Echo);

        _status = new VrcTransportStatusAdapter(status);
        _sender = new UdpOscSender(endpoints);

        Egress = new VrcEgress(
            _sender,
            world,
            profanity,
            cadence ?? new ChatboxCadence(),
            journal,
            Echo);

        Chatbox = new VrcChatboxPublisher(Egress, AvatarEpoch, options.UnchangedResend);

        _queryService = new OscQueryService(
            receiverFactory(Ingress),
            discovery,
            client,
            endpoints,
            _status,
            new OscQueryServiceOptions
            {
                ServiceName = options.ServiceName,
                PeerInstancePrefix = VrchatPeerInstancePrefix,
                Address = options.Address,
                OscReceivePort = options.OscReceivePort,
                QueryInterval = options.QueryInterval,
                SilenceBeforeNoDiscovery = options.SilenceBeforeNoDiscovery,
            });

        // Constructed after the query service, because it subscribes to it. Harvesting is what turns
        // "we learn a parameter when it moves" into "we know what exists before it ever does".
        _harvester = new VrcSchemaHarvester(_queryService, AvatarEpoch, schema ?? NullVrcSchemaSink.Instance);
    }

    /// <summary>The only way out to VRChat. Every send passes the full gate pipeline.</summary>
    public IVrcEgress Egress { get; }

    /// <summary>The chatbox front end: unchanged-content dedupe plus the 20-second heartbeat (P4).</summary>
    public VrcChatboxPublisher Chatbox { get; }

    /// <summary>Which avatar is loaded, and the invalidation everything else hangs off (P10).</summary>
    public VrcAvatarEpoch AvatarEpoch { get; }

    /// <summary>Correlates dispatched writes with VRChat's echo of them (P7).</summary>
    public VrcEchoTracker Echo { get; }

    /// <summary>The address projection, and its counters.</summary>
    public VrcAvatarIngress Ingress { get; }

    /// <summary>The most recent health reading, already in kernel vocabulary.</summary>
    public VrcTransportStatus Status => _status.Last;

    /// <summary>The UDP port we bound and advertised, or 0 before <see cref="RunAsync"/> binds it.</summary>
    public int OscReceivePort => _queryService.OscReceivePort;

    /// <summary>The TCP port our OSCQuery HTTP server bound, or 0 before it started.</summary>
    public int HttpPort => _queryService.HttpPort;

    /// <summary>
    /// Every other program on this machine that has announced itself over OSCQuery.
    /// </summary>
    /// <remarks>
    /// A method rather than a property because it snapshots and sorts a dictionary under a lock, and a
    /// property that costs an allocation invites a caller to read it in a loop. Empty is the common and
    /// healthy answer.
    /// </remarks>
    public IReadOnlyList<VrcNeighbour> DescribeNeighbours()
    {
        var heard = _queryService.Neighbours.List();
        if (heard.Count == 0)
        {
            return [];
        }

        var rows = new VrcNeighbour[heard.Count];
        for (var i = 0; i < heard.Count; i++)
        {
            rows[i] = VrcNeighbour.From(heard[i]);
        }

        return rows;
    }

    /// <summary>
    /// Builds the production transport: real sockets, real mDNS, real HTTP.
    /// </summary>
    /// <param name="world">Consulted before every chatbox send.</param>
    /// <param name="profanity">Consulted before every chatbox send.</param>
    /// <param name="observations">Where projected observations go. <c>Core</c> supplies the kernel bridge.</param>
    /// <param name="status">Where health readings go. Defaults to discarding them.</param>
    /// <param name="options">Defaults are the ones §12.3 argues for; the receive port is OS-chosen.</param>
    /// <param name="cadence">Courtesy cadence. Defaults to <see cref="ChatboxCadence.DefaultInterval"/>.</param>
    /// <param name="journal">Where dispatches and blocks are recorded. Defaults to discarding them.</param>
    public static VrcTransport Create(
        IWorldPolicy world,
        IProfanityPolicy profanity,
        IVrcObservationSink? observations = null,
        IVrcTransportStatusSink? status = null,
        VrcTransportOptions? options = null,
        IChatboxCadence? cadence = null,
        IEgressJournal? journal = null,
        IVrcSchemaSink? schema = null)
    {
        var resolved = options ?? new VrcTransportOptions();

        return new VrcTransport(
            resolved,
            new DiscoveredOscEndpointProvider(),
            sink => new UdpOscReceiver(sink, resolved.Address, resolved.OscReceivePort),
            new MDnsOscQueryDiscovery(),
            OscQueryClient.CreateDefault(),
            observations ?? NullVrcObservationSink.Instance,
            status ?? NullVrcTransportStatusSink.Instance,
            world,
            profanity,
            cadence,
            journal,
            schema);
    }

    /// <summary>Runs the whole subsystem — bind, serve, advertise, discover, receive — until cancelled.</summary>
    public Task RunAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // The harvester runs alongside rather than inside the query service: a harvest is an HTTP round
        // trip, and the handshake loop must stay free to service the next advertisement while it is in
        // flight. WhenAll rather than fire-and-forget so a fault surfaces at the host instead of
        // vanishing into an unobserved task.
        return Task.WhenAll(
            _queryService.RunAsync(cancellationToken),
            _harvester.RunAsync(cancellationToken));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Chatbox.Dispose();
        Echo.Dispose();

        // Before the query service: the harvester holds subscriptions to both it and the epoch.
        _harvester.Dispose();
        _queryService.Dispose();
        _sender.Dispose();
    }
}
