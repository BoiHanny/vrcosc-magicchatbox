using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using vrcosc_magicchatbox.Core.Updates;
using vrcosc_magicchatbox.UI.Controls;
using Xunit;

namespace MagicChatbox.Tests.UI;

/// <summary>
/// The card is the whole point of the update rework, and every one of its bindings can fail silently:
/// WPF writes to a trace source and renders nothing. These tests render it for real and read the text
/// back out of the visual tree, so a typo'd path fails here instead of shipping as an empty panel.
/// </summary>
public class UpdateProgressCardTests
{
    private static UpdateProgressState MidUpdate()
    {
        var state = new UpdateProgressState();
        state.Begin("Updating to 0.9.222");
        state.SetStep(UpdateStepKind.Download, UpdateStepStatus.Done, "14.7 MB");
        state.SetStep(UpdateStepKind.Verify, UpdateStepStatus.Done, "Valid package from the developer · 772388f6727f");
        state.SetStep(UpdateStepKind.Unpack, UpdateStepStatus.Running);
        state.Report(68, "10.0 MB of 14.7 MB · 2.1 MB/s");
        return state;
    }

    [Fact]
    public void The_card_renders_with_no_binding_errors()
    {
        string[] errors = Render(MidUpdate(), out _);

        Assert.True(
            errors.Length == 0,
            "binding errors while rendering the update card:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, errors.Distinct()));
    }

    [Fact]
    public void The_card_shows_the_headline_the_progress_line_and_every_step()
    {
        Render(MidUpdate(), out List<string> text);

        Assert.Contains("Updating to 0.9.222", text);
        Assert.Contains("10.0 MB of 14.7 MB · 2.1 MB/s", text);
        Assert.Contains("Download", text);
        Assert.Contains("Verify integrity", text);
        Assert.Contains("Unpack", text);
        Assert.Contains("Install", text);
    }

    [Fact]
    public void The_integrity_result_is_actually_on_screen()
    {
        Render(MidUpdate(), out List<string> text);

        Assert.Contains("Valid package from the developer · 772388f6727f", text);
    }

    [Fact]
    public void A_release_with_no_checksum_says_so_instead_of_claiming_it_verified()
    {
        var state = new UpdateProgressState();
        state.Begin("Updating to 0.9.222");
        state.SetStep(UpdateStepKind.Verify, UpdateStepStatus.Warning, "No checksum published for this release");

        Render(state, out List<string> text);

        Assert.Contains("No checksum published for this release", text);
        Assert.DoesNotContain(text, line => line.Contains("Valid package", StringComparison.Ordinal));
    }

    [Fact]
    public void The_percentage_is_rendered_once_the_length_is_known()
    {
        Render(MidUpdate(), out List<string> text);

        Assert.Contains("68%", text);
    }

    [Fact]
    public void An_idle_card_is_collapsed_so_it_does_not_hold_space_above_the_toasts()
    {
        var state = new UpdateProgressState();

        Render(state, out _, card =>
        {
            var border = (Border)card.FindName("CardRoot");
            Assert.Equal(Visibility.Collapsed, border.Visibility);
        });
    }

    [Fact]
    public void The_card_becomes_visible_once_an_update_starts()
    {
        var state = MidUpdate();

        Render(state, out _, card =>
        {
            var border = (Border)card.FindName("CardRoot");
            Assert.Equal(Visibility.Visible, border.Visibility);
        });
    }

    private static string[] Render(
        UpdateProgressState state,
        out List<string> text,
        Action<UpdateProgressCard>? inspect = null)
    {
        var collected = new List<string>();
        var captured = new List<string>();

        Exception? failure = WpfHost.Run(() =>
        {
            var listener = new BindingListener(collected);
            SourceLevels previous = PresentationTraceSources.DataBindingSource.Switch.Level;

            PresentationTraceSources.Refresh();
            PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;

            try
            {
                var card = new UpdateProgressCard { DataContext = state };
                var window = new Window
                {
                    Width = 420,
                    Height = 520,
                    Left = -4000,
                    Top = -4000,
                    ShowInTaskbar = false,
                    Content = card,
                };

                window.Show();
                window.UpdateLayout();

                CollectText(card, captured);
                inspect?.Invoke(card);

                window.Close();
            }
            finally
            {
                PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
                PresentationTraceSources.DataBindingSource.Switch.Level = previous;
            }
        });

        Assert.True(failure == null, "the update card could not be built: " + failure);

        text = captured;
        return collected.ToArray();
    }

    private static void CollectText(DependencyObject node, List<string> into)
    {
        if (node is TextBlock block && !string.IsNullOrWhiteSpace(block.Text))
        {
            into.Add(block.Text);
        }

        int count = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < count; i++)
        {
            CollectText(VisualTreeHelper.GetChild(node, i), into);
        }
    }

    private sealed class BindingListener : TraceListener
    {
        private readonly List<string> _messages;

        public BindingListener(List<string> messages) => _messages = messages;

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
