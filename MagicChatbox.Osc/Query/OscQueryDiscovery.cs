using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace MagicChatbox.Osc.Query;

/// <summary>The two DNS-SD service types OSCQuery uses.</summary>
public static class OscQueryServiceTypes
{
    /// <summary>The OSCQuery HTTP endpoint. Where a peer fetches <c>?HOST_INFO</c> and the node tree.</summary>
    public const string OscJsonTcp = "_oscjson._tcp";

    /// <summary>The raw OSC UDP endpoint. Where a peer sends messages.</summary>
    public const string OscUdp = "_osc._udp";
}

/// <summary>One resolved mDNS advertisement.</summary>
/// <param name="InstanceName">The instance label, e.g. <c>VRChat-Client-A1B2C3</c>.</param>
/// <param name="ServiceType">One of <see cref="OscQueryServiceTypes"/>.</param>
/// <param name="Address">The address from the accompanying A record.</param>
/// <param name="Port">The port from the SRV record.</param>
/// <param name="Expired">True when the record's TTL was zero — the peer is going away.</param>
public readonly record struct OscQueryAdvertisement(
    string InstanceName,
    string ServiceType,
    IPAddress Address,
    int Port,
    bool Expired = false)
{
    /// <summary>The dedupe key: an advertisement is the same one when both the name and port match.</summary>
    public string Key => $"{InstanceName}.{ServiceType}:{Port}";
}

/// <summary>Advertises us over mDNS and reports the advertisements we hear.</summary>
/// <remarks>
/// An interface because the implementation opens a multicast socket, and the rest of the subsystem — the
/// peer-selection rules, the reconnect schedule, the endpoint provider — must be testable without one.
/// Every behaviour that decides anything lives on this side of the seam; the implementation only carries
/// bytes.
/// </remarks>
public interface IOscQueryDiscovery : IDisposable
{
    /// <summary>Raised for each SRV record heard, on an arbitrary thread.</summary>
    event Action<OscQueryAdvertisement>? AdvertisementReceived;

    /// <summary>Starts the multicast listener.</summary>
    void Start();

    /// <summary>Advertises our two services so a peer can find us.</summary>
    void Advertise(string instanceName, IPAddress address, int httpPort, int oscPort);

    /// <summary>Sends a query for both OSCQuery service types. Called on a timer while unconnected.</summary>
    void Query();
}

/// <summary>
/// Decides which advertisements matter, and which of two competing ones wins.
/// </summary>
/// <remarks>
/// <para>
/// Pure, and separate from the mDNS stack, because every rule in it was learned the hard way and none of
/// them can be verified by reading a multicast socket:
/// </para>
/// <list type="bullet">
///   <item><b>Instance-name prefix.</b> The caller supplies it — <c>"VRChat-Client-"</c> in practice.
///   Keeping the string out of this assembly is what lets <c>MagicChatbox.Osc</c> stay honest about
///   knowing nothing about VRChat (§12); it speaks OSCQuery, and <c>Vrc</c> knows whose client to look
///   for.</item>
///   <item><b>Loopback beats LAN.</b> A machine can hear its own VRChat twice, once per interface. v2
///   handles this at <c>OscQueryServer.cs:154-158</c>; taking the LAN address for a local client routes
///   every message out to a switch and back, and breaks entirely on a VPN.</item>
///   <item><b>Our own advertisement is not a peer.</b> We advertise the same service types we listen
///   for, and answering our own handshake is a loop.</item>
/// </list>
/// </remarks>
public sealed class OscQueryPeerSelector
{
    private readonly string _instanceNamePrefix;
    private readonly string _ownInstanceName;
    private readonly Dictionary<string, OscQueryAdvertisement> _seen = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    /// <param name="instanceNamePrefix">Only instances starting with this are peers. Case-sensitive, as mDNS labels are.</param>
    /// <param name="ownInstanceName">Our own advertised instance name, so we can ignore ourselves.</param>
    public OscQueryPeerSelector(string instanceNamePrefix, string ownInstanceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceNamePrefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownInstanceName);

        _instanceNamePrefix = instanceNamePrefix;
        _ownInstanceName = ownInstanceName;
    }

    /// <summary>The HTTP endpoint of the peer we are currently talking to, if any.</summary>
    public IPEndPoint? SelectedHttpEndpoint { get; private set; }

    /// <summary>The peer's raw OSC UDP endpoint, when its <c>_osc._udp</c> record has been heard.</summary>
    public IPEndPoint? SelectedOscEndpoint { get; private set; }

    /// <summary>Considers one advertisement. True when it changed the selection and the caller should act.</summary>
    public bool TryAccept(OscQueryAdvertisement advertisement, [NotNullWhen(true)] out IPEndPoint? endpoint)
    {
        endpoint = null;

        if (!advertisement.InstanceName.StartsWith(_instanceNamePrefix, StringComparison.Ordinal)
            || advertisement.InstanceName.Equals(_ownInstanceName, StringComparison.Ordinal))
        {
            return false;
        }

        lock (_gate)
        {
            if (advertisement.Expired)
            {
                _seen.Remove(advertisement.Key);
                Forget(advertisement);
                return false;
            }

            var current = advertisement.ServiceType == OscQueryServiceTypes.OscUdp
                ? SelectedOscEndpoint
                : SelectedHttpEndpoint;

            // A local client heard twice: the loopback answer is the real one. Checked before the dedupe
            // table is touched, so the LAN advertisement is not remembered as "already handled" — if the
            // loopback route later goes away, the LAN one must still be adoptable.
            if (current is not null
                && IPAddress.IsLoopback(current.Address)
                && !IPAddress.IsLoopback(advertisement.Address))
            {
                return false;
            }

            if (!_seen.TryAdd(advertisement.Key, advertisement))
            {
                return false;
            }

            endpoint = new IPEndPoint(advertisement.Address, advertisement.Port);

            if (advertisement.ServiceType == OscQueryServiceTypes.OscUdp)
            {
                SelectedOscEndpoint = endpoint;
            }
            else
            {
                SelectedHttpEndpoint = endpoint;
            }

            return true;
        }
    }

    /// <summary>Drops everything remembered, so a restarted VRChat is rediscovered rather than deduped away.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _seen.Clear();
            SelectedHttpEndpoint = null;
            SelectedOscEndpoint = null;
        }
    }

    private void Forget(OscQueryAdvertisement advertisement)
    {
        var gone = new IPEndPoint(advertisement.Address, advertisement.Port);

        if (advertisement.ServiceType == OscQueryServiceTypes.OscUdp)
        {
            if (gone.Equals(SelectedOscEndpoint))
            {
                SelectedOscEndpoint = null;
            }
        }
        else if (gone.Equals(SelectedHttpEndpoint))
        {
            SelectedHttpEndpoint = null;
        }
    }
}
