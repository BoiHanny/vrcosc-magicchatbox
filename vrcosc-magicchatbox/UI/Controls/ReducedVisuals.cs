using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace vrcosc_magicchatbox.UI.Controls;

/// <summary>
/// Gives the graphics card back the time the app spends on decoration: drop shadows, glows, partial
/// opacity and animation. Every <see cref="UIElement.Effect"/> and every element below full opacity
/// makes WPF render that subtree into an offscreen surface and composite it again, once per frame.
/// </summary>
/// <remarks>
/// This works by coercion rather than by walking the tree and overwriting values. A sweep is wrong here:
/// most of these values come from style and template triggers, so hovering a button or selecting a tab
/// puts the shadow straight back, and the sweep only ever caught what happened to exist when it ran.
/// A <see cref="CoerceValueCallback"/> runs on every write from every source, including triggers and
/// animations, so there is nowhere for one to come back from.
///
/// While the mode is off, both callbacks hand back the value they were given, so nothing about normal
/// rendering changes.
/// </remarks>
public static class ReducedVisuals
{
    /// <summary>
    /// Elements whose effect was a blur. A blur is never structural - the element exists only to be a
    /// soft glow - so dropping the effect would leave a hard-edged block sitting there. Those get
    /// hidden instead. A drop shadow is decoration on real content, so that content stays.
    /// </summary>
    private static readonly ConditionalWeakTable<UIElement, object> BlurredDecoration = new();

    private static readonly object Marker = new();

    /// <summary>Animation frame rate while reduced. Low enough to be cheap, not zero, so state still settles.</summary>
    private const int ReducedFrameRate = 10;

    private static bool _installed;
    private static bool _enabled;

    public static bool IsEnabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
                return;

            _enabled = value;
            Changed?.Invoke();
        }
    }

    /// <summary>Raised when the mode is turned on or off, so live trees can be re-coerced.</summary>
    public static event Action? Changed;

    /// <summary>
    /// Hooks the properties. Must run before the first window is built, because metadata cannot be
    /// overridden once a type is in use.
    /// </summary>
    public static void Install()
    {
        if (_installed)
            return;

        _installed = true;

        UIElement.EffectProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                null,
                CoerceEffect));

        UIElement.OpacityProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(
                1.0,
                FrameworkPropertyMetadataOptions.AffectsRender,
                null,
                CoerceOpacity));

        Timeline.DesiredFrameRateProperty.OverrideMetadata(
            typeof(Timeline),
            new FrameworkPropertyMetadata(null, null, CoerceFrameRate));
    }

    /// <summary>Re-runs the coercion over a subtree, for content that already exists.</summary>
    public static void Refresh(DependencyObject? root)
    {
        if (root == null)
            return;

        if (root is UIElement element)
        {
            element.CoerceValue(UIElement.EffectProperty);
            element.CoerceValue(UIElement.OpacityProperty);
        }

        int children = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < children; i++)
            Refresh(VisualTreeHelper.GetChild(root, i));
    }

    private static object? CoerceEffect(DependencyObject d, object? baseValue)
    {
        if (d is UIElement element)
        {
            if (baseValue is BlurEffect)
                BlurredDecoration.AddOrUpdate(element, Marker);
            else if (baseValue == null)
                BlurredDecoration.Remove(element);
        }

        return _enabled ? null : baseValue;
    }

    private static object CoerceOpacity(DependencyObject d, object baseValue)
    {
        if (!_enabled || baseValue is not double opacity)
            return baseValue;

        // Something deliberately hidden stays hidden.
        if (opacity <= 0)
            return baseValue;

        if (d is UIElement element && BlurredDecoration.TryGetValue(element, out _))
            return 0d;

        return opacity < 1 ? 1d : baseValue;
    }

    private static object? CoerceFrameRate(DependencyObject d, object? baseValue)
    {
        if (!_enabled)
            return baseValue;

        return baseValue is int requested && requested > 0 && requested < ReducedFrameRate
            ? baseValue
            : ReducedFrameRate;
    }
}
