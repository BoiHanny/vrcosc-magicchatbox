using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace vrcosc_magicchatbox.Classes.Modules.Lyrics;

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

public static class InstrumentalMarker
{
    public static readonly TimeSpan FrameDuration = TimeSpan.FromSeconds(1);

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

    public static int MaxWidth(LyricsInstrumentalMarker style) => Frames(style).Max(f => f.Length);
}
