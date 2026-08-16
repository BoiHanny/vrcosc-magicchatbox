using MagicChatbox.Vocabulary;

namespace MagicChatbox.Vrc;

/// <summary>
/// A transport health reading, stated in the vocabulary the rest of the application speaks.
/// </summary>
/// <remarks>
/// <para>
/// <c>MagicChatbox.Osc</c> publishes <c>OscTransportStatus</c>, carrying its own <c>OscTransportReason</c>,
/// because that assembly deliberately references no other project. <c>Vrc</c> references both, so
/// translating is <c>Vrc</c>'s job and this is the translated form.
/// </para>
/// <para>
/// <b>Why <see cref="IsDegraded"/> is carried rather than derived from <see cref="Reason"/>.</b> Two of
/// the eleven OSC reasons — schema-from-file-only and loopback-only — describe a working transport whose
/// <i>capability</i> is reduced, and <see cref="ReasonCode"/> has no member that names either. Deriving
/// "degraded" from the reason code would therefore silently promote both to healthy, which is exactly
/// the silent failure §12.3 requires be named out loud. The bit comes across from
/// <c>OscTransportStatus.IsDegraded</c>, which is computed against the OSC enum where the distinction
/// still exists.
/// </para>
/// </remarks>
/// <param name="Reason">The machine-readable cause, in kernel vocabulary.</param>
/// <param name="Detail">Human context for the status bar. Never the only carrier of meaning.</param>
/// <param name="IsDegraded">True for every reading except a fully healthy one.</param>
/// <param name="AttemptCount">Consecutive failures behind this reading, 0 when healthy.</param>
/// <param name="NextRetryUtc">When the next attempt is due, when one is scheduled.</param>
public readonly record struct VrcTransportStatus(
    ReasonCode Reason,
    string Detail,
    bool IsDegraded,
    int AttemptCount = 0,
    DateTimeOffset? NextRetryUtc = null)
{
    /// <summary>The reading before anything has started.</summary>
    public static VrcTransportStatus NotStarted => new(ReasonCode.SourceDisabled, "Not started.", IsDegraded: true);
}

/// <summary>Receives every transport health change, already translated.</summary>
/// <remarks>
/// An interface rather than an event, for the reason <c>IOscTransportStatusSink</c> gives: a status
/// nobody listens to is the silent failure the type exists to prevent, and a declared collaborator
/// cannot be forgotten as easily as a subscription.
/// </remarks>
public interface IVrcTransportStatusSink
{
    /// <summary>Called whenever the transport's health reading changes.</summary>
    void OnStatus(VrcTransportStatus status);
}

/// <summary>Discards every status. The default until a host wires the Sources screen up.</summary>
public sealed class NullVrcTransportStatusSink : IVrcTransportStatusSink
{
    /// <summary>The shared instance; the type holds no state.</summary>
    public static readonly NullVrcTransportStatusSink Instance = new();

    private NullVrcTransportStatusSink() { }

    /// <inheritdoc />
    public void OnStatus(VrcTransportStatus status) { }
}
