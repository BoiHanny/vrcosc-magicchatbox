using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Xunit;

namespace MagicChatbox.Tests.UI;

public class IntegrationTileDeckTests
{
    [Fact]
    public void No_tile_control_deck_clips_the_controls_inside_it()
    {
        // A deck row that overruns its deck does not wrap or scroll - it is right-anchored, so the
        // leftmost chip is silently sliced in half. Nothing about that fails a build or a binding,
        // which is exactly why it shipped: "VOICE" rendered as "ICE".
        var overflowing = new List<string>();

        Exception? failure = WpfHost.RunInWindow(
            () => new vrcosc_magicchatbox.UI.Pages.IntegrationsPage(),
            page =>
            {
                page.UpdateLayout();

                foreach (StackPanel deck in FindDecks(page))
                {
                    deck.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                    // DesiredSize counts the element's own margin; ActualWidth does not.
                    double needed = deck.DesiredSize.Width - deck.Margin.Left - deck.Margin.Right;
                    double given = deck.ActualWidth;

                    if (given > 0 && needed > given + 0.5)
                        overflowing.Add($"{DescribeDeck(deck)} needs {needed:0}px but was given {given:0}px");
                }
            });

        Assert.True(failure == null, "the integrations page did not build: " + failure);
        Assert.True(
            overflowing.Count == 0,
            "control decks are clipping their contents:" + Environment.NewLine + string.Join(Environment.NewLine, overflowing));
    }

    private static string DescribeDeck(StackPanel deck)
    {
        string[] labels = Descendants<ContentControl>(deck)
            .Select(control => control.Content as string)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => text!)
            .ToArray();

        return labels.Length == 0 ? "a deck" : string.Join("/", labels);
    }

    private static IEnumerable<StackPanel> FindDecks(DependencyObject root)
    {
        object? deckStyle = Application.Current?.TryFindResource("TileControlDeck");

        return Descendants<StackPanel>(root)
            .Where(panel => deckStyle != null && ReferenceEquals(panel.Style, deckStyle));
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        int children = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < children; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed)
                yield return typed;

            foreach (T nested in Descendants<T>(child))
                yield return nested;
        }
    }
}
