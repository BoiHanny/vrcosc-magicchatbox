using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;
using vrcosc_magicchatbox.UI.Controls;
using Xunit;

namespace MagicChatbox.Tests.UI;

/// <summary>
/// The lazy page host, which is what stops four pages being built when only one is on screen.
/// </summary>
/// <remarks>
/// A host that quietly realizes its template anyway costs exactly as much as the eager markup it
/// replaced, and the only way to tell the two apart is to put one in a live window and look at
/// whether the child exists. Content is the mechanism: a ContentPresenter builds ContentTemplate the
/// moment Content stops being null and drops the whole subtree when it goes back.
/// </remarks>
public class LazyPageHostTests
{
    private const string TemplateXaml =
        "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>" +
        "<TextBlock x:Name='Marker' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' Text='built' />" +
        "</DataTemplate>";

    [Fact]
    public void A_host_whose_page_is_not_selected_never_builds_its_template()
    {
        Exception? failure = WpfHost.RunInWindow(
            () => Host(pageIndex: 2, selectedIndex: 0),
            element =>
            {
                var host = (LazyPageHost)element;
                Assert.False(host.IsRealized);
                Assert.Equal(Visibility.Collapsed, host.Visibility);
                Assert.Null(FindMarker(host));
            });

        Assert.True(failure == null, "unselected host: " + failure);
    }

    [Fact]
    public void Selecting_the_page_builds_the_template_once()
    {
        Exception? failure = WpfHost.RunInWindow(
            () => Host(pageIndex: 2, selectedIndex: 0),
            element =>
            {
                var host = (LazyPageHost)element;
                host.SelectedIndex = 2;
                element.UpdateLayout();

                Assert.True(host.IsRealized);
                Assert.Equal(Visibility.Visible, host.Visibility);
                Assert.NotNull(FindMarker(host));

                object realized = host.Child!;
                host.SelectedIndex = 0;
                host.SelectedIndex = 2;
                Assert.Same(realized, host.Child);
            });

        Assert.True(failure == null, "selected host: " + failure);
    }

    [Fact]
    public void A_keep_alive_host_holds_its_page_after_navigating_away()
    {
        Exception? failure = WpfHost.RunInWindow(
            () => Host(pageIndex: 2, selectedIndex: 2, keepAlive: true),
            element =>
            {
                var host = (LazyPageHost)element;
                element.UpdateLayout();
                Assert.True(host.IsRealized);

                host.SelectedIndex = 0;
                PumpPast(host.TeardownDelay);

                Assert.True(host.IsRealized);
                Assert.Equal(Visibility.Collapsed, host.Visibility);
            });

        Assert.True(failure == null, "keep-alive host: " + failure);
    }

    [Fact]
    public void A_disposable_host_drops_its_page_after_the_teardown_delay()
    {
        Exception? failure = WpfHost.RunInWindow(
            () => Host(pageIndex: 2, selectedIndex: 2, keepAlive: false,
                       teardown: TimeSpan.FromMilliseconds(30)),
            element =>
            {
                var host = (LazyPageHost)element;
                element.UpdateLayout();
                Assert.True(host.IsRealized);

                host.SelectedIndex = 0;
                PumpPast(TimeSpan.FromMilliseconds(30));

                Assert.False(host.IsRealized);

                element.UpdateLayout();
                Assert.Null(FindMarker(host));
            });

        Assert.True(failure == null, "disposable host: " + failure);
    }

    [Fact]
    public void Coming_back_before_the_teardown_fires_keeps_the_page_alive()
    {
        Exception? failure = WpfHost.RunInWindow(
            () => Host(pageIndex: 2, selectedIndex: 2, keepAlive: false,
                       teardown: TimeSpan.FromMilliseconds(60)),
            element =>
            {
                var host = (LazyPageHost)element;
                element.UpdateLayout();
                object realized = host.Child!;

                host.SelectedIndex = 0;
                host.SelectedIndex = 2;
                PumpPast(TimeSpan.FromMilliseconds(60));

                Assert.True(host.IsRealized);
                Assert.Same(realized, host.Child);
            });

        Assert.True(failure == null, "returning host: " + failure);
    }

    [Fact]
    public void Releasing_and_returning_rebuilds_a_fresh_page()
    {
        Exception? failure = WpfHost.RunInWindow(
            () => Host(pageIndex: 2, selectedIndex: 2, keepAlive: false,
                       teardown: TimeSpan.FromMilliseconds(30)),
            element =>
            {
                var host = (LazyPageHost)element;
                element.UpdateLayout();
                object first = host.Child!;

                host.SelectedIndex = 0;
                PumpPast(TimeSpan.FromMilliseconds(30));
                Assert.False(host.IsRealized);

                host.SelectedIndex = 2;
                element.UpdateLayout();

                Assert.True(host.IsRealized);
                Assert.NotSame(first, host.Child);
            });

        Assert.True(failure == null, "rebuild after release: " + failure);
    }

    [Fact]
    public void Release_is_safe_to_call_when_nothing_is_built()
    {
        Exception? failure = WpfHost.RunInWindow(
            () => Host(pageIndex: 2, selectedIndex: 0, keepAlive: false),
            element =>
            {
                var host = (LazyPageHost)element;
                Assert.False(host.IsRealized);

                host.Release();
                host.Release();

                Assert.False(host.IsRealized);
            });

        Assert.True(failure == null, "release with no page: " + failure);
    }

