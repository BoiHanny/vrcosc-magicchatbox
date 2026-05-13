using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using System.Windows.Navigation;
using vrcosc_magicchatbox.Services;

namespace vrcosc_magicchatbox.UI.Pages.Options;

/// <summary>Code-behind for the VRChat Radar (log parser) settings section.</summary>
public partial class VrcRadarSection : UserControl
{
    public VrcRadarSection()
    {
        InitializeComponent();
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        App.Services.GetRequiredService<INavigationService>().OpenUrl(e.Uri.AbsoluteUri);
        e.Handled = true;
    }
}
