using System;

namespace vrcosc_magicchatbox.Classes.Modules.Media;

/// <summary>
/// Outcome of evaluating an incoming SMTC timeline snapshot against the stored one.
/// </summary>
public enum TimelineDecision
{
    /// <summary>The snapshot is usable and should replace the stored timeline.</summary>
    Accept,

    /// <summary>
    /// The snapshot still matches the stale stored values — the player has changed track but
    /// hasn't published the new timeline yet. Keep waiting rather than showing the old position.
    /// </summary>
    RejectUnchangedStale,

    /// <summary>
    /// The position moved slightly backwards relative to our extrapolated clock. That pattern is
    /// scheduler drift, not a user seek.
    /// </summary>
    RejectRegressive,

    /// <summary>
    /// The player reports no usable duration. Happens during a Spotify track transition. The
    /// seekbar must be hidden, but the session still needs to be polled.
    /// </summary>
    NoTimeline,

    /// <summary>
    /// The player has reported no usable duration for long enough that this is clearly the nature
    /// of the source rather than a transition — a live stream, call audio, a notification sound.
    /// The seekbar stays hidden and the session stops being polled.
    /// </summary>
    NoTimelineSettled,
}

/// <summary>A duration/position pair normalised out of raw SMTC timeline properties.</summary>
public readonly record struct TimelineSnapshot(TimeSpan Full, TimeSpan Current);

/// <summary>Everything <see cref="MediaTimelinePolicy.Evaluate"/> needs, with no WinRT types.</summary>
public readonly record struct TimelineEvaluationInput
{
    /// <summary>Duration reported by the incoming snapshot.</summary>
    public TimeSpan IncomingFull { get; init; }

    /// <summary>Position reported by the incoming snapshot.</summary>
    public TimeSpan IncomingCurrent { get; init; }

    /// <summary>Duration we currently hold.</summary>
    public TimeSpan StoredFull { get; init; }

    /// <summary>Last position we actually stored (not extrapolated).</summary>
    public TimeSpan StoredCurrent { get; init; }

    /// <summary>Stored position advanced by wall-clock since it was stored.</summary>
    public TimeSpan ExtrapolatedCurrent { get; init; }

    /// <summary>True while we are waiting for the player to publish a timeline for a new track.</summary>
    public bool IsTimelineStale { get; init; }

    /// <summary>
    /// How long the session has been stale, measured from the current track's stale marking.
    /// Only drives the duration-less settle window; ignored when <see cref="IsTimelineStale"/> is false.
    /// </summary>
    public TimeSpan StaleAge { get; init; }

    /// <summary>True when playback is actively running (drift suppression only applies then).</summary>
    public bool IsPlaying { get; init; }

    /// <summary>Caller opt-in to the unchanged-stale rejection. Recovery paths pass true.</summary>
    public bool RejectUnchangedStaleTimeline { get; init; }
}

/// <summary>
/// Decides whether an incoming media timeline snapshot should be accepted.
/// <para>
/// Extracted from <c>MediaLinkModule.ApplyTimelineProperties</c> so the rules are unit-testable:
/// <c>GlobalSystemMediaTransportControlsSessionTimelineProperties</c> is a sealed WinRT type that
/// cannot be constructed in a test.
/// </para>
/// </summary>
public static class MediaTimelinePolicy
{
    /// <summary>Jitter allowance when comparing two timeline values for equality.</summary>
    public static readonly TimeSpan ValueMatchTolerance = TimeSpan.FromMilliseconds(500);

    /// <summary>Backward movement below this is treated as clock drift rather than a seek.</summary>
    public static readonly TimeSpan BackwardDriftTolerance = TimeSpan.FromMilliseconds(1250);

    /// <summary>Backward movement above this is a real jump and is always honoured.</summary>
    public static readonly TimeSpan BackwardJumpThreshold = TimeSpan.FromSeconds(5);

    /// <summary>Backward movement of the *stored* position beyond this is a deliberate user seek.</summary>
    public static readonly TimeSpan BackwardSeekTolerance = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// A session that has reported no duration for this long is treated as genuinely duration-less
    /// rather than mid-transition, and is settled so the resync loop stops polling it.
    /// </summary>
    public static readonly TimeSpan NoTimelineSettleWindow = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Converts raw SMTC start/end/position values into a duration and a start-relative position,
    /// clamped into range.
    /// </summary>
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

    /// <summary>Decides what to do with an incoming snapshot.</summary>
    public static TimelineDecision Evaluate(in TimelineEvaluationInput input)
    {
        if (input.IncomingFull <= TimeSpan.Zero)
        {
            return input.IsTimelineStale && input.StaleAge >= NoTimelineSettleWindow
                ? TimelineDecision.NoTimelineSettled
                : TimelineDecision.NoTimeline;
        }

        // Deliberately unconditional. An unchanged stale snapshot carries no new information by
        // definition — the stored values are still the *previous* track's — so there is no length
        // of wait after which accepting it becomes right. A timed escape hatch here would only
        // ever hand the old song's duration and position to the new song.
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

    /// <summary>True when two timeline values are equal within <see cref="ValueMatchTolerance"/>.</summary>
    public static bool ValuesMatch(TimeSpan left, TimeSpan right)
        => Math.Abs((left - right).TotalMilliseconds) <= ValueMatchTolerance.TotalMilliseconds;

    private static bool IsRegressiveDrift(in TimelineEvaluationInput input)
    {
        if (!input.IsPlaying)
            return false;

        // A stale timeline must always accept the next snapshot — otherwise the seekbar can get
        // stuck on the previous song when the new track happens to fall inside the drift window.
        if (input.IsTimelineStale)
            return false;

        if (!ValuesMatch(input.StoredFull, input.IncomingFull))
            return false;

        // Any meaningful backward movement in the stored (non-extrapolated) position is a
        // legitimate user seek — honour it even when it's small.
        if (input.IncomingCurrent < input.StoredCurrent - BackwardSeekTolerance)
            return false;

        // Suppress only when the incoming position is slightly behind our extrapolated position;
        // that pattern indicates clock/scheduler drift, not a real seek.
        TimeSpan extrapolatedDelta = input.ExtrapolatedCurrent - input.IncomingCurrent;
        return extrapolatedDelta > BackwardDriftTolerance &&
               extrapolatedDelta <= BackwardJumpThreshold;
    }
}
