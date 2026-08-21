using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.UI.Dialogs;
using vrcosc_magicchatbox.ViewModels.State;
using Xunit;

namespace MagicChatbox.Tests.UI;

/// <summary>
/// The crash dialog is the one window that must work when everything else has already failed, and
/// its bindings fail silently like any other. These render it for real and read the result back.
/// </summary>
public class ApplicationErrorDialogTests
{
    private static Exception Boom(bool withStack = true)
    {
        if (!withStack)
            return new InvalidOperationException("Something came apart.");

        try
        {
            throw new InvalidOperationException("Object reference not set to an instance of an object.");
        }
        catch (InvalidOperationException caught)
        {
            return caught;
        }
    }

    [Fact]
    public void The_dialog_renders_with_no_binding_errors()
    {
        string[] errors = Render(Boom(), new AppUpdateState(), out _);

        Assert.True(
            errors.Length == 0,
            "binding errors while rendering the crash dialog:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, errors.Distinct()));
    }

    [Fact]
    public void The_message_and_the_exception_type_are_both_shown()
    {
        Render(Boom(), new AppUpdateState(), out List<string> text);

        Assert.Contains("Object reference not set to an instance of an object.", text);
        Assert.Contains("System.InvalidOperationException", text);
    }

    [Fact]
    public void An_exception_with_no_stack_trace_says_so_instead_of_rendering_empty()
    {
        // Read the named element rather than the rendered text: the details panel starts collapsed,
        // so its ScrollViewer has not applied a template and nothing inside it is in the visual tree.
        Render(Boom(withStack: false), new AppUpdateState(), out _, dialog =>
        {
            Assert.Equal("(no stack trace was captured)", Named<TextBlock>(dialog, "CallStack").Text);
        });
    }

    [Fact]
    public void The_version_is_on_screen_because_the_dialog_asks_people_to_report_it()
    {
        var state = new AppUpdateState
        {
            AppVersion = new vrcosc_magicchatbox.ViewModels.Models.Version("0.9.222"),
        };

        Render(Boom(), state, out List<string> text);

        Assert.Contains(text, line => line.Contains("0.9.222", StringComparison.Ordinal));
    }

    [Fact]
    public void Recovery_buttons_stay_out_of_the_way_when_there_is_nothing_to_recover_with()
    {
        Render(Boom(), new AppUpdateState(), out _, dialog =>
        {
            Assert.Equal(Visibility.Collapsed, Named<Button>(dialog, "rollback").Visibility);
            Assert.Equal(Visibility.Collapsed, Named<Button>(dialog, "UpdateNow").Visibility);

            // Collapsed, not Hidden - the old dialog reserved dead space for a button nobody could press.
            Assert.Equal(Visibility.Visible, Named<TextBlock>(dialog, "NoRecoveryHint").Visibility);
        });
    }

    [Fact]
    public void The_rollback_button_appears_and_names_the_version_it_would_restore()
    {
        var state = new AppUpdateState
        {
            RollBackUpdateAvailable = true,
            RollBackVersion = new System.Version(0, 9, 221, 0),
        };

        Render(Boom(), state, out List<string> text, dialog =>
        {
            Assert.Equal(Visibility.Visible, Named<Button>(dialog, "rollback").Visibility);
            Assert.Equal(Visibility.Collapsed, Named<TextBlock>(dialog, "NoRecoveryHint").Visibility);
        });

        Assert.Contains(text, line => line.Contains("Go back to 0.9.221", StringComparison.Ordinal));
    }

    [Fact]
    public void The_update_button_appears_when_an_update_is_waiting()
    {
        var state = new AppUpdateState
        {
            CanUpdate = true,
            VersionTxt = "Update now",
        };

        Render(Boom(), state, out List<string> text, dialog =>
        {
            Assert.Equal(Visibility.Visible, Named<Button>(dialog, "UpdateNow").Visibility);
        });

        Assert.Contains("Update now", text);
    }

    [Fact]
    public void The_stack_trace_is_hidden_until_asked_for_and_capped_when_shown()
    {
        Render(Boom(), new AppUpdateState(), out _, dialog =>
        {
            var toggle = Named<ToggleButton>(dialog, "DetailsToggle");
            Assert.False(toggle.IsChecked ?? false);

            toggle.IsChecked = true;
            dialog.UpdateLayout();

            var stack = Named<TextBlock>(dialog, "CallStack");
            var scroller = FindAncestor<ScrollViewer>(stack);

            Assert.NotNull(scroller);
            Assert.Equal(220, scroller!.MaxHeight);
        });
    }

    [Fact]
    public void A_very_deep_stack_trace_does_not_grow_the_window_past_its_cap()
    {
        var deep = new InvalidOperationException("deep");
        string manyFrames = string.Join(
            Environment.NewLine,
            Enumerable.Range(0, 400).Select(i => $"   at Frame{i}.Method()"));

        Render(deep, new AppUpdateState(), out _, dialog =>
        {
            Named<TextBlock>(dialog, "CallStack").Text = manyFrames;
            Named<ToggleButton>(dialog, "DetailsToggle").IsChecked = true;
            dialog.UpdateLayout();

            Assert.True(
                dialog.ActualHeight <= dialog.MaxHeight,
                $"window grew to {dialog.ActualHeight}, past its {dialog.MaxHeight} cap");
        });
    }

    private static T Named<T>(DependencyObject root, string name) where T : FrameworkElement
    {
        var found = ((FrameworkElement)root).FindName(name) as T;
        Assert.True(found != null, $"'{name}' was not found in the dialog");
        return found!;
    }

    private static T? FindAncestor<T>(DependencyObject node) where T : DependencyObject
    {
        DependencyObject? current = VisualTreeHelper.GetParent(node);
        while (current != null)
        {
            if (current is T match)
                return match;
            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static string[] Render(
        Exception ex,
        AppUpdateState state,
        out List<string> text,
        Action<ApplicationError>? inspect = null)
    {
        var collected = new List<string>();
        var captured = new List<string>();

        Exception? failure = WpfHost.Run(() =>
        {
            var listener = new DialogBindingListener(collected);
            SourceLevels previous = PresentationTraceSources.DataBindingSource.Switch.Level;

            PresentationTraceSources.Refresh();
            PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;

            ApplicationError? dialog = null;
            try
            {
                dialog = new ApplicationError(
                    ex,
                    autoclose: false,
                    autoCloseinMiliSeconds: 0,
                    state,
                    new StubEnvironment(),
                    new StubHttpClientFactory(),
                    new InlineDispatcher(),
                    new StubVersionService(),
                    new NoOpNavigation())
                {
                    Left = -4000,
                    Top = -4000,
                    ShowInTaskbar = false,
                };

                dialog.Show();
                dialog.UpdateLayout();

                CollectText(dialog, captured);
                inspect?.Invoke(dialog);
            }
            finally
            {
                dialog?.Close();
                PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
                PresentationTraceSources.DataBindingSource.Switch.Level = previous;
            }
        });

        Assert.True(failure == null, "the crash dialog could not be built: " + failure);

        text = captured;
        return collected.ToArray();
    }

    private static void CollectText(DependencyObject node, List<string> into)
    {
        if (node is TextBlock block && !string.IsNullOrWhiteSpace(block.Text))
            into.Add(block.Text);

        int count = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < count; i++)
            CollectText(VisualTreeHelper.GetChild(node, i), into);
    }

    private sealed class StubEnvironment : IEnvironmentService
    {
        public string DataPath => System.IO.Path.GetTempPath();
        public string LogPath => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mcb-tests-logs-none");
        public string VrcPath => System.IO.Path.GetTempPath();
        public void SetCustomProfile(int profileNumber) { }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private sealed class InlineDispatcher : IUiDispatcher
    {
        public void Invoke(Action action) => action();
        public T Invoke<T>(Func<T> func) => func();
        public Task InvokeAsync(Action action) { action(); return Task.CompletedTask; }
        public Task<T> InvokeAsync<T>(Func<T> func) => Task.FromResult(func());
        public bool CheckAccess() => true;
        public void BeginInvoke(Action action) => action();
        public void Shutdown() { }
    }

    private sealed class StubVersionService : IVersionService
    {
        public string GetApplicationVersion() => "0.9.222";
        public Task CheckForUpdateAndWait(bool checkAgain = false) => Task.CompletedTask;
    }

    private sealed class NoOpNavigation : INavigationService
    {
        public bool OpenUrl(string url) => true;
        public bool OpenUrl(string url, string[] allowedDomains) => true;
        public bool OpenFolder(string folderPath) => true;
        public bool OpenFileInExplorer(string filePath) => true;
    }

    private sealed class DialogBindingListener : TraceListener
    {
        private readonly List<string> _messages;

        public DialogBindingListener(List<string> messages) => _messages = messages;

        public override void Write(string? message) { }

        public override void WriteLine(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                _messages.Add(message!);
        }

        public override void TraceEvent(
            TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? message)
        {
            if (eventType <= TraceEventType.Error && !string.IsNullOrWhiteSpace(message))
                _messages.Add(message!);
        }

        public override void TraceEvent(
            TraceEventCache? eventCache, string source, TraceEventType eventType, int id,
            string? format, params object?[]? args)
        {
            if (eventType > TraceEventType.Error || string.IsNullOrWhiteSpace(format))
                return;

            _messages.Add(args is { Length: > 0 } ? string.Format(format!, args) : format!);
        }
    }
}
