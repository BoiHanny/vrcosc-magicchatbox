using System;
using System.Globalization;

namespace vrcosc_magicchatbox.Classes.Modules.Lyrics;

/// <summary>
/// The arithmetic and wording behind the lyric timing controls, kept away from the view models so
/// the clamping and the readouts can be tested without a settings provider or a dispatcher.
/// </summary>
/// <remarks>
/// Every nudge clamps the <em>current</em> value as well as the result. A hand-edited settings file
/// can hold a gap of 900 seconds, and until now nothing on screen could bring it back into range,
/// so a stepper that only clamped the sum would need 870 clicks to become useful.
/// </remarks>
public static class LyricsTuning
{
    /// <summary>Roughly a wrong-edition LRC; far enough to fix one, short enough not to skip a verse.</summary>
    public const int MinOffsetMs = -10000;

    /// <inheritdoc cref="MinOffsetMs" />
    public const int MaxOffsetMs = 10000;

    /// <summary>Under two seconds every line looks like an instrumental break.</summary>
    public const int MinGapThresholdSeconds = 2;

    /// <summary>Thirty seconds covers a long intro without letting the marker never fire.</summary>
    public const int MaxGapThresholdSeconds = 30;

    /// <summary>Matches the <c>Math.Max(1, ...)</c> floor the module already applies.</summary>
    public const int MinLineHoldSeconds = 1;

    /// <inheritdoc cref="MaxGapThresholdSeconds" />
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

    /// <summary>
    /// Parses a XAML <c>CommandParameter</c> into a delta. Returns false for anything unparseable so
    /// a typo in the markup is a dead button rather than a silent reset to zero.
    /// </summary>
    public static bool TryParseDelta(string? amount, out int delta)
        => int.TryParse(amount, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out delta);

    /// <summary>
    /// The compact readout for the ribbon pill. Sub-second offsets stay in milliseconds because that
    /// is the unit the buttons nudge in - showing "+0.1 s" after a 100 ms tap reads like a rounding
    /// error rather than the exact thing that was asked for.
    /// </summary>
    public static string FormatOffsetChip(int offsetMs)
    {
        if (offsetMs == 0)
            return "in sync";

        return Math.Abs(offsetMs) < 1000
            ? offsetMs.ToString("+0 'ms';-0 'ms'", CultureInfo.InvariantCulture)
            : (offsetMs / 1000.0).ToString("+0.0' s';-0.0' s'", CultureInfo.InvariantCulture);
    }

    /// <summary>The long form for the flyout and the Options page, which have room for a sentence.</summary>
    public static string FormatOffsetSummary(int offsetMs) => offsetMs switch
    {
        0 => "In sync",
        > 0 => $"Lyrics run {offsetMs} ms early",
        _ => $"Lyrics run {Math.Abs(offsetMs)} ms late",
    };

    /// <summary>
    /// The silence a line must be followed by - strictly longer than this - before the ♪ break marker
    /// can ever appear.
    /// </summary>
    /// <remarks>
    /// <see cref="LyricScheduler.Resolve" /> wants the silence longer than the threshold <em>and</em>
    /// the line held past the hold, but both are measured from the same line start, so the hold is a
    /// second length requirement on the very same silence rather than an independent delay. Whichever
    /// number is larger is the one that decides; the smaller one is dead weight.
    /// </remarks>
    public static int EffectiveBreakSeconds(int gapThresholdSeconds, int lineHoldSeconds)
        => Math.Max(gapThresholdSeconds, lineHoldSeconds);

    /// <summary>
    /// Warns when the hold has swallowed the gap, which leaves the Gap stepper connected to nothing.
    /// Said out loud rather than fixed silently - moving a number the user did not touch is the worse
    /// surprise.
    /// </summary>
    public static string? DescribeTimingConflict(int gapThresholdSeconds, int lineHoldSeconds)
        => lineHoldSeconds >= gapThresholdSeconds
            ? $"Hold is not shorter than the gap, so the gap does nothing: the ♪ break marker waits {lineHoldSeconds}s either way."
            : null;
}
