using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using vrcosc_magicchatbox.UI.Controls;
using Xunit;

namespace MagicChatbox.Tests.UI;

/// <summary>
/// Reduced visuals works by coercion, not by sweeping the tree. A sweep would be silently wrong: most
/// of these values come from style and template triggers, so hovering a button or selecting a tab puts
/// the shadow straight back and only a screenshot at the wrong moment would show it. These check the
/// values that a trigger writes afterwards, not just the ones present at the start.
/// </summary>
public class ReducedVisualsTests
{
    [Fact]
    public void Turning_it_on_clears_effects_and_flattens_dimming()
    {
        bool effectCleared = false;
        double dimmedOpacity = 0;

        Exception? failure = Run((root, parts) =>
        {
            ReducedVisuals.IsEnabled = true;
            ReducedVisuals.Refresh(root);

            effectCleared = parts.Shadowed.Effect == null;
            dimmedOpacity = parts.Dimmed.Opacity;
        });

        Assert.True(failure == null, "the host did not build: " + failure);
        Assert.True(effectCleared, "the effect was left in place");
        Assert.Equal(1.0, dimmedOpacity);
    }

    [Fact]
    public void An_effect_applied_after_the_mode_is_on_is_still_refused()
    {
        bool stayedClear = false;

        Exception? failure = Run((root, parts) =>
        {
            ReducedVisuals.IsEnabled = true;
            ReducedVisuals.Refresh(root);

            // What a trigger does on hover or selection.
            parts.Dimmed.Effect = new DropShadowEffect { BlurRadius = 12 };

            stayedClear = parts.Dimmed.Effect == null;
        });

        Assert.True(failure == null, "the host did not build: " + failure);
        Assert.True(stayedClear, "an effect set while the mode was on came through");
    }

    [Fact]
    public void Turning_it_off_puts_the_originals_back()
    {
        bool effectRestored = false;
        double dimmedOpacity = 0;

        Exception? failure = Run((root, parts) =>
        {
            ReducedVisuals.IsEnabled = true;
            ReducedVisuals.Refresh(root);

            ReducedVisuals.IsEnabled = false;
            ReducedVisuals.Refresh(root);

            effectRestored = parts.Shadowed.Effect != null;
            dimmedOpacity = parts.Dimmed.Opacity;
        });

        Assert.True(failure == null, "the host did not build: " + failure);
        Assert.True(effectRestored, "the effect never came back");
        Assert.Equal(0.5, dimmedOpacity, 3);
    }

    [Fact]
    public void A_blurred_glow_is_hidden_rather_than_left_hard_edged()
    {
        double glowOpacity = -1;

        Exception? failure = Run((root, parts) =>
        {
            // A glow starts invisible and is animated up by a trigger; that is when it would show.
            parts.Glow.Opacity = 0.8;

            ReducedVisuals.IsEnabled = true;
            ReducedVisuals.Refresh(root);

            glowOpacity = parts.Glow.Opacity;
        });

        Assert.True(failure == null, "the host did not build: " + failure);
        Assert.Equal(0.0, glowOpacity);
    }

    [Fact]
    public void A_deliberately_invisible_element_is_left_alone()
    {
        double invisibleOpacity = -1;

        Exception? failure = Run((root, parts) =>
        {
            ReducedVisuals.IsEnabled = true;
            ReducedVisuals.Refresh(root);

            invisibleOpacity = parts.Invisible.Opacity;
        });

        Assert.True(failure == null, "the host did not build: " + failure);
        Assert.Equal(0.0, invisibleOpacity);
    }

    /// <summary>
    /// Everything runs on the host's own thread - the elements have dispatcher affinity - and the mode
    /// is a process-wide switch that must not be left on for whatever test runs next.
    /// </summary>
    private static Exception? Run(Action<FrameworkElement, Parts> body)
        => WpfHost.RunInWindow(Build, element =>
        {
            var panel = (StackPanel)((ContentControl)element).Content;
            var parts = new Parts(
                (Border)panel.Children[0],
                (Border)panel.Children[1],
                (Border)panel.Children[2],
                (Border)panel.Children[3]);

            ReducedVisuals.Install();

            try
            {
                body(element, parts);
            }
            finally
            {
                ReducedVisuals.IsEnabled = false;
                ReducedVisuals.Refresh(element);
            }
        });

    private static FrameworkElement Build()
    {
        var panel = new StackPanel();

        panel.Children.Add(new Border
        {
            Height = 20,
            Effect = new DropShadowEffect { BlurRadius = 10, Color = Colors.Black },
        });

        panel.Children.Add(new Border { Height = 20, Opacity = 0.5 });
        panel.Children.Add(new Border { Height = 20, Opacity = 0.0 });

        panel.Children.Add(new Border
        {
            Height = 20,
            Opacity = 0.0,
            Effect = new BlurEffect { KernelType = KernelType.Gaussian, Radius = 26 },
        });

        return new ContentControl { Content = panel, Width = 300, Height = 200 };
    }

    private readonly record struct Parts(Border Shadowed, Border Dimmed, Border Invisible, Border Glow);
}
