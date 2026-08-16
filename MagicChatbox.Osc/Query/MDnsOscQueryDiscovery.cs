using System.Net;
using MeaMod.DNS.Model;
using MeaMod.DNS.Multicast;

namespace MagicChatbox.Osc.Query;

/// <summary>
/// The real mDNS stack: advertises <c>_oscjson._tcp</c> and <c>_osc._udp</c>, and resolves peers from
/// SRV records.
/// </summary>
/// <remarks>
/// <para>
/// <b>Library choice.</b> MeaMod.DNS 1.0.71, the same build VRCOSC and v2 both ship. It publishes
/// <c>lib/net7.0</c>, which <c>net10.0</c> consumes directly — verified against the resolved package, not
/// assumed. The alternative, <c>VRChat.OSCQuery</c> 0.0.7, was rejected in §12.3 on its own merits: last
/// published 2023, targets <c>net6.0</c>, and cannot serve a value-only query, which is how a peer seeds
/// initial values without re-enumerating a 400-node tree. Both mature reference implementations declined
/// it independently and wrote their own — two votes from people who tried it.
/// </para>
/// <para>
/// <b>This type is deliberately thin and deliberately untested.</b> Everything that decides anything —
/// which peer wins, when to retry, which endpoint to send to — lives in
/// <see cref="OscQueryPeerSelector"/>, <see cref="OscReconnectBackoff"/> and
/// <see cref="DiscoveredOscEndpointProvider"/>, all of which are pure and covered. What remains here is
/// "hand the bytes to MeaMod", which cannot be asserted without a multicast socket and therefore is not
/// pretended to be.
/// </para>
/// </remarks>
public sealed class MDnsOscQueryDiscovery : IOscQueryDiscovery
{
    private readonly MulticastService _multicast;
    private readonly ServiceDiscovery _discovery;
    private readonly List<ServiceProfile> _advertised = [];
    private bool _disposed;

    /// <summary>Creates the stack. Nothing touches the network until <see cref="Start"/>.</summary>
    public MDnsOscQueryDiscovery()
    {
        // IPv6 off: VRChat's OSC is IPv4, and a dual-stack advertisement doubles the answers we then have
        // to de-duplicate. Duplicate suppression is on because a busy network re-announces constantly.
        _multicast = new MulticastService { UseIpv6 = false, IgnoreDuplicateMessages = true };
        _discovery = new ServiceDiscovery(_multicast);

        // A new interface means anything already listening on it has never heard us, so re-announce
        // as well as re-query. Both halves matter: the query finds peers that are already up, the
        // announcement tells peers that are already up about us.
        _multicast.NetworkInterfaceDiscovered += (_, _) =>
        {
            Query();
            AnnounceInBackground();
        };
        _multicast.AnswerReceived += OnAnswerReceived;
    }

    /// <inheritdoc />
    public event Action<OscQueryAdvertisement>? AdvertisementReceived;

