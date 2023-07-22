using System.Windows.Input;
using System.Windows;
using vrcosc_magicchatbox.ViewModels;
using System.Windows.Controls;

namespace vrcosc_magicchatbox.Classes
{
    public static class TextBoxBehaviour
    {
        public static bool GetHandleEnterKey(DependencyObject obj)
        {
            return (bool)obj.GetValue(HandleEnterKeyProperty);
        }

        public static void SetHandleEnterKey(DependencyObject obj, bool value)
        {
            obj.SetValue(HandleEnterKeyProperty, value);
        }

        public static readonly DependencyProperty HandleEnterKeyProperty =
            DependencyProperty.RegisterAttached("HandleEnterKey", typeof(bool), typeof(TextBoxBehaviour), new PropertyMetadata(false, HandleEnterKeyPropertyChanged));

        private static void HandleEnterKeyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBox textBox)
            {
                textBox.KeyDown -= TextBox_KeyDown;

                if ((bool)e.NewValue)
                {
                    textBox.KeyDown += TextBox_KeyDown;
                }
            }
        }

        private static void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || e.Key == Key.Enter)
            {
                var textBox = sender as TextBox;
                var item = textBox.DataContext as StatusItem;
                if (item == null) return;

                if (e.Key == Key.Escape)
                {
                    // Run your logic to cancel the edit here
                    item.editMsg = "";
                    item.IsEditing = false;
                }
                else if (e.Key == Key.Enter)
                {
                    // If enter is pressed, IsEditing is set to false
                    item.editMsg = textBox.Text;
                    item.IsEditing = false;
                }
            }
        }

    }
}

