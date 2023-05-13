using System.Windows;

namespace vrcosc_magicchatbox.Classes
{
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
            // We'll handle the changes here
        }
    }
}
