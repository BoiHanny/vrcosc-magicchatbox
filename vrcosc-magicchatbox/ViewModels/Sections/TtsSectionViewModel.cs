using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.ViewModels.Sections;

/// <summary>
/// Section ViewModel for TTS options.
/// Complete binding surface for TtsOptionsSection.xaml.
/// </summary>
public partial class TtsSectionViewModel : ObservableObject
{
    private readonly INavigationService _nav;

    public AppSettings AppSettings { get; }
    public TtsSettings TtsSettings { get; }
    public TtsAudioDisplayState TtsAudio { get; }

    /// <summary>
    /// Initializes the TTS section ViewModel with the TTS module, settings, app-state,
    /// and audio device service.
    /// </summary>
    public TtsSectionViewModel(
        ISettingsProvider<AppSettings> appSettingsProvider,
        ISettingsProvider<TtsSettings> ttsSettingsProvider,
        TtsAudioDisplayState ttsAudio,
        INavigationService nav)
    {
        AppSettings = appSettingsProvider.Value;
        TtsSettings = ttsSettingsProvider.Value;
        TtsAudio = ttsAudio;
        _nav = nav;
    }

    /// <summary>
    /// Called when the TTS voice ComboBox selection changes.
    /// </summary>
    public void OnTtsVoiceSelected(Voice? selectedVoice)
    {
        if (selectedVoice == null) return;
        TtsAudio.SelectedTikTokTTSVoice = selectedVoice;
        TtsSettings.RecentTikTokTTSVoice = selectedVoice.ApiName;
    }

    /// <summary>
    /// Called when playback output device selection changes.
    /// </summary>
    public void OnPlaybackDeviceSelected()
    {
        if (TtsAudio.SelectedPlaybackOutputDevice != null)
            TtsSettings.RecentPlayBackOutput = TtsAudio.SelectedPlaybackOutputDevice.FriendlyName;
    }

    [RelayCommand]
    private void LearnMoreTts()
        => _nav.OpenUrl(Core.Constants.WikiTtsAudioSetupUrl);
}
