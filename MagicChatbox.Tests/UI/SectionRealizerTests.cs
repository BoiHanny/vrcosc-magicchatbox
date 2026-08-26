using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using vrcosc_magicchatbox.UI.Controls;
using Xunit;

namespace MagicChatbox.Tests.UI;

/// <summary>
/// The options page owes its cheap navigation to only building the sections near the viewport. If the
/// realizer ever built everything up front the page would still look and behave correctly, and only a
/// stopwatch would notice - so the deferral itself is what these pin down.
/// </summary>
public class SectionRealizerTests
{
    private const int SectionCount = 20;

    private const double SectionHeight = 400;

    [Fact]
    public void A_section_far_below_the_viewport_is_not_built()
    {
        int built = 0;
        int total = 0;

        Exception? failure = WpfHost.RunInWindow(BuildHost, element =>
        {
            var scroll = (ScrollViewer)element;
            SectionRealizer realizer = Attach(scroll, new Dictionary<string, double>());

            realizer.Start();
            Pump();

            built = realizer.RealizedCount;
            total = realizer.TotalCount;
        });

        Assert.True(failure == null, "the realizer host did not build: " + failure);
        Assert.Equal(SectionCount, total);
        Assert.True(built > 0, "nothing was realized, so the page would show placeholders forever");
        Assert.True(built < total, $"all {total} sections were built up front; the deferral is not working");
    }

    [Fact]
    public void RealizeAll_builds_every_section()
    {
        int built = 0;
        int total = 0;

        Exception? failure = WpfHost.RunInWindow(BuildHost, element =>
        {
            SectionRealizer realizer = Attach((ScrollViewer)element, new Dictionary<string, double>());

            realizer.RealizeAll();

            built = realizer.RealizedCount;
            total = realizer.TotalCount;
        });

        Assert.True(failure == null, "the realizer host did not build: " + failure);
        Assert.Equal(total, built);
    }

    [Fact]
    public void An_unbuilt_section_reserves_its_remembered_height()
    {
        double reserved = 0;
        double unknown = 0;

        var heights = new Dictionary<string, double> { ["section0"] = 1234 };

        Exception? failure = WpfHost.RunInWindow(BuildHost, element =>
        {
            var scroll = (ScrollViewer)element;
            var panel = (StackPanel)scroll.Content;

            Attach(scroll, heights);

            reserved = ((ContentControl)panel.Children[0]).MinHeight;
            unknown = ((ContentControl)panel.Children[1]).MinHeight;
        });

        Assert.True(failure == null, "the realizer host did not build: " + failure);
        Assert.Equal(1234, reserved);
        Assert.Equal(SectionRealizer.DefaultSectionHeight, unknown);
    }

    [Fact]
    public void A_realized_section_records_its_height_for_next_time()
    {
        var heights = new Dictionary<string, double>();

        Exception? failure = WpfHost.RunInWindow(BuildHost, element =>
        {
            var scroll = (ScrollViewer)element;
            Attach(scroll, heights).RealizeAll();

            scroll.UpdateLayout();
            Pump();
        });

        Assert.True(failure == null, "the realizer host did not build: " + failure);
        Assert.True(heights.Count > 0, "no section height was recorded, so placeholders can never reserve properly");
        Assert.All(heights.Values, h => Assert.True(h > 0, "recorded a non-positive height"));
    }

    private static SectionRealizer Attach(ScrollViewer scroll, Dictionary<string, double> heights)
    {
        var realizer = new SectionRealizer(scroll, heights)
        {
            LookaheadViewports = 0.5,
            MaxPerPass = 64,
        };

        var panel = (StackPanel)scroll.Content;
        for (int i = 0; i < panel.Children.Count; i++)
        {
            string key = $"section{i}";
            realizer.Add(key, (ContentControl)panel.Children[i], () => new Border { Height = SectionHeight }, ".");
        }

        return realizer;
    }

    private static FrameworkElement BuildHost()
    {
        var panel = new StackPanel();

        for (int i = 0; i < SectionCount; i++)
            panel.Children.Add(new ContentControl { Content = new TextBlock { Text = "Loading…" } });

        return new ScrollViewer { Content = panel };
    }

    /// <summary>Drains the dispatcher queue: the realizer schedules its passes at Background priority.</summary>
    private static void Pump()
        => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.SystemIdle);
}
