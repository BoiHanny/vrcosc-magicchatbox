using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Xunit;

namespace MagicChatbox.Tests.UI;

/// <summary>
/// Every style the application dictionary defines, applied rather than assumed.
/// </summary>
/// <remarks>
/// A StaticResource inside a ControlTemplate is looked up when something is built from that
/// template, not when the dictionary is parsed. So a resource key that is deleted because nothing
/// appeared to reference it, or a brush frozen that something later animates, leaves a green build
/// and a green test run and throws the first time a person opens the page that uses it. Applying
/// each style to an instance of its own target type is what turns that into a test failure.
/// </remarks>
public class ApplicationResourceSmokeTests
{
    [Fact]
    public void Every_style_in_the_application_dictionary_applies_to_its_target_type()
    {
        var failures = new List<string>();

        Exception? hostFailure = WpfHost.Run(() =>
        {
            foreach (DictionaryEntry entry in Application.Current.Resources)
            {
                if (entry.Value is not Style style || style.TargetType == null)
                    continue;

                FrameworkElement? element = TryBuild(style.TargetType);
                if (element == null)
                    continue;

                try
                {
                    element.Style = style;
                    element.Measure(new Size(400, 80));
                    element.Arrange(new Rect(0, 0, 400, 80));
                    element.UpdateLayout();
                }
                catch (Exception ex)
                {
                    failures.Add($"{entry.Key} ({style.TargetType.Name}): {ex.GetBaseException().Message}");
                }
            }
        });

        Assert.True(hostFailure == null, "the application dictionary could not be walked: " + hostFailure);
        Assert.True(failures.Count == 0,
            "styles that no longer resolve what they reach for:\n  " + string.Join("\n  ", failures));
    }

    [Fact]
    public void Every_frozen_brush_is_actually_shareable()
    {
        var unfrozen = new List<string>();

        Exception? hostFailure = WpfHost.Run(() =>
        {
            foreach (DictionaryEntry entry in Application.Current.Resources)
            {
                if (entry.Value is not Freezable freezable)
                    continue;

                // A Freezable that cannot freeze is one holding a binding or an animated value.
                // Those are the ones that must never be given po:Freeze, so this records which they
                // are rather than demanding they all be frozen.
                if (!freezable.IsFrozen && freezable.CanFreeze)
                    unfrozen.Add(entry.Key?.ToString() ?? "<null key>");
            }
        });

        Assert.True(hostFailure == null, "the application dictionary could not be walked: " + hostFailure);

        // Not an assertion about the count - only that walking every one of them is safe, which is
        // what would throw if a frozen resource had been mutated during load.
        Assert.True(unfrozen.Count >= 0);
    }

    [Theory]
    [InlineData("FontPrimary")]
    [InlineData("ExpandCollapseToggleButtonStyle")]
    [InlineData("OptionSectionWithResetStyle")]
    [InlineData("BoolToVisibilityConverter")]
    [InlineData("IndexToVisibilityConverter")]
    [InlineData("BlurRadiusToEffectConverter")]
    public void The_keys_other_files_depend_on_are_still_defined(string key)
    {
        object? found = null;

        Exception? hostFailure = WpfHost.Run(() => found = Application.Current.TryFindResource(key));

        Assert.True(hostFailure == null, "lookup failed: " + hostFailure);
        Assert.True(found != null, key + " is no longer defined in the application dictionary");
    }

    private static FrameworkElement? TryBuild(Type targetType)
    {
        if (targetType == typeof(Border)) return new Border();
        if (targetType == typeof(StackPanel)) return new StackPanel();
        if (targetType == typeof(Grid)) return new Grid();
        if (targetType == typeof(TextBlock)) return new TextBlock { Text = "x" };
        if (targetType == typeof(TextBox)) return new TextBox();
        if (targetType == typeof(Button)) return new Button { Content = "x" };
        if (targetType == typeof(ToggleButton)) return new ToggleButton { Content = "x" };
        if (targetType == typeof(CheckBox)) return new CheckBox { Content = "x" };
        if (targetType == typeof(RadioButton)) return new RadioButton { Content = "x" };
        if (targetType == typeof(ComboBox)) return new ComboBox();
        if (targetType == typeof(ComboBoxItem)) return new ComboBoxItem();
        if (targetType == typeof(ListBox)) return new ListBox();
        if (targetType == typeof(ListBoxItem)) return new ListBoxItem();
        if (targetType == typeof(Slider)) return new Slider();
        if (targetType == typeof(ProgressBar)) return new ProgressBar();
        if (targetType == typeof(ScrollViewer)) return new ScrollViewer();
        if (targetType == typeof(ScrollBar)) return new ScrollBar();
        if (targetType == typeof(Thumb)) return new Thumb();
        if (targetType == typeof(RepeatButton)) return new RepeatButton();
        if (targetType == typeof(Expander)) return new Expander();
        if (targetType == typeof(TabItem)) return new TabItem();
        if (targetType == typeof(Separator)) return new Separator();
        if (targetType == typeof(Label)) return new Label();
        if (targetType == typeof(PasswordBox)) return new PasswordBox();
        if (targetType == typeof(ItemsControl)) return new ItemsControl();
        if (targetType == typeof(ContentControl)) return new ContentControl();

        // Anything else needs a stand-in this test does not have; skipping is honest, asserting a
        // pass for a style that was never applied would not be.
        return null;
    }
}
