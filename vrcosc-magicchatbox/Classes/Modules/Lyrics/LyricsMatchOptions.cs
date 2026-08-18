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

public readonly record struct LyricsMatchOptions
{
    public double AcceptThreshold { get; init; }

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
