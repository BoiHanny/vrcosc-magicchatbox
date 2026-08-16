using MagicChatbox.Osc.Query;

namespace MagicChatbox.Vrc;

/// <summary>
/// Another program on this machine that announced itself over OSCQuery.
/// </summary>
/// <remarks>
/// <para>
/// A translation of <c>OscNeighbour</c> for the same reason <see cref="VrcTransportStatus"/> is a
/// translation of <c>OscTransportStatus</c>: <c>Vrc</c>'s public surface names no <c>Osc</c> type, which is
/// what lets <c>Core</c> drop its <c>Osc</c> reference entirely (D5, part 2). Note that
/// <c>VrcPublicSurface_LeaksNoOscType</c> would <b>not</b> have caught the alternative — it inspects
/// parameter and return types, and <c>IReadOnlyList&lt;OscNeighbour&gt;</c> is a BCL type with an Osc type
/// inside it. This exists because the fence is a rule about the assembly rather than about what one test
/// can see. The address arrives as text rather than as <c>System.Net.IPAddress</c> because that is all any
/// caller does with it and because the row it lands in is a table.
/// </para>
/// <para>
/// <b><see cref="Label"/> is a name a program chose for itself, not a process this application
/// identified.</b> mDNS carries an instance label and nothing else; there is no pid, no image path and no
/// signature. Copy that renders one has to say "something advertising itself as VRCFaceTracking". Saying
/// "VRCFaceTracking is using port 9001" asserts an identity nobody verified, and the whole value of this
/// feature is that it is trustworthy about a thing people currently guess at.
/// </para>
/// </remarks>
/// <param name="Label">The mDNS instance label. Chosen by that program; see the remarks.</param>
/// <param name="ServiceType"><c>_oscjson._tcp</c> or <c>_osc._udp</c>.</param>
/// <param name="Address">The address from the A record, as text.</param>
/// <param name="Port">The port from the SRV record.</param>
/// <param name="FirstHeardUtc">When this application first heard this label on this port.</param>
/// <param name="LastHeardUtc">
/// When it last heard it. <b>The freshness is this stamp, not the row's existence</b> — a program that
/// exits without a goodbye stays listed until its entry expires.
/// </param>
public readonly record struct VrcNeighbour(
    string Label,
    string ServiceType,
    string Address,
    int Port,
    DateTimeOffset FirstHeardUtc,
    DateTimeOffset LastHeardUtc)
{
    internal static VrcNeighbour From(in OscNeighbour neighbour) => new(
        neighbour.InstanceName,
        neighbour.ServiceType,
        neighbour.Address.ToString(),
        neighbour.Port,
        neighbour.FirstHeardUtc,
        neighbour.LastHeardUtc);
}
