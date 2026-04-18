using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Application-wide persisted settings.
/// </summary>
public partial class AppSettings : VersionedSettings
{
    [ObservableProperty] private double _scanningInterval = 5;
    [ObservableProperty] private int _scanPauseTimeout = 15;

    [ObservableProperty] private bool _prefixIconStatus = true;
    [ObservableProperty] private bool _prefixIconMusic = true;
    [ObservableProperty] private bool _prefixIconSoundpad = true;
    [ObservableProperty] private ObservableCollection<string> _emojiCollection = new();
    [ObservableProperty] private bool _enableEmojiShuffleInChats = false;
    [ObservableProperty] private bool _enableEmojiShuffle = false;

    [ObservableProperty] private string _oscMessagePrefix = string.Empty;
    [ObservableProperty] private string _oscMessageSeparator = " ┆ ";
    [ObservableProperty] private string _oscMessageSuffix = string.Empty;
    [ObservableProperty] private bool _seperateWithENTERS = true;

    [ObservableProperty] private bool _countOculusSystemAsVR = true;
    [ObservableProperty] private bool _topmost = false;
    [ObservableProperty] private bool _joinedAlphaChannel = false;
    [ObservableProperty] private bool _checkUpdateOnStartup = true;

    [ObservableProperty] private int _switchStatusInterval = 5;
    [ObservableProperty] private string _eggPrefixIconStatus = "🥚";
    [ObservableProperty] private bool _isRandomCycling = false;
    [ObservableProperty] private bool _cycleStatus = false;
    [ObservableProperty] private bool _blankEgg = false;

    [ObservableProperty] private int _currentMenuItem = 0;

    [ObservableProperty] private bool _settings_Status = false;
    [ObservableProperty] private bool _settings_OpenAI = false;
    [ObservableProperty] private bool _settings_HeartRate = false;
    [ObservableProperty] private bool _settings_Time = false;
    [ObservableProperty] private bool _settings_Weather = false;
    [ObservableProperty] private bool _settings_Twitch = false;
    [ObservableProperty] private bool _settings_ComponentStats = false;
    [ObservableProperty] private bool _settings_NetworkStatistics = false;
    [ObservableProperty] private bool _settings_Chatting = false;
    [ObservableProperty] private bool _settings_TTS = false;
    [ObservableProperty] private bool _settings_MediaLink = false;
    [ObservableProperty] private bool _settings_AppOptions = false;
    [ObservableProperty] private bool _settings_WindowActivity = false;
    [ObservableProperty] private bool _settings_TrackerBattery = false;

    // Developer settings (moved from ViewModel — these are persisted toggles, not runtime flags)
    [ObservableProperty] private bool _settingsDev = false;
    [ObservableProperty] private bool _avatarSyncExecute = true;

    [ObservableProperty] private double _appOpacity = 0.98;
    [ObservableProperty] private bool _appIsEnabled = true;

    [ObservableProperty] private int _profileNumber;
    [ObservableProperty] private bool _useCustomProfile;

    // TOS acceptance tracking — compared against Constants.TosVersion on startup
    [ObservableProperty] private string _acceptedTosVersion = string.Empty;
}
