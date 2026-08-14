using System;
using System.Collections.Generic;
using vrcosc_magicchatbox.Classes.Modules.Lyrics;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules.Lyrics;

public class LyricsTuningTests
{
    [Theory]
    [InlineData(0, 100, 100)]
    [InlineData(0, -100, -100)]
    [InlineData(250, -1000, -750)]
    [InlineData(9950, 100, 10000)]
    [InlineData(-9950, -100, -10000)]
    public void NudgeOffset_adds_the_delta_and_stops_at_the_rail(int current, int delta, int expected)
        => Assert.Equal(expected, LyricsTuning.NudgeOffsetMs(current, delta));

    [Fact]
    public void NudgeOffset_at_the_rail_does_not_move_further()
    {
        Assert.Equal(LyricsTuning.MaxOffsetMs, LyricsTuning.NudgeOffsetMs(LyricsTuning.MaxOffsetMs, 1000));
        Assert.Equal(LyricsTuning.MinOffsetMs, LyricsTuning.NudgeOffsetMs(LyricsTuning.MinOffsetMs, -1000));
    }

    /// <summary>
    /// The steppers are the first UI these settings have ever had, so the stored value can be anything
    /// a hand-edited file contained. Nudging has to pull it back into range in one click, not clamp
    /// only the sum and leave the user tapping hundreds of times.
    /// </summary>
    [Theory]
    [InlineData(999999, -1, 9999)]
    [InlineData(999999, 1, 10000)]
    [InlineData(-999999, 1, -9999)]
    public void NudgeOffset_rescues_an_out_of_range_stored_value(int current, int delta, int expected)
        => Assert.Equal(expected, LyricsTuning.NudgeOffsetMs(current, delta));

    [Theory]
    [InlineData(8, 1, 9)]
    [InlineData(8, -1, 7)]
    [InlineData(2, -1, 2)]
    [InlineData(30, 1, 30)]
    [InlineData(900, -1, 29)]
    [InlineData(0, 1, 3)]
    public void NudgeGapThreshold_stays_between_two_and_thirty(int current, int delta, int expected)
        => Assert.Equal(expected, LyricsTuning.NudgeGapThresholdSeconds(current, delta));

    [Theory]
    [InlineData(6, 1, 7)]
    [InlineData(1, -1, 1)]
    [InlineData(30, 1, 30)]
    [InlineData(0, 0, 1)]
    [InlineData(900, -1, 29)]
    public void NudgeLineHold_stays_between_one_and_thirty(int current, int delta, int expected)
        => Assert.Equal(expected, LyricsTuning.NudgeLineHoldSeconds(current, delta));

    /// <summary>The whole nudge range must survive the round trip the module makes into a TimeSpan.</summary>
    [Fact]
    public void Offset_range_is_the_ten_seconds_either_way_the_scheduler_can_absorb()
    {
        Assert.Equal(TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(LyricsTuning.MaxOffsetMs));
        Assert.Equal(TimeSpan.FromSeconds(-10), TimeSpan.FromMilliseconds(LyricsTuning.MinOffsetMs));
    }

    [Theory]
    [InlineData(0, "in sync")]
    [InlineData(300, "+300 ms")]
    [InlineData(-300, "-300 ms")]
    [InlineData(999, "+999 ms")]
    [InlineData(1000, "+1.0 s")]
    [InlineData(-1500, "-1.5 s")]
    [InlineData(10000, "+10.0 s")]
    public void OffsetChip_shows_the_unit_the_buttons_nudge_in(int offsetMs, string expected)
        => Assert.Equal(expected, LyricsTuning.FormatOffsetChip(offsetMs));

    [Theory]
    [InlineData(0, "In sync")]
    [InlineData(300, "Lyrics run 300 ms early")]
    [InlineData(-300, "Lyrics run 300 ms late")]
    public void OffsetSummary_spells_out_the_direction(int offsetMs, string expected)
        => Assert.Equal(expected, LyricsTuning.FormatOffsetSummary(offsetMs));

