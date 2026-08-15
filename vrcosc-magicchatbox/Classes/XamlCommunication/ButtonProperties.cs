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
        }

        /// <summary>
        /// Marks the tab the user is currently on.
        /// </summary>
        /// <remarks>
        /// Which tab is selected is decided outside the control template, by a binding against the
        /// page index. A template cannot see that, so the state is put here where the template can
        /// trigger on it - and it is a real property rather than a borrowed Tag so nothing else on
        /// the button quietly fights over it.
        /// </remarks>
        public static readonly DependencyProperty IsActiveProperty =
            DependencyProperty.RegisterAttached(
                "IsActive",
                typeof(bool),
                typeof(ButtonProperties),
                new PropertyMetadata(false));

        public static bool GetIsActive(DependencyObject obj) => (bool)obj.GetValue(IsActiveProperty);

        public static void SetIsActive(DependencyObject obj, bool value) => obj.SetValue(IsActiveProperty, value);
    }
}