    [Fact]
    public void A_released_page_is_collectable()
    {
        WeakReference? page = null;

        Exception? failure = WpfHost.RunInWindow(
            () => Host(pageIndex: 2, selectedIndex: 2, keepAlive: false,
                       teardown: TimeSpan.FromMilliseconds(30)),
            element =>
            {
                var host = (LazyPageHost)element;
                element.UpdateLayout();

                page = new WeakReference(host.Child);
                Assert.True(page.IsAlive);

                host.SelectedIndex = 0;
                PumpPast(TimeSpan.FromMilliseconds(30));
                Assert.False(host.IsRealized);

                element.UpdateLayout();
            });

        Assert.True(failure == null, "release: " + failure);
        Assert.NotNull(page);

        // Releasing is only worth anything if nothing is still holding the tree. A subscription the
        // page never took back is exactly what would keep this alive.
        for (int i = 0; i < 4 && page!.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.False(page!.IsAlive, "the released page is still rooted, so tearing it down reclaims nothing");
    }

    [Fact]
    public void Going_to_the_tray_releases_the_page_that_was_open()
    {
        Exception? failure = WpfHost.RunInWindow(
            () => Host(pageIndex: 2, selectedIndex: 2, keepAlive: false,
                       teardown: TimeSpan.FromMilliseconds(30)),
            element =>
            {
                var host = (LazyPageHost)element;
                element.UpdateLayout();
                Assert.True(host.IsRealized);

                host.IsHostActive = false;
                PumpPast(TimeSpan.FromMilliseconds(30));

                Assert.False(host.IsRealized);

                host.IsHostActive = true;
                element.UpdateLayout();

                Assert.True(host.IsRealized);
                Assert.Equal(Visibility.Visible, host.Visibility);
            });

        Assert.True(failure == null, "tray release: " + failure);
    }

    [Fact]
    public void A_page_is_not_built_while_the_window_is_away()
    {
        Exception? failure = WpfHost.RunInWindow(
            () => Host(pageIndex: 2, selectedIndex: 0, keepAlive: false, isActive: false),
            element =>
            {
                var host = (LazyPageHost)element;

                // Navigating from the tray menu must not build a page nobody can see yet.
                host.SelectedIndex = 2;
                element.UpdateLayout();

                Assert.False(host.IsRealized);

                host.IsHostActive = true;
                element.UpdateLayout();

                Assert.True(host.IsRealized);
            });

        Assert.True(failure == null, "deferred build: " + failure);
    }

    [Fact]
    public void Leaving_mid_transition_does_not_strand_the_host_faded_out()
    {
        Exception? failure = WpfHost.RunInWindow(
            () => Host(pageIndex: 2, selectedIndex: 0, keepAlive: false,
                       teardown: TimeSpan.FromMilliseconds(30)),
            element =>
            {
                var host = (LazyPageHost)element;

                // Arrive - the entrance animation starts from zero opacity.
                host.SelectedIndex = 2;
                element.UpdateLayout();

                // Leave again immediately, while that animation is still running.
                host.SelectedIndex = 0;
                PumpPast(TimeSpan.FromMilliseconds(30));

                Assert.False(host.IsRealized);

                // Come back. A held animation value would leave the page invisible.
                host.SelectedIndex = 2;
                element.UpdateLayout();

                Assert.True(host.IsRealized);
                Assert.Equal(1.0, host.Opacity);
            });

        Assert.True(failure == null, "interrupted transition: " + failure);
    }

    [Fact]
    public void The_transition_leaves_no_animation_holding_the_host()
    {
        Exception? failure = WpfHost.RunInWindow(
            () => Host(pageIndex: 2, selectedIndex: 2, keepAlive: false,
                       teardown: TimeSpan.FromMilliseconds(30)),
            element =>
            {
                var host = (LazyPageHost)element;
                element.UpdateLayout();

                host.SelectedIndex = 0;
                PumpPast(TimeSpan.FromMilliseconds(30));

                // The animations are FillBehavior.Stop, so the properties must be back at their
                // base values rather than pinned by a clock that outlived the page.
                Assert.Equal(1.0, host.Opacity);
                Assert.False(host.IsRealized);
            });

        Assert.True(failure == null, "transition cleanup: " + failure);
    }

    private static LazyPageHost Host(
        int pageIndex,
        int selectedIndex,
        bool keepAlive = true,
        TimeSpan? teardown = null,
        bool isActive = true)
    {
        var host = new LazyPageHost
        {
            PageTemplate = (DataTemplate)XamlReader.Parse(TemplateXaml),
            KeepAlive = keepAlive,
            IsHostActive = isActive,
            PageIndex = pageIndex,
        };

        if (teardown.HasValue)
            host.TeardownDelay = teardown.Value;

        host.SelectedIndex = selectedIndex;
        return host;
    }

    private static TextBlock? FindMarker(DependencyObject root)
    {
        int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is TextBlock text && text.Text == "built")
                return text;

            TextBlock? found = FindMarker(child);
            if (found != null)
                return found;
        }

        return null;
    }

    // A DispatcherTimer only fires while the frame is pumping, and these tests own a thread that
    // never starts a dispatcher loop of its own. Pushing one frame per slice is what makes the
    // teardown tick observable without waiting on a real message loop.
    private static void PumpPast(TimeSpan delay)
    {
        DateTime deadline = DateTime.UtcNow + delay + TimeSpan.FromMilliseconds(120);
        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.CurrentDispatcher.Invoke(
                DispatcherPriority.Background,
                new Action(() => { }));
            System.Threading.Thread.Sleep(10);
        }
    }
}
