using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;

namespace vrcosc_magicchatbox.Classes
{
    public class CustomKeyBinding : Behavior<UIElement>
    {
        public Key Key
        {
            get { return (Key)GetValue(KeyProperty); }
            set { SetValue(KeyProperty, value); }
        }

        public static readonly DependencyProperty KeyProperty =
            DependencyProperty.Register(nameof(Key), typeof(Key), typeof(CustomKeyBinding), new PropertyMetadata(Key.None));

        public ICommand Command
        {
            get { return (ICommand)GetValue(CommandProperty); }
            set { SetValue(CommandProperty, value); }
        }

        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(CustomKeyBinding), new PropertyMetadata(null));

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.PreviewKeyDown += OnPreviewKeyDown;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            AssociatedObject.PreviewKeyDown -= OnPreviewKeyDown;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!(e.OriginalSource is TextBoxBase || e.OriginalSource is PasswordBox || e.OriginalSource is ComboBox))
            {
                if (e.Key == Key)
                {
                    e.Handled = true;
                    if (Command != null && Command.CanExecute(null))
                    {
                        Command.Execute(null);
                    }
                }
            }
        }
    }
}
