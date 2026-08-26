using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.UI.Controls;

/// <summary>
/// Remembers where each page was scrolled to and puts it back when the page is rebuilt, including
/// after the app is closed and reopened. Offsets are recorded as the user scrolls rather than on
/// Unloaded, because a page host tears its child out synchronously while Unloaded arrives later.
/// </summary>
public static class ScrollMemory
{
    private static readonly DependencyProperty KeyProperty = DependencyProperty.RegisterAttached(
        "ScrollMemoryKey", typeof(string), typeof(ScrollMemory), new PropertyMetadata(null));

    public static void Attach(ScrollViewer scroll, string key)
    {
        if (scroll == null || string.IsNullOrEmpty(key))
            return;

        scroll.SetValue(KeyProperty, key);
        scroll.ScrollChanged -= OnScrollChanged;
        scroll.ScrollChanged += OnScrollChanged;
    }

    public static void Detach(ScrollViewer scroll)
    {
        if (scroll == null)
            return;

        Remember(scroll, scroll.GetValue(KeyProperty) as string);
        scroll.ScrollChanged -= OnScrollChanged;
        MarkDirty();
    }

    /// <summary>
    /// Settings auto-save is driven by PropertyChanged, and mutating a dictionary in place raises nothing.
    /// This arms the save so offsets survive more than a clean shutdown.
    /// </summary>
    public static void MarkDirty()
        => Settings()?.MarkScrollStateChanged();

    /// <summary>
    /// Puts the scroll viewer back where it was. <paramref name="beforeSettle"/> runs between the two
    /// passes so lazily built content can be created before the final offset is applied.
    /// </summary>
    public static void Restore(ScrollViewer scroll, string key, Action? beforeSettle = null)
    {
        if (scroll == null || string.IsNullOrEmpty(key))
            return;

        Attach(scroll, key);

        if (!TryRead(key, out double offset) || offset <= 0)
            return;

        // Two passes: the first gives the content its height, the second lands on the real offset once
        // the extent is known. A single pass silently clamps to whatever was measured first.
        scroll.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            scroll.ScrollToVerticalOffset(offset);
            beforeSettle?.Invoke();

            scroll.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () =>
            {
                scroll.ScrollToVerticalOffset(offset);
                beforeSettle?.Invoke();
            });
        });
    }

    public static void Remember(ScrollViewer? scroll, string? key)
    {
        if (scroll == null || string.IsNullOrEmpty(key))
            return;

        // A zero offset on a viewer that has not measured yet is teardown noise, not a real position.
        if (scroll.VerticalOffset <= 0 && scroll.ExtentHeight <= scroll.ViewportHeight)
            return;

        Store(key, scroll.VerticalOffset);
    }

    private static void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is ScrollViewer scroll && Math.Abs(e.VerticalChange) > 0)
            Remember(scroll, scroll.GetValue(KeyProperty) as string);
    }

    private static bool TryRead(string key, out double offset)
    {
        offset = 0;
        var offsets = Offsets();
        return offsets != null && offsets.TryGetValue(key, out offset);
    }

    private static void Store(string key, double offset)
    {
        var offsets = Offsets();
        if (offsets != null)
            offsets[key] = offset;
    }

    private static Dictionary<string, double>? Offsets() => Settings()?.PageScrollOffsets;

    private static AppSettings? Settings()
        => App.Services?.GetService<ISettingsProvider<AppSettings>>()?.Value;
}
