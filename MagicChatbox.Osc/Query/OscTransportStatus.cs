namespace MagicChatbox.Osc.Query;

/// <summary>
/// Why the transport is in the state it is in. A closed enumeration, not a log message.
/// </summary>
/// <remarks>
/// <para>
/// Every failure mode in §12.3 has an entry here, because the user-visible consequence of each is
/// identical — nothing arrives from VRChat — while the fix is completely different. "mDNS is blocked by
/// your VPN" and "VRChat is not running" cannot be told apart from the symptom, so the status has to
/// carry which one it was. A log line does not reach the status bar, so this is a value.
/// </para>
/// <para>
/// <b>Deliberately NOT <c>MagicChatbox.Vocabulary.ReasonCode</c>.</b> This assembly references no other
/// project, and OSCQuery is not a reason to break that. The shape is copied instead: <c>ushort</c>-backed,
/// closed, explicitly and stably numbered, so that reconciling the two later is a mapping table rather
/// than a renumbering. <b>The seam is open and known</b> — whoever owns <c>Vrc</c> maps these onto
/// <c>ReasonCode</c>, which is the assembly that already references both.
/// </para>
/// </remarks>
public enum OscTransportReason : ushort
{
    /// <summary>Discovered, reachable, receiving. Not a degraded state.</summary>
    Connected = 0,

    /// <summary>Discovery is running and no <c>VRChat-Client-*</c> advertisement has arrived. VRChat is probably not running.</summary>
    NoClient = 1,

    /// <summary>
    /// We advertise, but nothing is ever discovered. mDNS is being blocked — a firewall, a VPN adapter,
    /// or a virtual switch eating multicast. The manual port override exists for exactly this, and the
    /// UI should offer it here, clearly labelled as a fallback.
    /// </summary>
    NoDiscovery = 2,

    /// <summary>A peer was discovered but its <c>?HOST_INFO</c> could not be fetched. Reconnect backoff owns this.</summary>
    HostInfoUnreachable = 3,

    /// <summary>The peer's tree was fetched and contained no parameters. Schema falls back to the avatar config JSON (P6).</summary>
    SchemaFromFileOnly = 4,

    /// <summary>
    /// Our HTTP server could only bind loopback. OSCQuery over HTTP is loopback-only in practice on
    /// Windows, and §12.3 requires that be a <i>named</i> mode from day one rather than a silent
    /// failure a LAN user spends an evening on.
    /// </summary>
    LoopbackOnly = 5,

    /// <summary>Every candidate HTTP port was taken. We keep trying; startup never fails over this.</summary>
    HttpPortUnavailable = 6,

    /// <summary>The OSC receive socket could not be bound. Nothing can arrive until it is.</summary>
    ReceiveBindFailed = 7,

    /// <summary>The endpoint came from the manual override, not from discovery. Correct, and worth saying out loud.</summary>
    ManualOverride = 8,

    /// <summary>A discovered peer stopped answering and we are backing off before retrying.</summary>
    Reconnecting = 9,

    /// <summary>The transport was stopped deliberately.</summary>
    Stopped = 10,
}

/// <summary>A point-in-time transport health reading, suitable for publishing to the UI.</summary>
/// <param name="Reason">The machine-readable cause.</param>
/// <param name="Detail">Human context for the status bar. Never the only carrier of meaning.</param>
/// <param name="AttemptCount">Consecutive failures behind this status, 0 when healthy.</param>
/// <param name="NextRetryUtc">When the next attempt is due, when one is scheduled.</param>
public readonly record struct OscTransportStatus(
    OscTransportReason Reason,
    string Detail,
    int AttemptCount = 0,
    DateTimeOffset? NextRetryUtc = null)
{
    /// <summary>True for every reason except <see cref="OscTransportReason.Connected"/>.</summary>
    public bool IsDegraded => Reason != OscTransportReason.Connected;

    /// <summary>The healthy reading.</summary>
    public static OscTransportStatus Connected(string detail) => new(OscTransportReason.Connected, detail);
}

/// <summary>Receives every transport status change.</summary>
/// <remarks>
/// An interface rather than an event so the consumer is a declared collaborator: a status nobody is
/// listening to is the silent failure this whole type exists to prevent.
/// </remarks>
public interface IOscTransportStatusSink
{
    /// <summary>Called whenever the transport's health reading changes.</summary>
    void OnStatus(OscTransportStatus status);
}
