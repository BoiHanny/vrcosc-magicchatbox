using System.Net;

namespace MagicChatbox.Osc.Query;

/// <summary>One program that announced itself over OSCQuery on the local network.</summary>
/// <remarks>
/// <b>An mDNS instance label is not a process.</b> <see cref="InstanceName"/> is the string the other
/// program chose to publish, and nothing here verified that a process by that name exists, is running, or
/// wrote the record. Every surface that renders one has to say "something advertising itself as
/// VRCFaceTracking" rather than "VRCFaceTracking", because overstating a boundary is worse than not
/// drawing one. <b>Nor is it necessarily this machine</b> — see <see cref="NeighbourRegistry"/>.
/// </remarks>
/// <param name="InstanceName">The instance label, e.g. <c>VRCFaceTracking</c>. Chosen by that program.</param>
/// <param name="ServiceType">One of <see cref="OscQueryServiceTypes"/>.</param>
/// <param name="Address">
/// The address from the accompanying A record, and the only thing here that says which computer this is.
/// </param>
/// <param name="Port">The port from the SRV record.</param>
/// <param name="FirstHeardUtc">When this application first heard this instance-and-port.</param>
/// <param name="LastHeardUtc">When it last heard it. <b>This, not presence in the list, is the freshness.</b></param>
public readonly record struct OscNeighbour(
    string InstanceName,
    string ServiceType,
    IPAddress Address,
    int Port,
    DateTimeOffset FirstHeardUtc,
    DateTimeOffset LastHeardUtc);

/// <summary>
/// Everything else on the wire that speaks OSCQuery, kept instead of thrown away.
/// </summary>
/// <remarks>
/// <para>
/// <b>"On the wire", not "on this machine", and the difference is a claim this type cannot make.</b>
/// mDNS is multicast over the local network: an announcement from a flatmate's PC arrives on the same
/// group as one from a program running beside us, and the only filter below is
/// <see cref="_ownInstanceName"/> — a name, never an address. Deciding locality would mean enumerating
/// this machine's interfaces, which is exactly the I/O the "pure, no socket" line below promises not to
/// do, and testing loopback alone is wrong in the direction that matters: a local program whose responder
/// publishes only its LAN address would be dropped from the one table that exists to name it. So the row
/// carries <see cref="OscNeighbour.Address"/> and the surface tells the reader to look at it. Only a
/// program on this machine can be holding a port MagicChatbox wanted, and the address is what says which.
/// </para>
/// <para>
/// <b>The data is already arriving.</b> <see cref="IOscQueryDiscovery.AdvertisementReceived"/> fires for
/// every SRV record heard on <c>_oscjson._tcp</c> and <c>_osc._udp</c>, and
/// <see cref="OscQueryPeerSelector.TryAccept"/> returns false for everything that is not the peer we are
/// looking for. Until this type existed, that "false" was the end of the record's life. The VRChat OSC
/// ecosystem is a dozen small tools and a normal power user runs three of them; when they interfere, every
/// one of them says some variant of "cannot connect" and none says <i>which other program</i>. This is the
/// list that ends that conversation.
/// </para>
/// <para>
/// <b>Pure, and no socket.</b> Same seam discipline <see cref="MDnsOscQueryDiscovery"/>'s remarks describe:
/// the multicast stack carries bytes and this decides what they mean, so every rule below is assertable
/// without joining a multicast group.
/// </para>
/// <para>
/// <b>Our own advertisement is excluded and the peer's is not.</b> We publish the same two service types we
/// listen for, and listing ourselves in a table of everything else is noise at best and a support thread at
/// worst. The VRChat client is kept, though the selector accepts it rather than rejecting it, because §8's
/// table leads with <c>VRChat-Client-4A2F</c> — "who else is on the wire" is a more useful question than
/// "who did the selector refuse", and the client being present is exactly what someone diagnosing silence
/// wants to see.
/// </para>
/// <para>
/// <b>Presence in this list is never a claim that something is running.</b> mDNS is best-effort, a program
/// that exits without a goodbye leaves its record behind until the TTL runs out, and an OSC application
/// that does not implement OSCQuery is invisible here entirely — plenty do not. The list is "who announced
/// themselves", never "everything that is running", and <see cref="OscNeighbour.LastHeardUtc"/> is carried
/// so a reader can judge for themselves rather than trusting a row's existence.
/// </para>
/// </remarks>
public sealed class NeighbourRegistry
{
    /// <summary>
    /// How long an entry survives without being heard again.
    /// </summary>
    /// <remarks>
    /// Generous on purpose, and the reason is in <see cref="OscQueryService"/>'s own query loop: it
    /// re-queries only while nothing is connected, so once VRChat is found we stop soliciting answers and
    /// hear only unsolicited announcements — which a program sends when it starts and then not again. A
    /// short window would therefore empty this table during exactly the healthy, connected session in
    /// which someone opens Diagnostics to ask who else is here. The honest freshness is the last-heard
    /// stamp on each row, not the presence of the row.
    /// </remarks>
    public static readonly TimeSpan DefaultTimeToLive = TimeSpan.FromMinutes(15);

