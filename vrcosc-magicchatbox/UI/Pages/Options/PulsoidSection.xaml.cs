using System.Windows;
using System.Windows.Controls;
using vrcosc_magicchatbox.UI.Dialogs;
using vrcosc_magicchatbox.ViewModels.Sections;

namespace vrcosc_magicchatbox.UI.Pages.Options;

/// <summary>Code-behind for the Pulsoid heart-rate integration settings section.</summary>
public partial class PulsoidSection : UserControl
{
    public PulsoidSection()
    {
        InitializeComponent();
    }

    private void ManualPulsoidAuthBtn_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is PulsoidSectionViewModel vm)
        {
            var dialog = new ManualPulsoidAuth(
                vm.Modules.Pulsoid,
                auth => vm.PulsoidAuthConnected = auth,
                vm.PulsoidOAuth,
                vm.Navigation);
            dialog.Owner = Window.GetWindow(this);
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            dialog.ShowDialog();
        }
    }

    private void LearnMoreAboutHeartbtn_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is PulsoidSectionViewModel vm)
            vm.LearnMoreHeartRateCommand.Execute(null);
    }
}
