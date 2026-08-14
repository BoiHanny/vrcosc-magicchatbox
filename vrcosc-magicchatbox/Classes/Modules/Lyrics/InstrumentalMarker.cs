using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace vrcosc_magicchatbox.Classes.Modules.Lyrics;

// Values are written to the settings file, so they are fixed. The gaps are withdrawn styles;
// reusing their numbers would change what a saved setting means.
public enum LyricsInstrumentalMarker
{
    [Description("Single note ♪")]
    Note = 0,

    [Description("Bouncing notes ♪ ♫ ♬")]
    BouncingNotes = 1,

    [Description("Trailing dots ♪ · · ·")]
    TrailingDots = 3,

    [Description("Vinyl ◐ ◓ ◑ ◒")]
    Vinyl = 6,

    [Description("Pulse · • ●")]
    Pulse = 8,

    [Description("Bouncing ball ·●··")]
    BouncingBall = 9,
}

/// <summary>
/// What to show during an intro, an outro or a break between verses. The frame comes from the
/// playback position rather than a timer of its own, so it advances without one. A frame a second
/// matches the default OSC tick.
/// </summary>
public static class InstrumentalMarker
{
    public static readonly TimeSpan FrameDuration = TimeSpan.FromSeconds(1);

    // BMP only: these cost one character each of the 144. Emoji would cost two.
    private static readonly IReadOnlyList<string> NoteFrames = ["♪"];
    private static readonly IReadOnlyList<string> BouncingFrames = ["♪", "♫", "♬", "♫"];
    private static readonly IReadOnlyList<string> DotFrames = ["♪", "♪ ·", "♪ · ·", "♪ · · ·"];

    private static readonly IReadOnlyList<string> VinylFrames = ["◐", "◓", "◑", "◒"];

    private static readonly IReadOnlyList<string> PulseFrames = ["·", "•", "●", "•"];

    private static readonly IReadOnlyList<string> BouncingBallFrames =
        ["●···", "·●··", "··●·", "···●", "··●·", "·●··"];

    public static IReadOnlyList<string> Frames(LyricsInstrumentalMarker style) => style switch
    {
        LyricsInstrumentalMarker.BouncingNotes => BouncingFrames,
        LyricsInstrumentalMarker.TrailingDots => DotFrames,
        LyricsInstrumentalMarker.Vinyl => VinylFrames,
        LyricsInstrumentalMarker.Pulse => PulseFrames,
        LyricsInstrumentalMarker.BouncingBall => BouncingBallFrames,
        _ => NoteFrames,
    };

    public static string Render(LyricsInstrumentalMarker style, TimeSpan position)
    {
        var frames = Frames(style);
        if (frames.Count == 1)
            return frames[0];

        double elapsed = Math.Max(0, position.TotalMilliseconds);
        int index = (int)(elapsed / FrameDuration.TotalMilliseconds) % frames.Count;

        return frames[index];
    }

    /// <summary>The widest frame, so a caller can budget before rendering.</summary>
    public static int MaxWidth(LyricsInstrumentalMarker style) => Frames(style).Max(f => f.Length);
}
