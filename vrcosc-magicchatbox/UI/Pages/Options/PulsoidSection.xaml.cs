using System.Windows;
using System.Windows.Controls;
using vrcosc_magicchatbox.UI.Dialogs;
using vrcosc_magicchatbox.ViewModels.Sections;

namespace vrcosc_magicchatbox.UI.Pages.Options;

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
                state => vm.PulsoidAuthState = state,
                vm.PulsoidOAuth,
                vm.Navigation);
            DialogWindowHelper.PrepareModal(dialog, Window.GetWindow(this));
            dialog.ShowDialog();
        }
    }

    private void LearnMoreAboutHeartbtn_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is PulsoidSectionViewModel vm)
            vm.LearnMoreHeartRateCommand.Execute(null);
    }
}
