namespace MagicChatbox.Kernel;

/// <summary>
/// Whether there is a meaningful value right now. Absent is not zero, and stale is not either.
/// </summary>
/// <remarks>
/// A first-class field on every <see cref="Cell"/>, never a sentinel value. "Spotify is not connected"
/// is this product's commonest user-visible state, and in v2 every provider invented its own magic
/// string for it — <c>"Nothing playing"</c>, <c>"Idle"</c>, <c>"No media session"</c> — which the
/// composer then rendered into the chatbox as if it were a track title.
/// <para>
/// The reason a cell is not <see cref="Live"/> travels beside it as a <c>ReasonCode</c>; this enum
/// answers "should I believe the value" and the reason answers "why not".
/// </para>
/// </remarks>
public enum Availability : byte
{
    /// <summary>This key has never carried a value. The cell exists so the UI can say so.</summary>
    Never = 0,

    /// <summary>A current reading.</summary>
    Live = 1,

    /// <summary>
    /// Had one; the descriptor's <c>StaleAfterMs</c> elapsed without a refresh and the staleness sweep
    /// flipped it. The last good value is retained — it is old, not wrong.
    /// </summary>
    Stale = 2,

    /// <summary>
    /// The owning source says it cannot produce a reading right now. The last good value is retained
    /// so a reconnect does not have to re-derive it, but nothing may render it as current.
    /// </summary>
    Unavailable = 3,
}
