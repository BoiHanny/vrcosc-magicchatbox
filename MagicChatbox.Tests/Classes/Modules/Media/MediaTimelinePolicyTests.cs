using System;
using vrcosc_magicchatbox.Classes.Modules.Media;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules.Media;

/// <summary>
/// Covers the rules that decide whether the MediaLink seekbar keeps updating.
/// Regression origin: a session that received a zero-duration snapshot during a Spotify track
/// transition lost its seekbar and was excluded from the only loop that polled it back.
/// </summary>
public class MediaTimelinePolicyTests
{
    private static readonly TimeSpan Track = TimeSpan.FromSeconds(210);

    private static TimelineEvaluationInput Input(
        TimeSpan? incomingFull = null,
        TimeSpan? incomingCurrent = null,
        TimeSpan? storedFull = null,
        TimeSpan? storedCurrent = null,
        TimeSpan? extrapolatedCurrent = null,
        bool isStale = false,
        TimeSpan? staleAge = null,
        bool isPlaying = true,
        bool rejectUnchangedStale = false)
    {
        TimeSpan stored = storedCurrent ?? TimeSpan.Zero;
        return new TimelineEvaluationInput
        {
            IncomingFull = incomingFull ?? Track,
            IncomingCurrent = incomingCurrent ?? TimeSpan.Zero,
            StoredFull = storedFull ?? Track,
            StoredCurrent = stored,
            ExtrapolatedCurrent = extrapolatedCurrent ?? stored,
            IsTimelineStale = isStale,
            StaleAge = staleAge ?? TimeSpan.Zero,
            IsPlaying = isPlaying,
            RejectUnchangedStaleTimeline = rejectUnchangedStale,
        };
    }

    // ---- Normalize -------------------------------------------------------

    [Fact]
    public void Normalize_SubtractsStartTimeFromPosition()
    {
        var snapshot = MediaTimelinePolicy.Normalize(
            startTime: TimeSpan.FromSeconds(10),
            endTime: TimeSpan.FromSeconds(130),
            position: TimeSpan.FromSeconds(40));

        Assert.Equal(TimeSpan.FromSeconds(120), snapshot.Full);
        Assert.Equal(TimeSpan.FromSeconds(30), snapshot.Current);
    }

    [Fact]
    public void Normalize_ClampsPositionIntoRange()
    {
        var over = MediaTimelinePolicy.Normalize(TimeSpan.Zero, Track, Track + TimeSpan.FromSeconds(5));
        Assert.Equal(Track, over.Current);

        var under = MediaTimelinePolicy.Normalize(
            startTime: TimeSpan.FromSeconds(30),
            endTime: TimeSpan.FromSeconds(240),
            position: TimeSpan.FromSeconds(10));
        Assert.Equal(TimeSpan.Zero, under.Current);
    }

