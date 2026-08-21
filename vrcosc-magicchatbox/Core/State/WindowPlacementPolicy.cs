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

    /// <summary>
    /// Whether a saved window belongs to the monitor it is about to be restored on. Uses the window's
    /// centre rather than its corner so a window straddling two monitors resolves to the one it is
    /// mostly on, which is the one a person would say it was open on.
    /// </summary>
    public static bool BelongsTo(Rect bounds, Rect workArea)
    {
        if (workArea.Width <= 0 || workArea.Height <= 0)
            return false;

        var centre = new Point(
            bounds.Left + (bounds.Width / 2),
            bounds.Top + (bounds.Height / 2));

        return workArea.Contains(centre);
    }

    /// <summary>
    /// Carries a window onto another monitor, keeping where it sat within its old one rather than
    /// its absolute coordinates - a window three-quarters across a wide screen lands three-quarters
    /// across a narrow one instead of hanging off the edge.
    /// </summary>
    public static Rect MoveToWorkArea(Rect bounds, Rect fromWorkArea, Rect toWorkArea)
    {
        if (toWorkArea.Width <= 0 || toWorkArea.Height <= 0)
            return bounds;

        double width = Math.Min(bounds.Width, toWorkArea.Width);
        double height = Math.Min(bounds.Height, toWorkArea.Height);

        double left = toWorkArea.Left + (RelativePosition(bounds.Left, bounds.Width, fromWorkArea.Left, fromWorkArea.Width)
            * Math.Max(0, toWorkArea.Width - width));
        double top = toWorkArea.Top + (RelativePosition(bounds.Top, bounds.Height, fromWorkArea.Top, fromWorkArea.Height)
            * Math.Max(0, toWorkArea.Height - height));

        left = Clamp(left, toWorkArea.Left, Math.Max(toWorkArea.Left, toWorkArea.Right - width));
        top = Clamp(top, toWorkArea.Top, Math.Max(toWorkArea.Top, toWorkArea.Bottom - height));

        return new Rect(left, top, width, height);
    }

    private static double RelativePosition(double start, double length, double areaStart, double areaLength)
    {
        double travel = areaLength - length;
        if (travel <= 0)
            return 0;

        return Clamp((start - areaStart) / travel, 0, 1);
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private static double Clamp(double value, double min, double max)
    {
        if (max < min) return min;
        return value < min ? min : value > max ? max : value;
    }
}
