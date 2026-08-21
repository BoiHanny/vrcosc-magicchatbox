using System;
using System.Windows;
using System.Windows.Controls;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels.Sections;

namespace vrcosc_magicchatbox.UI.Pages.Options;

public partial class VoicemodSection : UserControl
{
    public VoicemodSection()
    {
        InitializeComponent();
    }

    private async void SaveLocalClientKey_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button saveButton || DataContext is not VoicemodSectionViewModel viewModel)
            return;

        saveButton.IsEnabled = false;
        try
        {
            bool saved = await viewModel.SaveLocalClientKeyAsync(VoicemodClientKeyInput.Password);
            if (saved)
                VoicemodClientKeyInput.Clear();
        }
        catch (Exception exception)
        {
            Logging.WriteException(exception, MSGBox: false);
        }
        finally
        {
            saveButton.IsEnabled = true;
        }
    }
}
