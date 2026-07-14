using System.Windows.Controls;
using vrcosc_magicchatbox.ViewModels.Sections;

namespace vrcosc_magicchatbox.UI.Pages.Options;

/// <summary>Code-behind for the MediaLink settings section.</summary>
public partial class MediaLinkSection : UserControl
{
    public MediaLinkSection()
    {
        InitializeComponent();
    }

    // MouseUp doesn't support Command binding in WPF
    private void LearnMoreAboutSpotifybtn_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is MediaLinkSectionViewModel vm)
            vm.LearnMoreMediaLinkCommand.Execute(null);
    }
}
