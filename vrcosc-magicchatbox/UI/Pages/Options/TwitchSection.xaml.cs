using System.Windows;
using System.Windows.Controls;
using vrcosc_magicchatbox.UI.Dialogs;
using vrcosc_magicchatbox.ViewModels.Sections;

namespace vrcosc_magicchatbox.UI.Pages.Options;

/// <summary>Code-behind for the Twitch integration settings section.</summary>
public partial class TwitchSection : UserControl
{
    public TwitchSection()
    {
        InitializeComponent();
    }

    private void SetupTwitch_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is TwitchSectionViewModel vm)
        {
            var dialog = new TwitchAuth(vm.TwitchSettingsProvider, vm.Navigation);
            DialogWindowHelper.PrepareModal(dialog, Window.GetWindow(this));
            dialog.ShowDialog();
        }
    }
}
