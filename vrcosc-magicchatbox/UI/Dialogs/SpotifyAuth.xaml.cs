using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using vrcosc_magicchatbox.ViewModels.Sections;

namespace vrcosc_magicchatbox.UI.Dialogs;

/// <summary>
/// Friendly Spotify OAuth setup dialog with privacy choices.
/// </summary>
public partial class SpotifyAuth : Window
{
    private readonly SpotifySectionViewModel _vm;

    public SpotifyAuth(SpotifySectionViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        ClientIdBox.Password = vm.Settings.ClientId;
        ConnectButton.IsEnabled = !string.IsNullOrWhiteSpace(ClientIdBox.Password);
        AllowTitle.IsChecked = vm.Settings.AllowTrackTitleInOutput;
        AllowArtist.IsChecked = vm.Settings.AllowArtistInOutput;
        AllowAlbum.IsChecked = vm.Settings.AllowAlbumInOutput;
        AllowDevice.IsChecked = vm.Settings.AllowDeviceInOutput;
        PrivacyMode.IsChecked = vm.Settings.PrivacyMode;
    }

    private void OpenDashboard_Click(object sender, RoutedEventArgs e)
        => _vm.OpenDeveloperDashboardCommand.Execute(null);

    private void ClientIdBox_PasswordChanged(object sender, RoutedEventArgs e)
        => ConnectButton.IsEnabled = !string.IsNullOrWhiteSpace(ClientIdBox.Password);

    private void PasteClientId_Click(object sender, RoutedEventArgs e)
    {
        var text = Clipboard.GetText().Trim();
        if (string.IsNullOrWhiteSpace(text))
            return;

        ClientIdBox.Password = text;
        ConnectButton.IsEnabled = true;
    }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        ConnectButton.IsEnabled = false;
        StatusText.Foreground = Brushes.LightYellow;
        StatusText.Text = "Opening Spotify in your browser. Complete the approval, then return here...";

        _vm.Settings.AllowTrackTitleInOutput = AllowTitle.IsChecked == true;
        _vm.Settings.AllowArtistInOutput = AllowArtist.IsChecked == true;
        _vm.Settings.AllowAlbumInOutput = AllowAlbum.IsChecked == true;
        _vm.Settings.AllowDeviceInOutput = AllowDevice.IsChecked == true;
        _vm.Settings.PrivacyMode = PrivacyMode.IsChecked == true;
        _vm.Settings.PrivacyChoicesCompleted = true;

        try
        {
            await _vm.ConnectWithClientIdAsync(ClientIdBox.Password);
            if (_vm.Display.IsConnected)
            {
                DialogResult = true;
                Close();
                return;
            }

            StatusText.Foreground = Brushes.OrangeRed;
            StatusText.Text = "Spotify did not connect. Check your Client ID and redirect URI, then try again.";
        }
        catch (Exception ex)
        {
            StatusText.Foreground = Brushes.OrangeRed;
            StatusText.Text = $"Spotify setup failed: {ex.Message}";
        }
        finally
        {
            ConnectButton.IsEnabled = !string.IsNullOrWhiteSpace(ClientIdBox.Password);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.GetPosition(this).Y < 35)
            DragMove();
    }
}
