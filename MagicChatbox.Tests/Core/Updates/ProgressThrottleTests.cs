using System;
using vrcosc_magicchatbox.Core.Updates;
using Xunit;

namespace MagicChatbox.Tests.Core.Updates;

public class ProgressThrottleTests
{
    private static ProgressThrottle Throttle() =>
        new(TimeSpan.FromMilliseconds(100), minimumPercentStep: 1d);

    [Fact]
    public void The_first_report_always_gets_through()
    {
        Assert.True(Throttle().ShouldReport(TimeSpan.Zero, 0));
    }

    [Fact]
    public void A_burst_of_tiny_updates_inside_the_window_is_collapsed()
    {
        var throttle = Throttle();
        throttle.ShouldReport(TimeSpan.Zero, 0);

        Assert.False(throttle.ShouldReport(TimeSpan.FromMilliseconds(10), 0.1));
        Assert.False(throttle.ShouldReport(TimeSpan.FromMilliseconds(20), 0.2));
        Assert.False(throttle.ShouldReport(TimeSpan.FromMilliseconds(30), 0.3));
    }

    [Fact]
    public void Enough_elapsed_time_lets_a_report_through_even_if_the_bar_barely_moved()
    {
        var throttle = Throttle();
        throttle.ShouldReport(TimeSpan.Zero, 0);

        Assert.True(throttle.ShouldReport(TimeSpan.FromMilliseconds(150), 0.05));
    }

    [Fact]
    public void A_big_jump_lets_a_report_through_even_inside_the_time_window()
    {
        var throttle = Throttle();
        throttle.ShouldReport(TimeSpan.Zero, 0);

        Assert.True(throttle.ShouldReport(TimeSpan.FromMilliseconds(5), 40));
    }

    [Fact]
    public void Accepting_a_report_restarts_both_windows()
    {
        var throttle = Throttle();
        throttle.ShouldReport(TimeSpan.Zero, 0);

        Assert.True(throttle.ShouldReport(TimeSpan.FromMilliseconds(150), 5));
        Assert.False(throttle.ShouldReport(TimeSpan.FromMilliseconds(160), 5.2));
    }

    [Fact]
    public void A_rejected_report_does_not_move_the_baseline()
    {
        var throttle = Throttle();
        throttle.ShouldReport(TimeSpan.Zero, 0);

        Assert.False(throttle.ShouldReport(TimeSpan.FromMilliseconds(10), 0.4));
        Assert.False(throttle.ShouldReport(TimeSpan.FromMilliseconds(20), 0.8));

        // Still measured against the accepted report at 0%, so 1% is reached and allowed.
        Assert.True(throttle.ShouldReport(TimeSpan.FromMilliseconds(30), 1.0));
    }

    [Fact]
    public void Forcing_a_report_bypasses_both_checks_and_rebases()
    {
        var throttle = Throttle();
        throttle.ShouldReport(TimeSpan.Zero, 0);

        Assert.True(throttle.ShouldReport(TimeSpan.FromMilliseconds(1), 0, force: true));
        Assert.False(throttle.ShouldReport(TimeSpan.FromMilliseconds(2), 0));
    }

    [Fact]
    public void A_bar_running_backwards_still_counts_as_movement()
    {
        var throttle = Throttle();
        throttle.ShouldReport(TimeSpan.Zero, 50);

        Assert.True(throttle.ShouldReport(TimeSpan.FromMilliseconds(5), 20));
    }
}
