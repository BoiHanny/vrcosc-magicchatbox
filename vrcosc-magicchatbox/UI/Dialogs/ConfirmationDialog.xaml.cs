using System.Windows;
using System.Windows.Input;

namespace vrcosc_magicchatbox.UI.Dialogs;

public partial class ConfirmationDialog : Window
{
    public ConfirmationDialog(
        string title,
        string message,
        string hint,
        string confirmText = "Confirm")
    {
        InitializeComponent();
        Title = title;
        TitleTextBlock.Text = title;
        MessageTextBlock.Text = message;
        HintTextBlock.Text = hint;
        HintTextBlock.Visibility = string.IsNullOrWhiteSpace(hint) ? Visibility.Collapsed : Visibility.Visible;
        ConfirmButton.Content = confirmText;
    }

    public static bool Show(
        string title,
        string message,
        string hint,
        string confirmText = "Confirm",
        Window? owner = null)
    {
        var dialog = new ConfirmationDialog(title, message, hint, confirmText);
        DialogWindowHelper.PrepareModal(dialog, owner);
        return dialog.ShowDialog() == true;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void DragArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
}
