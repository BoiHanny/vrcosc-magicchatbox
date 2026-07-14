using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes
{
    /// <summary>
    /// Attached behaviour that intercepts Enter and Escape key presses inside a
    /// <see cref="TextBox"/> and commits or cancels a <see cref="StatusItem"/> inline edit.
    /// Attach via <c>local:TextBoxBehaviour.HandleEnterKey="True"</c>.
    /// </summary>
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
                    item.editMsg = "";
                    item.IsEditing = false;
                }
                else if (e.Key == Key.Enter)
                {
                    item.editMsg = textBox.Text;
                    item.IsEditing = false;
                }
            }
        }

    }
}

