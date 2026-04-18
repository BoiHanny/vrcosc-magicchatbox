using System.Windows;

namespace vrcosc_magicchatbox.Classes
{
    /// <summary>
    /// Attached property that exposes a <c>ShadowTrigger</c> boolean on any
    /// <see cref="DependencyObject"/> for use as a XAML trigger target.
    /// </summary>
    public class ButtonProperties
    {
        public static readonly DependencyProperty ShadowTriggerProperty =
            DependencyProperty.RegisterAttached("ShadowTrigger", typeof(bool), typeof(ButtonProperties), new PropertyMetadata(false, OnShadowTriggerChanged));

        public static bool GetShadowTrigger(DependencyObject obj)
        {
            return (bool)obj.GetValue(ShadowTriggerProperty);
        }

        public static void SetShadowTrigger(DependencyObject obj, bool value)
        {
            obj.SetValue(ShadowTriggerProperty, value);
        }

        private static void OnShadowTriggerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
        }
    }
}
