using System;

namespace vrcosc_magicchatbox.Classes.Modules.Lyrics;

public enum LyricCursorKind
{
    None,
    BeforeFirstLine,
    Line,
    InstrumentalGap,
}

public readonly record struct LyricCursor(
    LyricCursorKind Kind,
    int Index,
    string Text,
    TimeSpan LineStart,
    TimeSpan LineEnd)
{
    public static readonly LyricCursor None = new(LyricCursorKind.None, -1, string.Empty, TimeSpan.Zero, TimeSpan.Zero);

    public TimeSpan LineDuration => LineEnd > LineStart ? LineEnd - LineStart : TimeSpan.Zero;
}

public static class LyricScheduler
{
    public static readonly TimeSpan DefaultGapThreshold = TimeSpan.FromSeconds(8);
    public static readonly TimeSpan DefaultLineHold = TimeSpan.FromSeconds(6);

    public static LyricCursor Resolve(
        LyricTrack track,
        TimeSpan position,
        TimeSpan userOffset,
        TimeSpan gapThreshold,
        TimeSpan lineHold)
    {
        if (track == null || track.Lines.Count == 0)
            return LyricCursor.None;

        TimeSpan at = position + userOffset + track.EmbeddedOffset;

        int index = FindLineIndex(track, at);
        if (index < 0)
            return new LyricCursor(LyricCursorKind.BeforeFirstLine, -1, string.Empty, TimeSpan.Zero, track.Lines[0].Start);

        var line = track.Lines[index];
        TimeSpan next = index + 1 < track.Lines.Count ? track.Lines[index + 1].Start : TimeSpan.MaxValue;

        TimeSpan held = at - line.Start;
        bool longGap = next != TimeSpan.MaxValue && next - line.Start > gapThreshold;

        if (longGap && held > lineHold)
            return new LyricCursor(LyricCursorKind.InstrumentalGap, index, string.Empty, line.Start, next);

        return new LyricCursor(LyricCursorKind.Line, index, line.Text, line.Start, next);
    }

    public static int FindLineIndex(LyricTrack track, TimeSpan at)
    {
        var lines = track.Lines;
        int low = 0;
        int high = lines.Count - 1;
        int found = -1;

        while (low <= high)
        {
            int mid = low + ((high - low) / 2);
            if (lines[mid].Start <= at)
            {
                found = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return found;
    }
}
