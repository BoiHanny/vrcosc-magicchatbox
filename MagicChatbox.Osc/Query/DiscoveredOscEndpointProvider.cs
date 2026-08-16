using System.Net;

namespace MagicChatbox.Osc.Query;

/// <summary>How the manual port override relates to what discovery found.</summary>
public enum OscEndpointOverrideMode
{
    /// <summary>No override. Discovery or nothing — the correct production default.</summary>
    Off = 0,

    /// <summary>Use the override only while discovery has found nothing. The setting to offer a user whose mDNS is blocked.</summary>
    Fallback = 1,

    /// <summary>Use the override even when discovery has found something. VRCOSC's <c>ForceStart</c>: a diagnostic, not a mode to live in.</summary>
    Forced = 2,
}

/// <summary>
/// Supplies the egress endpoint, preferring what OSCQuery discovered and falling back to a hand-entered
/// one only when asked.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the override is never the default.</b> 9000/9001 are VRChat's ports only when VRChat is the
/// only OSC application running, and in this community it never is. The moment a second one starts, the
/// ports move — and hard-coding them does not produce an error, it produces silence: messages leave, land
/// on a port nobody is reading, and the application looks alive while doing nothing (§12.1). Discovery is
/// the mechanism; the override is the escape hatch for the one failure it cannot fix, which is mDNS being
/// blocked by a firewall or a VPN adapter.
/// </para>
/// <para>
/// <see cref="OscEndpointOverrideMode.Forced"/> exists because VRCOSC ships the equivalent and users need
/// it to diagnose. It is not the same setting as <see cref="OscEndpointOverrideMode.Fallback"/> and the
/// UI must not merge them: forcing silently overrides a *working* discovery, which is a great way to
/// spend an evening on a problem you created.
/// </para>
/// </remarks>
public sealed class DiscoveredOscEndpointProvider : IOscEndpointProvider
{
    /// <summary>VRChat's documented default receive port — what it listens on when nothing else has taken it.</summary>
    public const int DefaultVrchatReceivePort = 9000;

    /// <summary>VRChat's documented default send port — what it sends to when nothing else has taken it.</summary>
    public const int DefaultVrchatSendPort = 9001;

    private IPEndPoint? _discovered;
    private IPEndPoint _manual = new(IPAddress.Loopback, DefaultVrchatReceivePort);

    // Held as an int so the read and the write are plainly atomic: this is read from the send path while
    // the UI thread changes it.
    private int _mode = (int)OscEndpointOverrideMode.Off;

    /// <inheritdoc />
    public IPEndPoint? Current
    {
        get
        {
            var discovered = Volatile.Read(ref _discovered);

            return OverrideMode switch
            {
                OscEndpointOverrideMode.Forced => Volatile.Read(ref _manual),
                OscEndpointOverrideMode.Fallback => discovered ?? Volatile.Read(ref _manual),
                _ => discovered,
            };
        }
    }

    /// <summary>What discovery found, or null when it has found nothing.</summary>
    public IPEndPoint? Discovered => Volatile.Read(ref _discovered);

    /// <summary>The endpoint the override would use.</summary>
    public IPEndPoint ManualEndpoint => Volatile.Read(ref _manual);

    /// <summary>How the override is currently configured.</summary>
    public OscEndpointOverrideMode OverrideMode => (OscEndpointOverrideMode)Volatile.Read(ref _mode);

    /// <summary>Records what OSCQuery discovered. Null clears it — VRChat went away.</summary>
    public void SetDiscovered(IPEndPoint? endpoint) => Volatile.Write(ref _discovered, endpoint);

    /// <summary>Points the override at a specific endpoint and chooses how it competes with discovery.</summary>
    public void SetManualOverride(IPEndPoint endpoint, OscEndpointOverrideMode mode)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        if (mode == OscEndpointOverrideMode.Off)
        {
            throw new ArgumentException(
                $"Use {nameof(ClearManualOverride)} to turn the override off; setting an endpoint and " +
                "disabling it in one call reads as an accident.", nameof(mode));
        }

        Volatile.Write(ref _manual, endpoint);
        Volatile.Write(ref _mode, (int)mode);
    }

    /// <summary>Turns the override off, leaving discovery in sole charge.</summary>
    public void ClearManualOverride() => Volatile.Write(ref _mode, (int)OscEndpointOverrideMode.Off);

    /// <summary>The status to publish for the endpoint currently in use.</summary>
    /// <remarks>
    /// An endpoint that came from the override is a degraded state even when it works, because the number
    /// is a guess: it is right until another OSC application starts, and then it is silently wrong.
    /// </remarks>
    public OscTransportStatus DescribeStatus() => Current switch
    {
        null =>
            new OscTransportStatus(OscTransportReason.NoClient, "No VRChat client discovered yet."),
        var endpoint when OverrideMode == OscEndpointOverrideMode.Forced =>
            new OscTransportStatus(OscTransportReason.ManualOverride, $"Forcing OSC egress to {endpoint}, ignoring discovery."),
        var endpoint when Discovered is null =>
            new OscTransportStatus(OscTransportReason.ManualOverride, $"Using the manual OSC endpoint {endpoint}; discovery has found nothing."),
        var endpoint =>
            OscTransportStatus.Connected($"Sending OSC to the discovered endpoint {endpoint}."),
    };
}
