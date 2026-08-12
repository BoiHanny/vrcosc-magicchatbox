using System.Windows;
using vrcosc_magicchatbox.Core.State;
using Xunit;

namespace MagicChatbox.Tests.Core.State;

public class WindowPlacementPolicyTests
{
    // A single 1920x1080 monitor at the origin.
    private static readonly Rect SingleScreen = new(0, 0, 1920, 1080);

    // A second monitor to the left, as Windows reports it: negative coordinates.
    private static readonly Rect DualScreen = new(-1920, 0, 3840, 1080);

    private static readonly Size MinSize = new(1080, 500);

    [Fact]
    public void ARememberedWindowOnTheMainScreenIsRestored()
    {
        var result = WindowPlacementPolicy.Resolve(120, 80, 1150, 775, SingleScreen, MinSize);

        Assert.NotNull(result);
        Assert.Equal(120, result!.Value.Left);
        Assert.Equal(80, result.Value.Top);
        Assert.Equal(1150, result.Value.Width);
        Assert.Equal(775, result.Value.Height);
    }

    [Fact]
    public void AWindowOnASecondMonitorKeepsItsNegativeCoordinates()
    {
        var result = WindowPlacementPolicy.Resolve(-1500, 100, 1150, 775, DualScreen, MinSize);

        Assert.NotNull(result);
        Assert.Equal(-1500, result!.Value.Left);
    }

    // The reason this policy exists: that second monitor gets unplugged.
    [Fact]
    public void AWindowOnAMonitorThatIsGoneIsNotRestored()
    {
        var result = WindowPlacementPolicy.Resolve(-1500, 100, 1150, 775, SingleScreen, MinSize);

        Assert.Null(result);
    }

    [Fact]
    public void AWindowLeftAboveTheDesktopIsNotRestored()
    {
        // Title bar off the top edge means it cannot be dragged back.
        var result = WindowPlacementPolicy.Resolve(100, -300, 1150, 775, SingleScreen, MinSize);

        Assert.Null(result);
    }

    [Fact]
    public void AWindowBarelyPeekingOntoTheDesktopIsNotRestored()
    {
        var result = WindowPlacementPolicy.Resolve(1900, 500, 1150, 775, SingleScreen, MinSize);

        Assert.Null(result);
    }

    [Fact]
    public void NothingSavedYetMeansNoRestore()
    {
        var result = WindowPlacementPolicy.Resolve(
            double.NaN, double.NaN, double.NaN, double.NaN, SingleScreen, MinSize);

        Assert.Null(result);
    }

    [Fact]
    public void ASizeSmallerThanTheWindowAllowsIsGrownToTheMinimum()
    {
        var result = WindowPlacementPolicy.Resolve(100, 100, 400, 200, SingleScreen, MinSize);

        Assert.NotNull(result);
        Assert.Equal(MinSize.Width, result!.Value.Width);
        Assert.Equal(MinSize.Height, result.Value.Height);
    }

    [Fact]
    public void ASizeLargerThanTheDesktopIsClampedToIt()
    {
        var result = WindowPlacementPolicy.Resolve(0, 0, 5000, 4000, SingleScreen, MinSize);

        Assert.NotNull(result);
        Assert.Equal(SingleScreen.Width, result!.Value.Width);
        Assert.Equal(SingleScreen.Height, result.Value.Height);
    }

    [Fact]
    public void GarbageValuesAreRejectedRatherThanThrown()
    {
        Assert.Null(WindowPlacementPolicy.Resolve(0, 0, -5, -5, SingleScreen, MinSize));
        Assert.Null(WindowPlacementPolicy.Resolve(double.PositiveInfinity, 0, 800, 600, SingleScreen, MinSize));
        Assert.Null(WindowPlacementPolicy.Resolve(0, 0, 800, 600, Rect.Empty, MinSize));
    }
}
