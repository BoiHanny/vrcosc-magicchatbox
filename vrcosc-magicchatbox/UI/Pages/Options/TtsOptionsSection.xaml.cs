using System.Windows.Controls;
using System.Windows.Input;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.Sections;

namespace vrcosc_magicchatbox.UI.Pages.Options;

/// <summary>Code-behind for the text-to-speech options settings section.</summary>
public partial class TtsOptionsSection : UserControl
{
    private TtsSectionViewModel VM => (TtsSectionViewModel)DataContext;

    public TtsOptionsSection()
    {
        InitializeComponent();
    }

    private void LearnMoreAboutTTSbtn_MouseUp(object sender, MouseButtonEventArgs e)
        => VM.LearnMoreTtsCommand.Execute(null);

    private void PlaybackOutputDeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => VM?.OnPlaybackDeviceSelected();

    public void SelectTTSOutput()
    {
        foreach (var device in PlaybackOutputDeviceComboBox.Items)
        {
            if (device is AudioDevice audioDevice &&
                audioDevice.FriendlyName ==
                VM?.TtsAudio?.SelectedPlaybackOutputDevice?.FriendlyName)
            {
                PlaybackOutputDeviceComboBox.SelectedItem = device;
                break;
            }
        }
    }
}
