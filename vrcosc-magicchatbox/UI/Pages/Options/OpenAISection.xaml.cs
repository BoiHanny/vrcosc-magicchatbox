using System.Windows;
using System.Windows.Controls;
using vrcosc_magicchatbox.UI.Dialogs;
using vrcosc_magicchatbox.ViewModels.Sections;

namespace vrcosc_magicchatbox.UI.Pages.Options;

/// <summary>Code-behind for the OpenAI integration settings section.</summary>
public partial class OpenAISection : UserControl
{
    public OpenAISection()
    {
        InitializeComponent();
    }

    private void ConnectWithOpenAI_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is OpenAISectionViewModel vm)
        {
            var dialog = new OpenAIAuth(vm.OpenAI, vm.OpenAISettingsProvider, vm.OpenAIModuleInstance, vm.Navigation);
            DialogWindowHelper.PrepareModal(dialog, Window.GetWindow(this));
            dialog.ShowDialog();
        }
    }

    private void LearnMoreAboutOpenAIbtn_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is OpenAISectionViewModel vm)
            vm.LearnMoreOpenAICommand.Execute(null);
    }

    private void MyUsageBtn_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is OpenAISectionViewModel vm)
            vm.OpenAIUsageCommand.Execute(null);
    }
}
