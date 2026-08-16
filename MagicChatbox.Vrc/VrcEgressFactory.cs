using MagicChatbox.Osc;

namespace MagicChatbox.Vrc;

/// <summary>Builds the egress path. The only public way to obtain an <see cref="IVrcEgress"/>.</summary>
/// <remarks>
/// <para>
/// This factory exists because the raw sender is <c>internal</c> to <c>MagicChatbox.Osc</c> and visible
/// only here. A caller in <c>Core</c> cannot construct one, cannot obtain one, and cannot reference the
/// assembly that defines it — so the safety pipeline is not something a caller remembers to use, it is
/// the only thing there is.
/// </para>
/// <para>
/// Note what the caller supplies: an <i>endpoint provider</i>, the world and profanity policies, and
/// optionally a cadence and a journal. It never supplies a sender, and there is no overload that accepts
/// one. That asymmetry is deliberate.
/// </para>
/// </remarks>
public static class VrcEgressFactory
{
    /// <summary>Creates the egress facade and the socket it owns.</summary>
    /// <param name="endpoints">Supplies VRChat's negotiated OSC endpoint. See OSCQuery discovery.</param>
    /// <param name="world">Consulted before every chatbox send.</param>
    /// <param name="profanity">Consulted before every chatbox send.</param>
    /// <param name="cadence">Courtesy cadence. Defaults to <see cref="ChatboxCadence.DefaultInterval"/>.</param>
    /// <param name="journal">Where dispatches and blocks are recorded. Defaults to discarding them.</param>
    public static IVrcEgress Create(
        IOscEndpointProvider endpoints,
        IWorldPolicy world,
        IProfanityPolicy profanity,
        IChatboxCadence? cadence = null,
        IEgressJournal? journal = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        return new VrcEgress(
            new UdpOscSender(endpoints),
            world,
            profanity,
            cadence ?? new ChatboxCadence(),
            journal);
    }
}
