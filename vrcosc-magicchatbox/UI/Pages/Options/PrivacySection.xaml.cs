using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using System.Windows.Navigation;
using vrcosc_magicchatbox.Services;

namespace vrcosc_magicchatbox.UI.Pages.Options;

public partial class PrivacySection : UserControl
{
    public PrivacySection()
    {
        InitializeComponent();
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        App.Services.GetRequiredService<INavigationService>().OpenUrl(e.Uri.AbsoluteUri);
        e.Handled = true;
    }
}
