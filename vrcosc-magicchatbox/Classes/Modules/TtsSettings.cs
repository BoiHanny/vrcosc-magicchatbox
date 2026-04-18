using CommunityToolkit.Mvvm.ComponentModel;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Settings for the TTS module, including TikTok voice selection, volume, and playback options.
/// </summary>
public partial class TtsSettings : VersionedSettings
{
    [ObservableProperty] private bool _ttsTikTokEnabled = false;
    [ObservableProperty] private bool _ttsCutOff = true;
    [ObservableProperty] private bool _autoUnmuteTTS = true;
    [ObservableProperty] private bool _toggleVoiceWithV = true;
    [ObservableProperty] private float _ttsVolume = 0.2f;
    [ObservableProperty] private string _recentTikTokTTSVoice = string.Empty;
    [ObservableProperty] private string _recentPlayBackOutput = string.Empty;
    [ObservableProperty] private bool _ttsOnResendChat = false;
}
