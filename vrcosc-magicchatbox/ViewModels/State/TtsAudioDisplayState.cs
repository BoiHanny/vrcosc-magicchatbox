using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.ViewModels.Models;

namespace vrcosc_magicchatbox.ViewModels.State;

public sealed partial class TtsAudioDisplayState : ObservableObject
{
    [ObservableProperty]
    private string _toggleVoiceText = "Toggle voice";

    public TtsAudioDisplayState(ISettingsProvider<TtsSettings> ttsSettingsProvider)
    {
        TtsSettings settings = ttsSettingsProvider.Value;
        UpdateToggleVoiceText(settings.ToggleVoiceWithV);

        settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TtsSettings.ToggleVoiceWithV))
                UpdateToggleVoiceText(settings.ToggleVoiceWithV);
        };
    }

    private bool _TTSBtnShadow = false;
    public bool TTSBtnShadow
    {
        get => _TTSBtnShadow;
        set { _TTSBtnShadow = value; OnPropertyChanged(); }
    }

    private List<Voice>? _tikTokTTSVoices = new();
    public List<Voice>? TikTokTTSVoices
    {
        get => _tikTokTTSVoices;
        set { _tikTokTTSVoices = value; OnPropertyChanged(); }
    }

    private Voice? _selectedTikTokTTSVoice;
    public Voice? SelectedTikTokTTSVoice
    {
        get => _selectedTikTokTTSVoice;
        set
        {
            if (value == null)
                return;

            _selectedTikTokTTSVoice = value;
            OnPropertyChanged();
        }
    }

    private List<AudioDevice> _auxOutputDevices = new();
    public List<AudioDevice> AuxOutputDevices
    {
        get => _auxOutputDevices;
        set { _auxOutputDevices = value; OnPropertyChanged(); }
    }

    private List<AudioDevice> _playbackOutputDevices = new();
    public List<AudioDevice> PlaybackOutputDevices
    {
        get => _playbackOutputDevices;
        set { _playbackOutputDevices = value; OnPropertyChanged(); }
    }

    private AudioDevice? _selectedAuxOutputDevice;
    public AudioDevice? SelectedAuxOutputDevice
    {
        get => _selectedAuxOutputDevice;
        set { _selectedAuxOutputDevice = value; OnPropertyChanged(); }
    }

    private AudioDevice? _selectedPlaybackOutputDevice;
    public AudioDevice? SelectedPlaybackOutputDevice
    {
        get => _selectedPlaybackOutputDevice;
        set
        {
            if (value == null)
                return;

            _selectedPlaybackOutputDevice = value;
            OnPropertyChanged();
        }
    }

    public void UpdateToggleVoiceText(bool toggleVoiceWithV)
    {
        ToggleVoiceText = toggleVoiceWithV ? "Toggle voice (V)" : "Toggle voice";
    }
}
