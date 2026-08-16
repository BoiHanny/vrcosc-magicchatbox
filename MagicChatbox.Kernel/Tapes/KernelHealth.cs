using System.Collections.Immutable;
using MagicChatbox.Vocabulary;

namespace MagicChatbox.Kernel;

/// <summary>One ejected sink, kept so the UI can name what stopped working.</summary>
/// <param name="Sink">The sink's type name and the grant it held — enough to identify it in the UI.</param>
/// <param name="Tape">Which tape it was subscribed to.</param>
/// <param name="Detail">The last exception's message.</param>
/// <param name="Timestamp">Monotonic ticks at ejection.</param>
public readonly record struct SinkEjection(string Sink, string Tape, string? Detail, long Timestamp);

/// <summary>
/// The kernel's own health, surfaced because some of its failures are invisible otherwise.
/// </summary>
/// <remarks>
/// <b>D12.</b> A sink that throws three times running is removed from the fan-out. If the sink that got
/// ejected is the composer's mailbox, the chatbox freezes on its last composition forever — which is
/// indistinguishable, from the user's side, from "the app is fine and nothing is changing". A logged
/// diagnostic is not a user-visible failure mode, and this failure is user-visible whether it is
/// reported or not.
/// <para>
/// So ejection does two things: it puts a <see cref="OccurrenceKind.SinkEjected"/> occurrence on the
/// tape, and it sets <see cref="IsDegraded"/> here, which the Sources screen and the status bar read.
/// </para>
/// </remarks>
public sealed class KernelHealth
{
    private readonly Lock _gate = new();
    private ImmutableArray<SinkEjection> _ejections = ImmutableArray<SinkEjection>.Empty;

    /// <summary>True once any sink has been ejected. Never clears on its own — the fan-out is smaller than it was.</summary>
    public bool IsDegraded => !Ejections.IsEmpty;

    /// <summary>Every ejection so far, oldest first.</summary>
    public ImmutableArray<SinkEjection> Ejections
    {
        get
        {
            lock (_gate)
            {
                return _ejections;
            }
        }
    }

    /// <summary>The reason code an ejection reports. A sink throwing repeatedly is a fault, and it is named as one.</summary>
    public const ReasonCode EjectionReason = ReasonCode.SourceFaulted;

    internal void RecordEjection(in SinkEjection ejection)
    {
        lock (_gate)
        {
            _ejections = _ejections.Add(ejection);
        }
    }
}
