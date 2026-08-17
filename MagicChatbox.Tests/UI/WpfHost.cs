using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Windows;

namespace MagicChatbox.Tests.UI;

/// <summary>
/// Runs a piece of WPF on the one thread in this process that can host it, against the application
/// dictionary the real app builds.
/// </summary>
/// <remarks>
/// <para>
/// One thread, created once, kept for the life of the process. A thread per call looks equivalent and
/// is not: WPF allows exactly one <see cref="Application"/> per process, that Application belongs to
/// the Dispatcher of the thread that created it, and a window built on any later thread gets a
/// different Dispatcher. The symptom is not an exception - it is a page that lays out and renders while
/// its two-way bindings quietly fail to write back, in whichever test happens to run second. Adding
/// WPF tests made it appear in a chat test that had passed for months and had nothing to do with them.
/// </para>
/// <para>
/// The thread takes work from a queue rather than running a dispatcher loop, and that is deliberate.
/// Pumping messages here would let anything the real App queued during construction actually run, which
/// starts services this process has no business starting: an early attempt at it brought up the OpenVR
/// session service and crashed the test host. Laying out a window needs no message loop - only the
/// explicit UpdateLayout below.
/// </para>
/// </remarks>
internal static class WpfHost
{
    private static readonly Lock Gate = new();
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(60);

    private static BlockingCollection<Action>? _work;
    private static Exception? _startupFailure;

    // A page that merges a dictionary by a rooted path - Source="/UI/Resources/SharedConverters.xaml" -
    // cannot be loaded here at all. WPF resolves that form against Application.ResourceAssembly, which is
    // the entry assembly: the test host, which contains no such resource. It cannot be redirected either,
    // because the property is latched before the first line of this class runs and its setter then
    // refuses every later value. Such a page has to name its dictionary in full,
    // "pack://application:,,,/MagicChatbox;component/UI/...", which resolves the same way in both hosts.

    /// <summary>
    /// Runs <paramref name="action"/> on the UI thread with the theme loaded, and hands back whatever
    /// it threw rather than throwing on the caller's thread.
    /// </summary>
    public static Exception? Run(Action action)
    {
        BlockingCollection<Action> work;

        try
        {
            work = EnsureThread();
        }
        catch (Exception ex)
        {
            return ex;
        }

        Exception? failure = null;
        using var done = new ManualResetEventSlim();

        work.Add(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                done.Set();
            }
        });

        // The queue serialises the work, so this budget covers time spent waiting behind another test
        // as well as time spent running.
        if (!done.Wait(Budget))
            return new TimeoutException($"the ui thread did not finish within {Budget.TotalSeconds:0} seconds");

        return failure;
    }

    /// <summary>
    /// Runs <paramref name="inspect"/> against <paramref name="content"/> inside a real, off-screen
    /// window.
    /// </summary>
    /// <remarks>
    /// Measure and Arrange on a loose element are not enough: bindings on it sit at Unattached, so
    /// the value never travels and every assertion about it passes for the wrong reason. Only a live
    /// window makes the binding engine attach.
    /// </remarks>
    public static Exception? RunInWindow(Func<FrameworkElement> content, Action<FrameworkElement> inspect)
        => Run(() =>
        {
            FrameworkElement element = content();
            var window = new Window
            {
                Content = element,
                Width = 900,
                Height = 620,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                // Off the desktop rather than merely hidden - a hidden window never lays out.
                Left = -20000,
                Top = -20000,
            };

            try
            {
                window.Show();
                element.UpdateLayout();
                inspect(element);
            }
            finally
            {
                window.Close();
            }
        });

    private static BlockingCollection<Action> EnsureThread()
    {
        lock (Gate)
        {
            if (_startupFailure != null)
                throw _startupFailure;

            if (_work != null)
                return _work;

            var work = new BlockingCollection<Action>();
            using var ready = new ManualResetEventSlim();
            Exception? startupFailure = null;

            var thread = new Thread(() =>
            {
                try
                {
                    EnsureApplication();
                }
                catch (Exception ex)
                {
                    startupFailure = ex;
                    return;
                }
                finally
                {
                    ready.Set();
                }

                // Runs until the process ends. The thread is a background thread, so it does not hold
                // the process open once the tests are done with it.
                foreach (Action item in work.GetConsumingEnumerable())
                    item();
            });

            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            if (!ready.Wait(Budget))
                throw new TimeoutException("the ui thread did not start");

            if (startupFailure != null)
            {
                _startupFailure = startupFailure;
                throw startupFailure;
            }

            _work = work;
            return work;
        }
    }

    private static void EnsureApplication()
    {
        // Touching this registers the pack scheme. Without it every pack:// Uri is parsed as having
        // a port and throws before any dictionary is looked for.
        _ = System.IO.Packaging.PackUriHelper.UriSchemePack;

        if (Application.Current != null)
            return;

        // The real App, not a stand-in with the theme bolted on. Its resource dictionary is three
        // deep - App.xaml's own styles over Theme.xaml over SharedConverters.xaml - and a page that
        // only resolves against part of it is a page this host would clear while the app crashes.
        // Constructing App does nothing on its own; the startup work all hangs off OnStartup, which
        // only Run() reaches.
        var app = new vrcosc_magicchatbox.App();
        app.InitializeComponent();
    }
}
