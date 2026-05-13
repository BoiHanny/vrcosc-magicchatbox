using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.UI.Dialogs;
using vrcosc_magicchatbox.ViewModels.Sections;

namespace vrcosc_magicchatbox.UI.Pages.Options;

/// <summary>Code-behind for Spotify integration settings.</summary>
public partial class SpotifySection : UserControl
{
    public SpotifySection()
    {
        InitializeComponent();
    }

    private void ConnectSpotify_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SpotifySectionViewModel vm)
        {
            var dialog = new SpotifyAuth(vm);
            DialogWindowHelper.PrepareModal(dialog, Window.GetWindow(this));
            dialog.ShowDialog();
        }
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        App.Services.GetRequiredService<INavigationService>().OpenUrl(e.Uri.AbsoluteUri);
        e.Handled = true;
    }
}
