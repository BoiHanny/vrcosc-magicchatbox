using System;
using System.Globalization;

namespace vrcosc_magicchatbox.Classes.Modules.Lyrics;

public static class LyricsTuning
{
    public const int DefaultOffsetMs = 0;

    public const int MinOffsetMs = -10000;

    public const int MaxOffsetMs = 10000;

    public const int MinGapThresholdSeconds = 2;

    public const int MaxGapThresholdSeconds = 30;

    public const int MinLineHoldSeconds = 1;

    public const int MaxLineHoldSeconds = 30;

    public static int ClampOffsetMs(int offsetMs) => Math.Clamp(offsetMs, MinOffsetMs, MaxOffsetMs);

    public static int ClampGapThresholdSeconds(int seconds)
        => Math.Clamp(seconds, MinGapThresholdSeconds, MaxGapThresholdSeconds);

    public static int ClampLineHoldSeconds(int seconds)
        => Math.Clamp(seconds, MinLineHoldSeconds, MaxLineHoldSeconds);

    public static int NudgeOffsetMs(int current, int delta) => ClampOffsetMs(ClampOffsetMs(current) + delta);

    public static int NudgeGapThresholdSeconds(int current, int delta)
        => ClampGapThresholdSeconds(ClampGapThresholdSeconds(current) + delta);

    public static int NudgeLineHoldSeconds(int current, int delta)
        => ClampLineHoldSeconds(ClampLineHoldSeconds(current) + delta);

    public static bool TryParseDelta(string? amount, out int delta)
        => int.TryParse(amount, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out delta);

    public static string FormatOffsetChip(int offsetMs)
    {
        if (offsetMs == 0)
            return "in sync";

        return Math.Abs(offsetMs) < 1000
            ? offsetMs.ToString("+0 'ms';-0 'ms'", CultureInfo.InvariantCulture)
            : (offsetMs / 1000.0).ToString("+0.0' s';-0.0' s'", CultureInfo.InvariantCulture);
    }

    public static string FormatOffsetSummary(int offsetMs) => offsetMs switch
    {
        0 => "In sync",
        > 0 => $"Lyrics run {offsetMs} ms early",
        _ => $"Lyrics run {Math.Abs(offsetMs)} ms late",
    };

    public static int EffectiveBreakSeconds(int gapThresholdSeconds, int lineHoldSeconds)
        => Math.Max(gapThresholdSeconds, lineHoldSeconds);

    public static string? DescribeTimingConflict(int gapThresholdSeconds, int lineHoldSeconds)
        => lineHoldSeconds >= gapThresholdSeconds
            ? $"Hold is not shorter than the gap, so the gap does nothing: the ♪ break marker waits {lineHoldSeconds}s either way."
            : null;
}
