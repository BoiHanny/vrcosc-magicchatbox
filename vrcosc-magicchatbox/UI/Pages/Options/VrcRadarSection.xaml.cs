using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;

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
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
