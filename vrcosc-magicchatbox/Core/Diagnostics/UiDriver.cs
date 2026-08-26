using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace vrcosc_magicchatbox.Core.Diagnostics;

/// <summary>
/// Finds and operates real controls through their automation peers, so a scripted run goes down the
/// same path a click does - command, trigger, animation and all - rather than poking the view model
/// and skipping everything the UI would actually have done.
/// </summary>
public static class UiDriver
{
    /// <summary>Every element of type T under <paramref name="root"/>, in visual-tree order.</summary>
    public static List<T> FindAll<T>(DependencyObject? root) where T : DependencyObject
    {
        var found = new List<T>();
        Collect(root, found);
        return found;
    }

    private static void Collect<T>(DependencyObject? node, List<T> found) where T : DependencyObject
    {
        if (node == null)
            return;

        if (node is T match)
            found.Add(match);

        int count = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < count; i++)
            Collect(VisualTreeHelper.GetChild(node, i), found);
    }

    public static T? FindByName<T>(DependencyObject? root, string name) where T : FrameworkElement
        => FindAll<T>(root).FirstOrDefault(e => e.Name == name);

    /// <summary>Clicks a button the way the automation layer does. Returns false if it could not be clicked.</summary>
    public static bool Invoke(UIElement? element)
    {
        if (element is not UIElement { IsEnabled: true } target)
            return false;

        AutomationPeer? peer = UIElementAutomationPeer.CreatePeerForElement(target);
        if (peer?.GetPattern(PatternInterface.Invoke) is IInvokeProvider invoke)
        {
            invoke.Invoke();
            return true;
        }

        if (peer?.GetPattern(PatternInterface.Toggle) is IToggleProvider toggle)
        {
            toggle.Toggle();
            return true;
        }

        return false;
    }

    /// <summary>Sets a toggle to a specific state, doing nothing when it is already there.</summary>
    public static bool SetToggle(ToggleButton? button, bool on)
    {
        if (button == null || !button.IsEnabled || button.IsChecked == on)
            return false;

        AutomationPeer? peer = UIElementAutomationPeer.CreatePeerForElement(button);
        if (peer?.GetPattern(PatternInterface.Toggle) is IToggleProvider toggle)
        {
            toggle.Toggle();
            return true;
        }

        button.IsChecked = on;
        return true;
    }

    /// <summary>
    /// Returns once everything the last action queued has run. Two priorities, because layout is queued
    /// at Loaded and the work layout itself queues lands behind it.
    /// </summary>
    public static async Task SettleAsync(Dispatcher dispatcher, int extraMs = 0)
    {
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);

        if (extraMs > 0)
            await Task.Delay(extraMs);
    }

    /// <summary>Scrolls a viewer top to bottom in viewport steps, settling at each one.</summary>
    public static async Task ScrollThroughAsync(ScrollViewer scroll, int steps = 10, int dwellMs = 90)
    {
        if (scroll.ScrollableHeight <= 0)
            return;

        for (int i = 0; i <= steps; i++)
        {
            scroll.ScrollToVerticalOffset(scroll.ScrollableHeight * i / steps);
            await SettleAsync(scroll.Dispatcher, dwellMs);
        }

        scroll.ScrollToVerticalOffset(0);
        await SettleAsync(scroll.Dispatcher);
    }
}
