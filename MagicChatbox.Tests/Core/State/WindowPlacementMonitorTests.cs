using System.Windows;
using vrcosc_magicchatbox.Core.State;
using Xunit;

namespace MagicChatbox.Tests.Core.State;

public class WindowPlacementMonitorTests
{
    private static readonly Rect LeftScreen = new(-1920, 0, 1920, 1040);
    private static readonly Rect RightScreen = new(0, 0, 2560, 1400);

    [Fact]
    public void A_window_is_owned_by_the_monitor_its_centre_sits_on()
    {
        var onRight = new Rect(200, 100, 1100, 800);

        Assert.True(WindowPlacementPolicy.BelongsTo(onRight, RightScreen));
        Assert.False(WindowPlacementPolicy.BelongsTo(onRight, LeftScreen));
    }

    [Fact]
    public void A_window_straddling_two_monitors_belongs_to_the_one_holding_most_of_it()
    {
        // Mostly on the left screen, spilling a little onto the right.
        var straddling = new Rect(-300, 100, 500, 400);

        Assert.True(WindowPlacementPolicy.BelongsTo(straddling, LeftScreen));
        Assert.False(WindowPlacementPolicy.BelongsTo(straddling, RightScreen));
    }

    [Fact]
    public void Moving_to_another_monitor_keeps_the_window_inside_it()
    {
        var saved = new Rect(1200, 200, 1100, 800);

        Rect moved = WindowPlacementPolicy.MoveToWorkArea(saved, RightScreen, LeftScreen);

        Assert.True(moved.Left >= LeftScreen.Left);
        Assert.True(moved.Top >= LeftScreen.Top);
        Assert.True(moved.Right <= LeftScreen.Right);
        Assert.True(moved.Bottom <= LeftScreen.Bottom);
    }

    [Fact]
    public void Moving_keeps_where_the_window_sat_rather_than_its_coordinates()
    {
        // Hard against the right edge of the wide screen.
        var saved = new Rect(RightScreen.Right - 1000, 0, 1000, 600);

        Rect moved = WindowPlacementPolicy.MoveToWorkArea(saved, RightScreen, LeftScreen);

        Assert.Equal(LeftScreen.Right - moved.Width, moved.Left, 3);
    }

    [Fact]
    public void A_window_larger_than_the_target_monitor_is_shrunk_to_fit()
    {
        var saved = new Rect(0, 0, 2400, 1300);

        Rect moved = WindowPlacementPolicy.MoveToWorkArea(saved, RightScreen, LeftScreen);

        Assert.Equal(LeftScreen.Width, moved.Width);
        Assert.Equal(LeftScreen.Height, moved.Height);
        Assert.Equal(LeftScreen.Left, moved.Left);
    }

    [Fact]
    public void A_degenerate_target_leaves_the_window_alone()
    {
        var saved = new Rect(100, 100, 800, 600);

        Assert.Equal(saved, WindowPlacementPolicy.MoveToWorkArea(saved, RightScreen, new Rect(0, 0, 0, 0)));
    }
}
