using System.Windows.Controls;
using vrcosc_magicchatbox.ViewModels.Sections;

namespace vrcosc_magicchatbox.UI.Pages.Options;

public partial class MediaLinkSection : UserControl
{
    public MediaLinkSection()
    {
        InitializeComponent();
    }

    private void LearnMoreAboutSpotifybtn_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is MediaLinkSectionViewModel vm)
            vm.LearnMoreMediaLinkCommand.Execute(null);
    }
}