    /// <summary>
    /// A ceiling on tracked neighbours, so the table cannot grow without bound.
    /// </summary>
    /// <remarks>
    /// Two entries per program — one per service type — so 64 is 32 OSC applications on one machine,
    /// against a real ecosystem of about a dozen. It exists for the case the entries are not the same
    /// program twice: <see cref="OscQueryServiceOptions.InstanceName"/> shows why an instance label can be
    /// randomised per launch, and a machine that has started such a program many times without a clean
    /// goodbye would otherwise accumulate one row per launch.
    /// </remarks>
    public const int MaxTracked = 64;

    private readonly Dictionary<string, OscNeighbour> _heard = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();
    private readonly string _ownInstanceName;
    private readonly TimeSpan _timeToLive;
    private readonly TimeProvider _time;

    /// <param name="ownInstanceName">Our own advertised instance name, so we do not list ourselves.</param>
    /// <param name="timeToLive">Defaults to <see cref="DefaultTimeToLive"/>.</param>
    /// <param name="time">The clock behind first-heard and last-heard. Defaults to the system clock.</param>
    public NeighbourRegistry(string ownInstanceName, TimeSpan? timeToLive = null, TimeProvider? time = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownInstanceName);

        var ttl = timeToLive ?? DefaultTimeToLive;
        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeToLive), ttl, "The neighbour TTL must be positive.");
        }

        _ownInstanceName = ownInstanceName;
        _timeToLive = ttl;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>How many neighbours are currently remembered, expiry applied.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                Prune(_time.GetUtcNow());
                return _heard.Count;
            }
        }
    }

    /// <summary>
    /// Folds one advertisement in. Safe to call from the mDNS callback thread.
    /// </summary>
    /// <remarks>
    /// An expiring record — TTL zero, the goodbye a program multicasts as it exits — removes the entry
    /// rather than refreshing it. Keeping it would leave the one case where mDNS tells us plainly that
    /// something has gone, and still show it as present.
    /// </remarks>
    /// <returns>True when the table changed.</returns>
    public bool Record(in OscQueryAdvertisement advertisement)
    {
        if (advertisement.InstanceName.Length == 0
            || advertisement.InstanceName.Equals(_ownInstanceName, StringComparison.Ordinal))
        {
            return false;
        }

        var now = _time.GetUtcNow();
        var key = advertisement.Key;

        lock (_gate)
        {
            Prune(now);

            if (advertisement.Expired)
            {
                return _heard.Remove(key);
            }

            if (_heard.TryGetValue(key, out var existing))
            {
                // The address and port are re-taken from the record rather than kept from the first
                // sighting: a program that restarts on a new address keeps its label, and a row naming
                // where it used to be is worse than no row.
                _heard[key] = existing with
                {
                    Address = advertisement.Address,
                    LastHeardUtc = now,
                };

                return true;
            }

            if (_heard.Count >= MaxTracked && !TryEvictLeastRecent())
            {
                return false;
            }

            _heard[key] = new OscNeighbour(
                advertisement.InstanceName,
                advertisement.ServiceType,
                advertisement.Address,
                advertisement.Port,
                now,
                now);

            return true;
        }
    }

    /// <summary>Everyone currently remembered, in a stable order suitable for a table.</summary>
    /// <remarks>
    /// Ordered by label, then service type, then port — not by when they were heard. A table whose rows
    /// reorder themselves every time an announcement lands is one nobody can read, and the last-heard
    /// column already carries the recency the ordering would otherwise be smuggling.
    /// </remarks>
    public IReadOnlyList<OscNeighbour> List()
    {
        lock (_gate)
        {
            Prune(_time.GetUtcNow());

            if (_heard.Count == 0)
            {
                return [];
            }

            var rows = new List<OscNeighbour>(_heard.Values);
            rows.Sort(CompareForDisplay);
            return rows;
        }
    }

    private static int CompareForDisplay(OscNeighbour left, OscNeighbour right)
    {
        var byName = string.Compare(left.InstanceName, right.InstanceName, StringComparison.OrdinalIgnoreCase);
        if (byName != 0)
        {
            return byName;
        }

        var byService = string.CompareOrdinal(left.ServiceType, right.ServiceType);
        return byService != 0 ? byService : left.Port.CompareTo(right.Port);
    }

    private void Prune(DateTimeOffset now)
    {
        if (_heard.Count == 0)
        {
            return;
        }

        List<string>? expired = null;

        foreach (var (key, neighbour) in _heard)
        {
            if (now - neighbour.LastHeardUtc >= _timeToLive)
            {
                (expired ??= []).Add(key);
            }
        }

        if (expired is null)
        {
            return;
        }

        foreach (var key in expired)
        {
            _heard.Remove(key);
        }
    }

    /// <remarks>
    /// Evict the least recently heard rather than refuse the newcomer. Refusing means that once the table
    /// fills with programs that have gone quiet, the one that just started — the one somebody is trying to
    /// find — can never appear, which inverts what the cap is for.
    /// </remarks>
    private bool TryEvictLeastRecent()
    {
        string? oldestKey = null;
        var oldestHeard = DateTimeOffset.MaxValue;

        foreach (var (key, neighbour) in _heard)
        {
            if (neighbour.LastHeardUtc < oldestHeard)
            {
                oldestHeard = neighbour.LastHeardUtc;
                oldestKey = key;
            }
        }

        return oldestKey is not null && _heard.Remove(oldestKey);
    }
}
