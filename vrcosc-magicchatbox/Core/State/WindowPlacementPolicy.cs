using System;
using System.Windows;

namespace vrcosc_magicchatbox.Core.State;

/// <summary>
/// Decides whether a remembered window rectangle can be restored. Kept as a pure function because the
/// failure this guards against - a window restored onto a monitor that is no longer connected, leaving
/// it invisible and unreachable - is not something you want to discover by hand.
/// </summary>
public static class WindowPlacementPolicy
{
    /// <summary>How much of the window must overlap the desktop for it to be grabbable.</summary>
    public const double MinVisibleWidth = 160;

    public const double MinVisibleHeight = 40;

    public static Rect? Resolve(
        double left,
        double top,
        double width,
        double height,
        Rect virtualScreen,
        Size minimumSize)
    {
        if (!IsFinite(left) || !IsFinite(top) || !IsFinite(width) || !IsFinite(height))
            return null;

        if (width <= 0 || height <= 0)
            return null;

        if (virtualScreen.Width <= 0 || virtualScreen.Height <= 0)
            return null;

        // Never restore smaller than the window can actually be, or larger than the desktop.
        double resolvedWidth = Clamp(width, minimumSize.Width, virtualScreen.Width);
        double resolvedHeight = Clamp(height, minimumSize.Height, virtualScreen.Height);

        var candidate = new Rect(left, top, resolvedWidth, resolvedHeight);

        var overlap = Rect.Intersect(candidate, virtualScreen);
        if (overlap.IsEmpty)
            return null;

        // A sliver poking onto the desktop is not good enough - the user has to be able to see and
        // drag the title bar.
        if (overlap.Width < Math.Min(MinVisibleWidth, resolvedWidth))
            return null;

        if (overlap.Height < Math.Min(MinVisibleHeight, resolvedHeight))
            return null;

        // The title bar specifically must be reachable, so the top edge cannot sit above the desktop.
        if (candidate.Top < virtualScreen.Top)
            return null;

        return candidate;
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private static double Clamp(double value, double min, double max)
    {
        if (max < min) return min;
        return value < min ? min : value > max ? max : value;
    }
}
