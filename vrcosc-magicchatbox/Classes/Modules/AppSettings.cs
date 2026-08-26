using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Updates;

namespace vrcosc_magicchatbox.Classes.Modules;

public partial class AppSettings : VersionedSettings
{
    public const double OscTickIntervalDefaultSeconds = 1.0;
    public const double OscTickIntervalMinSeconds = 0.7;
    public const double OscTickIntervalMaxSeconds = 10.0;
    public const int OscTickIntervalDecimals = 1;

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

    [ObservableProperty] private bool _startWithSteamVr = false;
    [ObservableProperty] private bool _quitWithSteamVr = false;
    [ObservableProperty] private string _steamVrManifestPath = string.Empty;

    [ObservableProperty] private bool _countOculusSystemAsVR = true;
    [ObservableProperty] private bool _topmost = false;
    [ObservableProperty] private UpdateChannelMode _stableUpdateMode = UpdateChannelMode.Auto;
    [ObservableProperty] private UpdateChannelMode _preReleaseUpdateMode = UpdateChannelMode.Off;
    [ObservableProperty] private bool _checkUpdateOnStartup = true;

    [JsonProperty("JoinedAlphaChannel", NullValueHandling = NullValueHandling.Ignore)]
    public bool? LegacyJoinedAlphaChannel
    {
        get => null;
        set
        {
            if (value == true)
                PreReleaseUpdateMode = UpdateChannelMode.Notify;
        }
    }

    [ObservableProperty] private bool _startInBackground = false;
    [ObservableProperty] private bool _minimizeToTray = false;
    [ObservableProperty] private bool _closeToTray = false;
    [ObservableProperty] private bool _minimizeToTrayOnMinimize = false;
    [ObservableProperty] private bool _enableTrayNotifications = true;
    [ObservableProperty] private bool _showTrayRunningReminder = true;
    [ObservableProperty] private bool _openTrayWithAltX = true;
    [ObservableProperty] private bool _showHiddenIntegrationWarning = true;

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

    [ObservableProperty] private Dictionary<string, double> _pageScrollOffsets = new();

    /// <summary>
    /// Last measured height of each Options section, so an unrealized section's placeholder can reserve
    /// the right space. Without it the scrollbar and any restored offset are wrong until everything is built.
    /// </summary>
    [ObservableProperty] private Dictionary<string, double> _optionsSectionHeights = new();

    /// <summary>
    /// Arms the auto-save for the two scroll dictionaries. They are mutated in place, which raises no
    /// PropertyChanged of its own, so without this they would only reach disk on a clean shutdown.
    /// </summary>
    public void MarkScrollStateChanged()
    {
        OnPropertyChanged(nameof(PageScrollOffsets));
        OnPropertyChanged(nameof(OptionsSectionHeights));
    }

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
    [ObservableProperty] private bool _settings_VrPerformance = false;
    [ObservableProperty] private bool _settings_Lyrics = false;
    [ObservableProperty] private bool _settings_Voicemod = false;

    [ObservableProperty] private bool _settings_Privacy = false;

    [ObservableProperty] private double _windowLeft = double.NaN;
    [ObservableProperty] private double _windowTop = double.NaN;
    [ObservableProperty] private double _windowWidth = double.NaN;
    [ObservableProperty] private double _windowHeight = double.NaN;
    [ObservableProperty] private bool _windowMaximized = false;

    [ObservableProperty] private bool _settingsDev = false;
    [ObservableProperty] private bool _avatarSyncExecute = true;

    [ObservableProperty] private double _appOpacity = 0.98;
    [ObservableProperty] private bool _appIsEnabled = true;

    /// <summary>Drops shadows, blurs, partial opacity and page transitions, to give the GPU back.</summary>
    [ObservableProperty] private bool _reducedVisuals = false;

    /// <summary>Turns the above on by itself while VR is running, which is when the GPU is contended.</summary>
    [ObservableProperty] private bool _reducedVisualsInVr = true;

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
        {
            ScanningInterval = OscTickIntervalMinSeconds;
            return;
        }

        if (value > OscTickIntervalMaxSeconds)
        {
            ScanningInterval = OscTickIntervalMaxSeconds;
            return;
        }

        // The slider snaps to minimum plus a whole number of steps, and computing that in binary
        // floating point drifts: a tenth of a second is not representable, so the tick a user picks
        // arrives here as 9.799999999999999 and is stored and displayed that way.
        double snapped = Math.Round(value, OscTickIntervalDecimals, MidpointRounding.AwayFromZero);
        if (snapped != value)
            ScanningInterval = snapped;
    }

    partial void OnMinimizeToTrayChanged(bool value)
    {
        if (!value)
            return;

        CloseToTray = true;
        MinimizeToTrayOnMinimize = true;
    }
}
