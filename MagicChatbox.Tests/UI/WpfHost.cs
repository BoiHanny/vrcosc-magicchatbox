using System;
using System.Threading;
using System.Windows;

namespace MagicChatbox.Tests.UI;

/// <summary>
/// Runs a piece of WPF on a thread that can host it, against the application dictionary the real app
/// builds.
/// </summary>
/// <remarks>
/// The lock is not optional. xUnit runs test classes in parallel and WPF allows exactly one
/// <see cref="Application"/> per process, so two classes reaching for the host at the same moment
/// would race to create it and one of them would throw. It is shared here rather than kept private
/// to a test class for exactly that reason.
/// </remarks>
internal static class WpfHost
{
    private static readonly Lock Gate = new();

    /// <summary>
    /// Runs <paramref name="action"/> on an STA thread with the theme loaded, and hands back
    /// whatever it threw rather than throwing on the caller's thread.
    /// </summary>
    public static Exception? Run(Action action)
    {
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                lock (Gate)
                {
                    EnsureApplication();
                    action();
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(60));

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

        // Every host window is opened and closed again, and the default OnLastWindowClose would
        // queue a shutdown behind that last Close. Nothing pumps here, so it normally sits in the
        // queue forever and no test notices - until one pumps the dispatcher and takes the whole
        // application dictionary down under the tests that come after it.
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
    }
}
