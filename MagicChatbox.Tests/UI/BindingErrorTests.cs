using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Xunit;

namespace MagicChatbox.Tests.UI;

/// <summary>
/// WPF reports a failed binding by writing to a trace source and carrying on, so a typo'd path or an
/// ancestor walk that cannot resolve costs nothing at build time and nothing at test time - it just
/// quietly renders the wrong thing forever. Listening to that trace source turns it into a failure.
/// </summary>
public class BindingErrorTests
{
    [Fact]
    public void A_combo_box_whose_items_are_realized_detached_reports_no_binding_errors()
    {
        // ComboBoxItem's framework style aligns content through a FindAncestor binding up to an
        // ItemsControl. Generating containers while the ComboBox is outside the visual tree is
        // exactly the case where that walk has nothing to find - which is what the app hits, and
        // what a ComboBox built inside a live window does not reproduce.
        string[] errors = CaptureBindingErrors(() =>
        {
            var combo = new ComboBox { ItemsSource = new[] { "one", "two", "three" } };

            combo.ApplyTemplate();
            combo.Measure(new Size(300, 200));
            combo.Arrange(new Rect(0, 0, 300, 200));

            IItemContainerGenerator generator = combo.ItemContainerGenerator;
            using (generator.StartAt(new GeneratorPosition(-1, 0), GeneratorDirection.Forward))
            {
                for (int i = 0; i < 3; i++)
                {
                    if (generator.GenerateNext() is ComboBoxItem item)
                    {
                        generator.PrepareItemContainer(item);
                        item.Measure(new Size(300, 24));
                    }
                }
            }
        });

        Assert.True(
            errors.Length == 0,
            "binding errors while realizing ComboBox items:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
    }

    [Fact]
    public void Building_the_integrations_page_reports_no_binding_errors()
    {
        string[] errors = CaptureBindingErrors(() =>
        {
            var page = new vrcosc_magicchatbox.UI.Pages.IntegrationsPage();
            var window = new Window
            {
                Width = 1100,
                Height = 800,
                Left = -4000,
                Top = -4000,
                ShowInTaskbar = false,
                Content = page,
            };

            window.Show();
            window.UpdateLayout();
            window.Close();
        });

        Assert.True(
            errors.Length == 0,
            "binding errors while building the integrations page:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, errors.Distinct()));
    }

    private static string[] CaptureBindingErrors(Action build)
    {
        var collected = new List<string>();

        Exception? failure = WpfHost.Run(() =>
        {
            var listener = new CollectingListener(collected);
            SourceLevels previous = PresentationTraceSources.DataBindingSource.Switch.Level;

            PresentationTraceSources.Refresh();
            PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;

            try
            {
                build();
            }
            finally
            {
                PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
                PresentationTraceSources.DataBindingSource.Switch.Level = previous;
            }
        });

        Assert.True(failure == null, "the control could not be built: " + failure);
        return collected.ToArray();
    }

    private sealed class CollectingListener : TraceListener
    {
        private readonly List<string> _messages;

        public CollectingListener(List<string> messages) => _messages = messages;

        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                _messages.Add(message!);
        }

        public override void TraceEvent(
            TraceEventCache? eventCache,
            string source,
            TraceEventType eventType,
            int id,
            string? message)
        {
            if (eventType <= TraceEventType.Error && !string.IsNullOrWhiteSpace(message))
                _messages.Add(message!);
        }

        public override void TraceEvent(
            TraceEventCache? eventCache,
            string source,
            TraceEventType eventType,
            int id,
            string? format,
            params object?[]? args)
        {
            if (eventType > TraceEventType.Error || string.IsNullOrWhiteSpace(format))
                return;

            _messages.Add(args is { Length: > 0 } ? string.Format(format!, args) : format!);
        }
    }
}