    [Fact]
    public void Normalize_LeavesZeroDurationAlone()
    {
        var snapshot = MediaTimelinePolicy.Normalize(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
        Assert.Equal(TimeSpan.Zero, snapshot.Full);
    }

    // ---- NoTimeline ------------------------------------------------------

    [Fact]
    public void ZeroDuration_IsNoTimeline()
    {
        var decision = MediaTimelinePolicy.Evaluate(Input(incomingFull: TimeSpan.Zero));
        Assert.Equal(TimelineDecision.NoTimeline, decision);
    }

    [Fact]
    public void NegativeDuration_IsNoTimeline()
    {
        var decision = MediaTimelinePolicy.Evaluate(Input(incomingFull: TimeSpan.FromSeconds(-1)));
        Assert.Equal(TimelineDecision.NoTimeline, decision);
    }

    // ---- Unchanged-stale rejection --------------------------------------

    [Fact]
    public void StaleAndUnchanged_IsRejected_SoTheOldSongsPositionIsNotShown()
    {
        var decision = MediaTimelinePolicy.Evaluate(Input(
            incomingCurrent: TimeSpan.FromSeconds(90),
            storedCurrent: TimeSpan.FromSeconds(90),
            isStale: true,
            rejectUnchangedStale: true));

        Assert.Equal(TimelineDecision.RejectUnchangedStale, decision);
    }

    [Fact]
    public void StaleAndUnchanged_IsAccepted_WhenCallerDidNotOptIn()
    {
        var decision = MediaTimelinePolicy.Evaluate(Input(
            incomingCurrent: TimeSpan.FromSeconds(90),
            storedCurrent: TimeSpan.FromSeconds(90),
            isStale: true,
            rejectUnchangedStale: false));

        Assert.Equal(TimelineDecision.Accept, decision);
    }

    [Fact]
    public void StaleWithMovedPosition_IsAccepted()
    {
        var decision = MediaTimelinePolicy.Evaluate(Input(
            incomingCurrent: TimeSpan.FromSeconds(2),
            storedCurrent: TimeSpan.FromSeconds(90),
            isStale: true,
            rejectUnchangedStale: true));

        Assert.Equal(TimelineDecision.Accept, decision);
    }

    [Fact]
    public void StaleWithNewDuration_IsAccepted()
    {
        var decision = MediaTimelinePolicy.Evaluate(Input(
            incomingFull: TimeSpan.FromSeconds(185),
            incomingCurrent: TimeSpan.FromSeconds(90),
            storedFull: Track,
            storedCurrent: TimeSpan.FromSeconds(90),
            isStale: true,
            rejectUnchangedStale: true));

        Assert.Equal(TimelineDecision.Accept, decision);
    }

    [Fact]
    public void UnchangedWithin500ms_CountsAsUnchanged()
    {
        var decision = MediaTimelinePolicy.Evaluate(Input(
            incomingCurrent: TimeSpan.FromMilliseconds(90_400),
            storedCurrent: TimeSpan.FromSeconds(90),
            isStale: true,
            rejectUnchangedStale: true));

        Assert.Equal(TimelineDecision.RejectUnchangedStale, decision);
    }

    // ---- Unchanged-stale rejection does not time out ---------------------

    [Fact]
    public void StaleAndUnchanged_IsStillRejectedNoMatterHowLongTheWait()
    {
        // An unchanged stale snapshot is the *previous* track's timeline by definition, so no
        // amount of waiting makes accepting it correct. Showing 1:30/3:00 from the last song
        // against the new song's title is worse than showing no seekbar at all.
        foreach (var age in new[]
                 {
                     TimeSpan.Zero,
                     TimeSpan.FromSeconds(6),
                     TimeSpan.FromSeconds(30),
                     TimeSpan.FromMinutes(10),
                 })
        {
            var input = Input(
                incomingCurrent: TimeSpan.FromSeconds(90),
                storedCurrent: TimeSpan.FromSeconds(90),
                isStale: true,
                staleAge: age,
                rejectUnchangedStale: true);

            Assert.Equal(TimelineDecision.RejectUnchangedStale, MediaTimelinePolicy.Evaluate(input));
        }
    }

    // ---- Duration-less sources settle instead of being polled forever ----

    [Fact]
    public void NoDurationBriefly_StaysNoTimeline_SoTheResyncKeepsLooking()
    {
        var decision = MediaTimelinePolicy.Evaluate(Input(
            incomingFull: TimeSpan.Zero,
            isStale: true,
            staleAge: MediaTimelinePolicy.NoTimelineSettleWindow - TimeSpan.FromMilliseconds(1)));

        Assert.Equal(TimelineDecision.NoTimeline, decision);
    }

    [Fact]
    public void NoDurationForLongEnough_Settles_SoLiveStreamsStopBeingPolled()
    {
        var decision = MediaTimelinePolicy.Evaluate(Input(
            incomingFull: TimeSpan.Zero,
            isStale: true,
            staleAge: MediaTimelinePolicy.NoTimelineSettleWindow));

        Assert.Equal(TimelineDecision.NoTimelineSettled, decision);
    }

    [Fact]
    public void NoDurationWhenNotStale_IsNeverSettled()
    {
        // StaleAge is meaningless unless the session is actually stale.
        var decision = MediaTimelinePolicy.Evaluate(Input(
            incomingFull: TimeSpan.Zero,
            isStale: false,
            staleAge: TimeSpan.FromHours(1)));

        Assert.Equal(TimelineDecision.NoTimeline, decision);
    }

    // ---- Regressive drift ------------------------------------------------

    [Fact]
    public void SmallBackwardsDriftWhilePlaying_IsSuppressed()
    {
        var decision = MediaTimelinePolicy.Evaluate(Input(
            incomingCurrent: TimeSpan.FromSeconds(60),
            storedCurrent: TimeSpan.FromSeconds(60),
            extrapolatedCurrent: TimeSpan.FromSeconds(62),
            isStale: false));

        Assert.Equal(TimelineDecision.RejectRegressive, decision);
    }

    [Fact]
    public void DeliberateBackwardSeek_IsHonoured()
    {
        var decision = MediaTimelinePolicy.Evaluate(Input(
            incomingCurrent: TimeSpan.FromSeconds(30),
            storedCurrent: TimeSpan.FromSeconds(60),
            extrapolatedCurrent: TimeSpan.FromSeconds(62),
            isStale: false));

        Assert.Equal(TimelineDecision.Accept, decision);
    }

    [Fact]
    public void LargeBackwardJump_IsHonoured()
    {
        var decision = MediaTimelinePolicy.Evaluate(Input(
            incomingCurrent: TimeSpan.FromSeconds(60),
            storedCurrent: TimeSpan.FromSeconds(60),
            extrapolatedCurrent: TimeSpan.FromSeconds(70),
            isStale: false));

        Assert.Equal(TimelineDecision.Accept, decision);
    }

    [Fact]
    public void DriftSuppression_DoesNotApplyWhenPaused()
    {
        var decision = MediaTimelinePolicy.Evaluate(Input(
            incomingCurrent: TimeSpan.FromSeconds(60),
            storedCurrent: TimeSpan.FromSeconds(60),
            extrapolatedCurrent: TimeSpan.FromSeconds(62),
            isStale: false,
            isPlaying: false));

        Assert.Equal(TimelineDecision.Accept, decision);
    }

    [Fact]
    public void DriftSuppression_DoesNotApplyWhileStale()
    {
        var decision = MediaTimelinePolicy.Evaluate(Input(
            incomingCurrent: TimeSpan.FromSeconds(60),
            storedCurrent: TimeSpan.FromSeconds(60),
            extrapolatedCurrent: TimeSpan.FromSeconds(62),
            isStale: true,
            rejectUnchangedStale: false));

        Assert.Equal(TimelineDecision.Accept, decision);
    }

    // ---- The reported scenario, end to end ------------------------------

    [Fact]
    public void TrackTransition_RecoversOnceTheNewTimelineArrives()
    {
        // Spotify publishes an empty timeline mid-transition.
        var duringTransition = MediaTimelinePolicy.Evaluate(Input(
            incomingFull: TimeSpan.Zero,
            isStale: true,
            rejectUnchangedStale: true));
        Assert.Equal(TimelineDecision.NoTimeline, duringTransition);

        // Then the new track's timeline lands and must be taken.
        var afterTransition = MediaTimelinePolicy.Evaluate(Input(
            incomingFull: TimeSpan.FromSeconds(185),
            incomingCurrent: TimeSpan.FromSeconds(1),
            storedFull: Track,
            storedCurrent: TimeSpan.FromSeconds(207),
            isStale: true,
            staleAge: TimeSpan.FromSeconds(2),
            rejectUnchangedStale: true));
        Assert.Equal(TimelineDecision.Accept, afterTransition);
    }

    [Fact]
    public void RepeatOneSameTrack_RecoversWithoutAnyGraceWindow()
    {
        // Repeat-one replays a track with an identical duration. The metadata does not change, so
        // nothing marks the timeline stale and the unchanged-stale rule never applies; the restart
        // reads as a large backward jump, which is always honoured.
        var restart = MediaTimelinePolicy.Evaluate(Input(
            incomingCurrent: TimeSpan.Zero,
            storedCurrent: TimeSpan.FromSeconds(207),
            extrapolatedCurrent: TimeSpan.FromSeconds(207),
            isStale: false,
            isPlaying: true,
            rejectUnchangedStale: true));

        Assert.Equal(TimelineDecision.Accept, restart);
    }

    [Fact]
    public void StaleAtTrackStart_RecoversAsSoonAsThePositionAdvances()
    {
        // The one case the removed grace window addressed: stale while the stored position happens
        // to equal the incoming one. It resolves itself within a second of playback rather than
        // needing a timer that would otherwise re-admit the previous track's timeline.
        var deadlocked = Input(
            incomingCurrent: TimeSpan.Zero,
            storedCurrent: TimeSpan.Zero,
            isStale: true,
            staleAge: TimeSpan.FromSeconds(2),
            rejectUnchangedStale: true);

        Assert.Equal(TimelineDecision.RejectUnchangedStale, MediaTimelinePolicy.Evaluate(deadlocked));

        // One resync tick later the player has moved past the 500 ms match tolerance.
        var advanced = deadlocked with { IncomingCurrent = TimeSpan.FromSeconds(2) };
        Assert.Equal(TimelineDecision.Accept, MediaTimelinePolicy.Evaluate(advanced));
    }
}
