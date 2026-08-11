using System;

namespace vrcosc_magicchatbox.Classes.Modules.Media;

public enum TimelineDecision
{
    Accept,

    RejectUnchangedStale,

    RejectRegressive,

    NoTimeline,

    NoTimelineSettled,
}

public readonly record struct TimelineSnapshot(TimeSpan Full, TimeSpan Current);

public readonly record struct TimelineEvaluationInput
{
    public TimeSpan IncomingFull { get; init; }

    public TimeSpan IncomingCurrent { get; init; }

    public TimeSpan StoredFull { get; init; }

    public TimeSpan StoredCurrent { get; init; }

    public TimeSpan ExtrapolatedCurrent { get; init; }

    public bool IsTimelineStale { get; init; }

    public TimeSpan StaleAge { get; init; }

    public bool IsPlaying { get; init; }

    public bool RejectUnchangedStaleTimeline { get; init; }
}

public static class MediaTimelinePolicy
{
    public static readonly TimeSpan ValueMatchTolerance = TimeSpan.FromMilliseconds(500);

    public static readonly TimeSpan BackwardDriftTolerance = TimeSpan.FromMilliseconds(1250);

    public static readonly TimeSpan BackwardJumpThreshold = TimeSpan.FromSeconds(5);

    public static readonly TimeSpan BackwardSeekTolerance = TimeSpan.FromMilliseconds(250);

    public static readonly TimeSpan NoTimelineSettleWindow = TimeSpan.FromSeconds(10);

    public static TimelineSnapshot Normalize(TimeSpan startTime, TimeSpan endTime, TimeSpan position)
    {
        TimeSpan full = endTime - startTime;
        TimeSpan current = position;

        if (startTime != TimeSpan.Zero)
            current -= startTime;

        if (full > TimeSpan.Zero)
        {
            if (current < TimeSpan.Zero)
                current = TimeSpan.Zero;
            if (current > full)
                current = full;
        }

        return new TimelineSnapshot(full, current);
    }

    public static TimelineDecision Evaluate(in TimelineEvaluationInput input)
    {
        if (input.IncomingFull <= TimeSpan.Zero)
        {
            return input.IsTimelineStale && input.StaleAge >= NoTimelineSettleWindow
                ? TimelineDecision.NoTimelineSettled
                : TimelineDecision.NoTimeline;
        }

        if (input.RejectUnchangedStaleTimeline
            && input.IsTimelineStale
            && ValuesMatch(input.StoredFull, input.IncomingFull)
            && ValuesMatch(input.StoredCurrent, input.IncomingCurrent))
        {
            return TimelineDecision.RejectUnchangedStale;
        }

        if (IsRegressiveDrift(input))
            return TimelineDecision.RejectRegressive;

        return TimelineDecision.Accept;
    }

    public static bool ValuesMatch(TimeSpan left, TimeSpan right)
        => Math.Abs((left - right).TotalMilliseconds) <= ValueMatchTolerance.TotalMilliseconds;

    private static bool IsRegressiveDrift(in TimelineEvaluationInput input)
    {
        if (!input.IsPlaying)
            return false;

        if (input.IsTimelineStale)
            return false;

        if (!ValuesMatch(input.StoredFull, input.IncomingFull))
            return false;

        if (input.IncomingCurrent < input.StoredCurrent - BackwardSeekTolerance)
            return false;

        TimeSpan extrapolatedDelta = input.ExtrapolatedCurrent - input.IncomingCurrent;
        return extrapolatedDelta > BackwardDriftTolerance &&
               extrapolatedDelta <= BackwardJumpThreshold;
    }
}
