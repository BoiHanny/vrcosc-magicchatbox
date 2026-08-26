using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;

namespace vrcosc_magicchatbox.UI.Dialogs;

public partial class ConfirmationDialog : Window
{
    // Destructive is the default because a confirmation is only ever asked for when saying yes
    // costs the user something; a caller with a harmless action opts out and gets the accent styling.
    public ConfirmationDialog(
        string title,
        string message,
        string hint,
        string confirmText = "Confirm",
        bool isDestructive = true)
    {
        InitializeComponent();
        Title = title;
        TitleTextBlock.Text = title;
        MessageTextBlock.Text = message;
        HintTextBlock.Text = hint;
        HintTextBlock.Visibility = string.IsNullOrWhiteSpace(hint) ? Visibility.Collapsed : Visibility.Visible;
        ConfirmButton.Content = confirmText;

        ConfirmButton.Style = (Style)FindResource(isDestructive ? "DialogDangerButton" : "DialogPrimaryButton");
        SeverityRail.Background = (Brush)FindResource(isDestructive ? "StatusErrorBrush" : "AccentLightPurpleBrush");

        AutomationProperties.SetName(this, title);
        AutomationProperties.SetName(ConfirmButton, confirmText);
    }

    public static bool Show(
        string title,
        string message,
        string hint,
        string confirmText = "Confirm",
        Window? owner = null,
        bool isDestructive = true)
    {
        var dialog = new ConfirmationDialog(title, message, hint, confirmText, isDestructive);
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
}
