using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace vrcosc_magicchatbox.UI.Pages.Options;

/// <summary>Code-behind for the Discord voice integration settings section.</summary>
public partial class DiscordSection : UserControl
{
    public DiscordSection()
    {
        InitializeComponent();
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
