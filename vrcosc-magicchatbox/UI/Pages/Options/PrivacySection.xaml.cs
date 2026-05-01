using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace vrcosc_magicchatbox.UI.Pages.Options;

public partial class PrivacySection : UserControl
{
    public PrivacySection()
    {
        InitializeComponent();
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
