using MagicChatbox.Osc.Query;
using MagicChatbox.Vocabulary;

namespace MagicChatbox.Vrc;

/// <summary>
/// The one place <c>OscTransportReason</c> becomes <see cref="ReasonCode"/>.
/// </summary>
/// <remarks>
/// <para>
/// <c>MagicChatbox.Osc</c> has no project references by design, so it defines its own closed reason
/// enum instead of using <see cref="ReasonCode"/>, and says so in its own remarks: <i>"the seam is open
/// and known — whoever owns Vrc maps these onto ReasonCode."</i> This is that seam, closed, in the one
/// assembly that references both vocabularies.
/// </para>
/// <para>
/// <b>The mapping is total and the compiler enforces it.</b> There is no discard arm: a discard that
/// fell through to <see cref="ReasonCode.Ok"/> would turn a future transport failure into a silent
/// success, which is the failure mode this whole subsystem exists to prevent. The project elevates
/// <c>CS8509</c> to an error, so adding a member to <c>OscTransportReason</c> without deciding what it
/// means here <b>fails the build</b> rather than passing review.
/// </para>
/// <para>
/// <b>Three mappings are lossy, and they are lossy because <see cref="ReasonCode"/> has no member for
/// what they say.</b> None of them is forced into a code that would read as a lie:
/// </para>
/// <list type="bullet">
///   <item>
///   <b><c>NoDiscovery</c> collapses onto <see cref="ReasonCode.NotConnected"/>,</b> the same code as
///   <c>NoClient</c> — and telling those two apart is the entire reason <c>OscTransportReason</c>
///   distinguishes them: one is fixed by starting VRChat, the other by turning off a VPN adapter. The
///   distinction survives only in <c>Detail</c> until <see cref="ReasonCode"/> grows a
///   <c>DiscoveryBlocked</c> member.
///   </item>
///   <item>
///   <b><c>SchemaFromFileOnly</c> and <c>LoopbackOnly</c> have no fit at all.</b> Both describe a
///   working transport with reduced capability, and every candidate code asserts something untrue:
///   <see cref="ReasonCode.Ok"/> claims nothing is wrong, <see cref="ReasonCode.SourceFaulted"/> claims
///   something broke, <see cref="ReasonCode.Stale"/> is about a value's age. They map to
///   <see cref="ReasonCode.None"/> — "no reason recorded" — and rely on
///   <c>VrcTransportStatus.IsDegraded</c> to keep the reading honest. That is a gap in the vocabulary,
///   not a decision, and it is written down here so it is fixed rather than rediscovered.
///   </item>
/// </list>
/// </remarks>
internal static class VrcTransportReasonMap
{
    /// <summary>Translates one OSC transport reason into the kernel's vocabulary.</summary>
    // CS8524 is suppressed rather than answered with a discard arm: the only values it worries about are
    // undefined ones cast into the enum, and admitting a discard would also silence CS8509 — the
    // diagnostic that makes a newly added member fail the build.
#pragma warning disable CS8524
    internal static ReasonCode ToReasonCode(OscTransportReason reason) => reason switch
    {
        // Discovered, reachable, receiving.
        OscTransportReason.Connected => ReasonCode.Ok,

        // VRChat is not running. The exact case ReasonCode.NotConnected was written for.
        OscTransportReason.NoClient => ReasonCode.NotConnected,

        // Lossy: indistinguishable from NoClient in this vocabulary. See the remarks.
        OscTransportReason.NoDiscovery => ReasonCode.NotConnected,

        // A peer exists but its HOST_INFO could not be read. A fault with a diagnostic and a retry.
        OscTransportReason.HostInfoUnreachable => ReasonCode.SourceFaulted,

        // No fit. Working transport, degraded schema provenance (P6). See the remarks.
        OscTransportReason.SchemaFromFileOnly => ReasonCode.None,

        // No fit. Working transport, reduced reach. See the remarks.
        OscTransportReason.LoopbackOnly => ReasonCode.None,

        // We cannot be discovered. Our own subsystem failed; detail carries which port range.
        OscTransportReason.HttpPortUnavailable => ReasonCode.SourceFaulted,

        // Nothing can arrive at all. Our own socket failed.
        OscTransportReason.ReceiveBindFailed => ReasonCode.SourceFaulted,

        // The endpoint works; it is simply a guess rather than a negotiated fact. Degraded, not failed.
        OscTransportReason.ManualOverride => ReasonCode.Ok,

        // Discovered once, silent now, backing off. Not connected right now.
        OscTransportReason.Reconnecting => ReasonCode.NotConnected,

        // Stopped on purpose, which is what SourceDisabled means.
        OscTransportReason.Stopped => ReasonCode.SourceDisabled,
    };
#pragma warning restore CS8524

    /// <summary>Translates a whole reading, preserving the degraded bit the reason code cannot carry.</summary>
    internal static VrcTransportStatus ToVrcStatus(in OscTransportStatus status) => new(
        ToReasonCode(status.Reason),
        status.Detail,
        status.IsDegraded,
        status.AttemptCount,
        status.NextRetryUtc);
}

/// <summary>Adapts the OSC status stream onto <see cref="IVrcTransportStatusSink"/>.</summary>
/// <remarks>
/// Internal because it implements an <c>Osc</c> interface, and <c>Vrc</c>'s public surface names no
/// <c>Osc</c> type except <c>IOscEndpointProvider</c> — the property that lets <c>Core</c> stay off the
/// wire entirely (D5, part 2). It is asserted by <c>VrcPublicSurface_LeaksNoOscType</c>.
/// </remarks>
internal sealed class VrcTransportStatusAdapter : IOscTransportStatusSink
{
    private readonly IVrcTransportStatusSink _inner;

    internal VrcTransportStatusAdapter(IVrcTransportStatusSink inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    /// <summary>The most recent reading, already translated.</summary>
    internal VrcTransportStatus Last { get; private set; } = VrcTransportStatus.NotStarted;

    /// <inheritdoc />
    public void OnStatus(OscTransportStatus status)
    {
        var translated = VrcTransportReasonMap.ToVrcStatus(status);
        Last = translated;
        _inner.OnStatus(translated);
    }
}
