using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Application-wide persisted settings.
/// </summary>
public partial class AppSettings : VersionedSettings
{
    public const double OscTickIntervalDefaultSeconds = 1.0;
    public const double OscTickIntervalMinSeconds = 0.7;
    public const double OscTickIntervalMaxSeconds = 10.0;

    [ObservableProperty] private double _scanningInterval = OscTickIntervalDefaultSeconds;
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
    [ObservableProperty] private bool _startInBackground = false;
    [ObservableProperty] private bool _minimizeToTray = false;
    [ObservableProperty] private bool _closeToTray = false;
    [ObservableProperty] private bool _minimizeToTrayOnMinimize = false;
    [ObservableProperty] private bool _enableTrayNotifications = true;
    [ObservableProperty] private bool _showTrayRunningReminder = true;
    [ObservableProperty] private bool _openTrayWithAltX = true;

    [JsonProperty("OpenTrayWithAltQ", NullValueHandling = NullValueHandling.Ignore)]
    public bool? LegacyOpenTrayShortcut
    {
        get => null;
        set
        {
            if (value.HasValue)
                OpenTrayWithAltX = value.Value;
        }
    }

    [ObservableProperty] private int _switchStatusInterval = 5;
    [ObservableProperty] private string _eggPrefixIconStatus = "🥚";
    [ObservableProperty] private bool _isRandomCycling = false;
    [ObservableProperty] private bool _cycleStatus = false;
    [ObservableProperty] private bool _cycleOverrideCurrentGroup = false;
    [ObservableProperty] private string _cycleOverrideGroupId = "";
    [ObservableProperty] private string _lastSelectedGroupId = "";
    [ObservableProperty] private bool _blankEgg = false;

    [ObservableProperty] private bool _statusRoundCorners = true;

    [ObservableProperty] private int _currentMenuItem = 0;

    [ObservableProperty] private bool _settings_Status = false;
    [ObservableProperty] private bool _settings_OpenAI = false;
    [ObservableProperty] private bool _settings_HeartRate = false;
    [ObservableProperty] private bool _settings_Time = false;
    [ObservableProperty] private bool _settings_Weather = false;
    [ObservableProperty] private bool _settings_Twitch = false;
    [ObservableProperty] private bool _settings_TikTokLive = false;
    [ObservableProperty] private bool _settings_Discord = false;
    [ObservableProperty] private bool _settings_Spotify = false;
    [ObservableProperty] private bool _settings_ComponentStats = false;
    [ObservableProperty] private bool _settings_NetworkStatistics = false;
    [ObservableProperty] private bool _settings_Chatting = false;
    [ObservableProperty] private bool _settings_TTS = false;
    [ObservableProperty] private bool _settings_MediaLink = false;
    [ObservableProperty] private bool _settings_AppOptions = false;
    [ObservableProperty] private bool _settings_WindowActivity = false;
    [ObservableProperty] private bool _settings_VrcRadar = false;
    [ObservableProperty] private bool _settings_TrackerBattery = false;

    [ObservableProperty] private bool _settingsDev = false;
    [ObservableProperty] private bool _avatarSyncExecute = true;

    [ObservableProperty] private double _appOpacity = 0.98;
    [ObservableProperty] private bool _appIsEnabled = true;

    [ObservableProperty]
    [property: Newtonsoft.Json.JsonIgnore]
    [property: System.Text.Json.Serialization.JsonIgnore]
    private int _profileNumber;

    [ObservableProperty]
    [property: Newtonsoft.Json.JsonIgnore]
    [property: System.Text.Json.Serialization.JsonIgnore]
    private bool _useCustomProfile;

    [ObservableProperty] private string _acceptedTosVersion = string.Empty;

    partial void OnScanningIntervalChanged(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            ScanningInterval = OscTickIntervalDefaultSeconds;
            return;
        }

        if (value < OscTickIntervalMinSeconds)
            ScanningInterval = OscTickIntervalMinSeconds;
        else if (value > OscTickIntervalMaxSeconds)
            ScanningInterval = OscTickIntervalMaxSeconds;
    }

    partial void OnMinimizeToTrayChanged(bool value)
    {
        if (!value)
            return;

        CloseToTray = true;
        MinimizeToTrayOnMinimize = true;
    }
}
