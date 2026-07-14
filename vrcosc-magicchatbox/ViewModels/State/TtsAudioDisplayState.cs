using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using vrcosc_magicchatbox.ViewModels.Models;

namespace vrcosc_magicchatbox.ViewModels.State;

/// <summary>
/// Owns TTS voice lists, audio device selection, and TTS button state.
/// Extracted from ViewModel to isolate TTS/Audio runtime display concerns.
/// </summary>
public sealed partial class TtsAudioDisplayState : ObservableObject
{
    [ObservableProperty]
    private string _toggleVoiceText = "Toggle voice (V)";

    private bool _TTSBtnShadow = false;
    public bool TTSBtnShadow
    {
        get => _TTSBtnShadow;
        set { _TTSBtnShadow = value; OnPropertyChanged(); }
    }

    private List<Voice> _tikTokTTSVoices;
    public List<Voice> TikTokTTSVoices
    {
        get => _tikTokTTSVoices;
        set { _tikTokTTSVoices = value; OnPropertyChanged(); }
    }

    private Voice _selectedTikTokTTSVoice;
    public Voice SelectedTikTokTTSVoice
    {
        get => _selectedTikTokTTSVoice;
        set { _selectedTikTokTTSVoice = value; OnPropertyChanged(); }
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

    private AudioDevice _selectedAuxOutputDevice;
    public AudioDevice SelectedAuxOutputDevice
    {
        get => _selectedAuxOutputDevice;
        set { _selectedAuxOutputDevice = value; OnPropertyChanged(); }
    }

    private AudioDevice _selectedPlaybackOutputDevice;
    public AudioDevice SelectedPlaybackOutputDevice
    {
        get => _selectedPlaybackOutputDevice;
        set { _selectedPlaybackOutputDevice = value; OnPropertyChanged(); }
    }

    public void UpdateToggleVoiceText(bool toggleVoiceWithV)
    {
        ToggleVoiceText = toggleVoiceWithV ? "Toggle voice (V)" : "Toggle voice";
    }
}
