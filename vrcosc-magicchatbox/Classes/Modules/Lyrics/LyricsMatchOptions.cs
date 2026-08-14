using System.ComponentModel;

namespace vrcosc_magicchatbox.Classes.Modules.Lyrics;

public enum LyricsMatchStrictness
{
    [Description("Relaxed - more songs, more wrong guesses")]
    Relaxed,

    [Description("Balanced - the usual choice")]
    Balanced,

    [Description("Strict - only confident matches")]
    Strict,
}

/// <summary>How hard a candidate has to work to be accepted.</summary>
public readonly record struct LyricsMatchOptions
{
    /// <summary>Total score a candidate must reach. Zero means the balanced default.</summary>
    public double AcceptThreshold { get; init; }

    /// <summary>
    /// Set when the search that produced the candidates had detail removed from the title, which
    /// leaves the running time as the only evidence that this is the same recording.
    /// </summary>
    public bool RequireCloseDuration { get; init; }

    public static LyricsMatchOptions For(LyricsMatchStrictness strictness, bool requireCloseDuration = false)
        => new()
        {
            AcceptThreshold = strictness switch
            {
                LyricsMatchStrictness.Relaxed => 0.52,
                LyricsMatchStrictness.Strict => 0.74,
                _ => LyricsMatchScorer.BalancedThreshold,
            },
            RequireCloseDuration = requireCloseDuration,
        };
}