    /// <inheritdoc />
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _multicast.Start();
    }

    /// <inheritdoc />
    public void Advertise(string instanceName, IPAddress address, int httpPort, int oscPort)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);
        ArgumentNullException.ThrowIfNull(address);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var profiles = new[]
        {
            new ServiceProfile(instanceName, OscQueryServiceTypes.OscJsonTcp, (ushort)httpPort, [address]),
            new ServiceProfile(instanceName, OscQueryServiceTypes.OscUdp, (ushort)oscPort, [address]),
        };

        foreach (var profile in profiles)
        {
            _discovery.Advertise(profile);
        }

        lock (_advertised)
        {
            _advertised.AddRange(profiles);
        }

        AnnounceInBackground();
    }

    /// <summary>
    /// Multicasts an unsolicited announcement for everything we advertise.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><see cref="ServiceDiscovery.Advertise"/> puts nothing on the network.</b> It only populates
    /// MeaMod's catalogue so an incoming query can be answered. Measured on the wire: advertising
    /// alone produced no traffic at all over a two-second window. <see cref="ServiceDiscovery.Announce"/>
    /// is the method that actually multicasts, and without it a peer only ever learns about us by
    /// asking.
    /// </para>
    /// <para>
    /// That is the difference between the two launch orders. VRChat queries at construction and when
    /// an interface appears, and never refreshes on a timer — so starting MagicChatbox first is found
    /// by VRChat's startup query, while starting it second, mid-session, is not found at all. The
    /// second is the ordinary case: relaunching the companion while the game keeps running.
    /// </para>
    /// <para>
    /// Off the calling thread because <c>Announce</c> sends twice a second apart (RFC 6762 §8.3), and
    /// its caller is the startup path, which runs on the WPF dispatcher. Failures are swallowed for
    /// the same reason <see cref="Query"/> swallows them: sustained silence already surfaces as
    /// <see cref="OscTransportReason.NoDiscovery"/>, which is the signal a person can act on.
    /// </para>
    /// </remarks>
    private void AnnounceInBackground()
    {
        ServiceProfile[] profiles;
        lock (_advertised)
        {
            if (_advertised.Count == 0)
            {
                return;
            }

            profiles = [.. _advertised];
        }

        _ = Task.Run(() =>
        {
            foreach (var profile in profiles)
            {
                if (_disposed)
                {
                    return;
                }

                try
                {
                    _discovery.Announce(profile);
                }
                catch (Exception)
                {
                    // Best effort, exactly as for Query.
                }
            }
        });
    }

    /// <inheritdoc />
    public void Query()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _multicast.SendQuery($"{OscQueryServiceTypes.OscJsonTcp}.local");
            _multicast.SendQuery($"{OscQueryServiceTypes.OscUdp}.local");
        }
        catch (Exception)
        {
            // A query that could not go out is not a failure worth propagating: the caller is a timer and
            // will ask again. Sustained silence surfaces as OscTransportReason.NoDiscovery, which is the
            // signal a user can actually act on.
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            // Per profile rather than the blanket overload, so we withdraw exactly what we published.
            // MeaMod sends these synchronously, so the zero-TTL records are genuinely on the wire
            // before the transport below is torn down.
            ServiceProfile[] profiles;
            lock (_advertised)
            {
                profiles = [.. _advertised];
                _advertised.Clear();
            }

            foreach (var profile in profiles)
            {
                _discovery.Unadvertise(profile);
            }
        }
        catch (Exception)
        {
            // Best effort. A goodbye packet we could not send costs a peer one stale TTL, nothing more.
        }

        // Discovery first, then the transport underneath it. The reverse order pulled the socket out
        // from under the layer still using it, and both calls sat outside the try — so a throw from
        // either abandoned the other.
        try
        {
            _discovery.Dispose();
        }
        catch (Exception)
        {
        }

        try
        {
            _multicast.Dispose();
        }
        catch (Exception)
        {
        }
    }

    private void OnAnswerReceived(object? sender, MessageEventArgs args)
    {
        var handler = AdvertisementReceived;
        if (handler is null)
        {
            return;
        }

        // Both sections, because where a record lands depends on why it was sent. A response to our
        // query puts PTR in ANSWERS and SRV/A in ADDITIONAL, but an unsolicited announcement — the
        // thing a peer multicasts when it starts up — puts SRV and A in ANSWERS. Reading only the
        // additional section therefore made the app deaf to every announcing peer, leaving polling
        // as the only way it could ever find VRChat. This is the same defect VRChat fixed in their
        // own library in vrc-oscquery-lib PR #59.
        var addresses = args.Message.AdditionalRecords.OfType<ARecord>()
            .Concat(args.Message.Answers.OfType<ARecord>())
            .ToList();

        // Loopback first, so a client answering on two interfaces is resolved to the local address before
        // OscQueryPeerSelector ever sees it. The selector re-checks; this just gives it the better input.
        var address = addresses.Find(r => IPAddress.IsLoopback(r.Address))?.Address
                   ?? addresses.FirstOrDefault()?.Address;

        if (address is null)
        {
            return;
        }

        foreach (var srv in args.Message.AdditionalRecords.OfType<SRVRecord>()
                     .Concat(args.Message.Answers.OfType<SRVRecord>()))
        {
            var labels = srv.Name.Labels;
            if (labels.Count < 3)
            {
                continue;
            }

            var serviceType = $"{labels[1]}.{labels[2]}";
            if (serviceType is not (OscQueryServiceTypes.OscJsonTcp or OscQueryServiceTypes.OscUdp))
            {
                continue;
            }

            handler(new OscQueryAdvertisement(
                labels[0],
                serviceType,
                address,
                srv.Port,
                Expired: srv.TTL == TimeSpan.Zero));
        }
    }
}