    [Theory]
    [InlineData("100", 100)]
    [InlineData("-1000", -1000)]
    [InlineData("+1", 1)]
    public void TryParseDelta_accepts_the_command_parameters_the_markup_passes(string amount, int expected)
    {
        Assert.True(LyricsTuning.TryParseDelta(amount, out int delta));
        Assert.Equal(expected, delta);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1.5")]
    [InlineData("half a second")]
    public void TryParseDelta_refuses_anything_it_cannot_read(string? amount)
        => Assert.False(LyricsTuning.TryParseDelta(amount, out _));

    [Theory]
    [InlineData(8, 6)]
    [InlineData(3, 2)]
    public void No_warning_while_the_hold_is_shorter_than_the_gap(int gap, int hold)
        => Assert.Null(LyricsTuning.DescribeTimingConflict(gap, hold));

    [Theory]
    [InlineData(6, 6)]
    [InlineData(4, 9)]
    public void Warning_when_the_hold_swallows_the_gap(int gap, int hold)
        => Assert.NotNull(LyricsTuning.DescribeTimingConflict(gap, hold));

    [Theory]
    [InlineData(8, 6, 8)]
    [InlineData(5, 9, 9)]
    [InlineData(6, 6, 6)]
    public void The_larger_of_the_two_numbers_is_the_one_that_decides(int gap, int hold, int expected)
        => Assert.Equal(expected, LyricsTuning.EffectiveBreakSeconds(gap, hold));

    private static LyricTrack SilenceOf(int seconds) => new()
    {
        Lines = new List<LyricLine>
        {
            new(TimeSpan.Zero, "first"),
            new(TimeSpan.FromSeconds(seconds), "second"),
        },
    };

    private static LyricCursorKind KindAt(LyricTrack track, int second, int gap, int hold)
        => LyricScheduler.Resolve(
            track,
            TimeSpan.FromSeconds(second),
            TimeSpan.Zero,
            TimeSpan.FromSeconds(gap),
            TimeSpan.FromSeconds(hold)).Kind;

    /// <summary>
    /// <see cref="LyricsTuning.EffectiveBreakSeconds" /> is a claim about
    /// <see cref="LyricScheduler.Resolve" />, so it is proved against it rather than trusted. This is
    /// where the original "hold &gt;= gap kills the marker" assumption fell over: the scheduler
    /// measures the hold from the same line start as the gap, so the hold is a second length
    /// requirement on the same silence, not an independent delay. A long enough silence still breaks.
    /// </summary>
    [Theory]
    [InlineData(8, 6)]
    [InlineData(5, 9)]
    [InlineData(6, 6)]
    [InlineData(2, 30)]
    public void A_silence_at_the_effective_break_holds_the_line_and_one_second_more_breaks_it(int gap, int hold)
    {
        int effective = LyricsTuning.EffectiveBreakSeconds(gap, hold);

        // Exactly at the requirement, the words stay up for the whole silence.
        var tooShort = SilenceOf(effective);
        for (int second = 0; second < effective; second++)
            Assert.Equal(LyricCursorKind.Line, KindAt(tooShort, second, gap, hold));

        // Longer than the requirement and the marker does appear, after the hold - which is what the
        // hold means. Two seconds of headroom so the sample at hold + 1 lands inside the silence
        // rather than on the next line's own timestamp.
        var longEnough = SilenceOf(effective + 2);
        Assert.Equal(LyricCursorKind.Line, KindAt(longEnough, hold, gap, hold));
        Assert.Equal(LyricCursorKind.InstrumentalGap, KindAt(longEnough, hold + 1, gap, hold));
    }

    /// <summary>
    /// The point of the warning: once the hold reaches the gap, moving the gap changes nothing at all.
    /// </summary>
    [Fact]
    public void While_the_warning_shows_the_gap_stepper_is_connected_to_nothing()
    {
        const int hold = 10;
        var track = SilenceOf(12);

        Assert.NotNull(LyricsTuning.DescribeTimingConflict(gapThresholdSeconds: 4, lineHoldSeconds: hold));

        foreach (int gap in new[] { 2, 4, 7, 10 })
        {
            Assert.Equal(LyricCursorKind.Line, KindAt(track, hold, gap, hold));
            Assert.Equal(LyricCursorKind.InstrumentalGap, KindAt(track, hold + 1, gap, hold));
        }

        // Push the gap past the hold and it starts deciding again: a 12 second silence is no longer
        // long enough, so the line stays up.
        Assert.Null(LyricsTuning.DescribeTimingConflict(gapThresholdSeconds: 12, lineHoldSeconds: hold));
        Assert.Equal(LyricCursorKind.Line, KindAt(track, hold + 1, 12, hold));
    }
}
