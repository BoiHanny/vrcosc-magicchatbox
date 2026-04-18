using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.UI.Pages
{
    /// <summary>Code-behind for the status page, handling inline editing and favorite toggling for status items.</summary>
    public partial class StatusPage : UserControl
    {
        private StatusPageViewModel VM => (StatusPageViewModel)DataContext;

        public StatusPage()
        {
            InitializeComponent();
        }

        private void AddFav_Click(object sender, RoutedEventArgs e)
            => VM.AddStatusCommand.Execute(null);

        private void CancelEditbutton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            VM.CancelEditCommand.Execute(button?.Tag as StatusItem);
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            VM.DeleteStatusCommand.Execute(button?.Tag as StatusItem);
        }

        private void Editbutton_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as ToggleButton;
                var item = button?.Tag as StatusItem;
                if (item == null) return;

                if ((bool)button.IsChecked)
                {
                    // Begin edit — business logic in VM, then focus the textbox
                    VM.BeginEdit(item);

                    var parent = VisualTreeHelper.GetParent(button);
                    while (!(parent is ContentPresenter))
                        parent = VisualTreeHelper.GetParent(parent);
                    var contentPresenter = parent as ContentPresenter;
                    var editTextBox = (TextBox)contentPresenter.ContentTemplate.FindName("EditTextBox", contentPresenter);
                    editTextBox.Focus();
                    editTextBox.CaretIndex = editTextBox.Text.Length;
                }
                else
                {
                    VM.ConfirmEdit(item);
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        private void FavBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                VM.AddStatusCommand.Execute(null);
            if (e.Key == Key.Escape)
                VM.ClearStatusInputCommand.Execute(null);
        }

        private void Favbutton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is StatusItem item)
                VM.ToggleFavoriteCommand.Execute(item);
        }

        private void NewFavText_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            VM.UpdateStatusBoxCount(textBox?.Text.Length ?? 0);
        }
    }
}
