using System;
using System.Windows;

namespace vrcosc_magicchatbox.Core.State;

public static class WindowPlacementPolicy
{
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

        double resolvedWidth = Clamp(width, minimumSize.Width, virtualScreen.Width);
        double resolvedHeight = Clamp(height, minimumSize.Height, virtualScreen.Height);

        var candidate = new Rect(left, top, resolvedWidth, resolvedHeight);

        var overlap = Rect.Intersect(candidate, virtualScreen);
        if (overlap.IsEmpty)
            return null;

        if (overlap.Width < Math.Min(MinVisibleWidth, resolvedWidth))
            return null;

        if (overlap.Height < Math.Min(MinVisibleHeight, resolvedHeight))
            return null;

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
