using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Holds user-specific settings for the Pulsoid module and provides serialization to JSON.
/// </summary>
public partial class PulsoidModuleSettings : VersionedSettings
{
    private const string SettingsFileName = "PulsoidModuleSettings.json";

    [ObservableProperty]
    private bool applyHeartRateAdjustment = false;

    [ObservableProperty]
    private bool throttleHR = false;

    [ObservableProperty]
    private int throttleMaxAdditional = 10;

    [ObservableProperty]
    private int throttleHRMax = 105;

    [ObservableProperty]
    private int currentHeartIconIndex = 0;

    [ObservableProperty]
    private string currentHeartRateTitle = "Heart Rate";

    [ObservableProperty]
    private bool disableLegacySupport = false;

    [ObservableProperty]
    private bool enableHeartRateOfflineCheck = true;

    [ObservableProperty]
    [property: JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    private List<string> heartIcons = new List<string> { "❤️", "💖", "💗", "💙", "💚", "💛", "💜" };

    [ObservableProperty]
    private int heartRateAdjustment = -5;

    [ObservableProperty]
    private string heartRateIcon = "❤️";

    [ObservableProperty]
    private int heartRateScanInterval = 1;

    [ObservableProperty]
    private bool heartRateTitle = false;

    [ObservableProperty]
    private string heartRateTrendIndicator = string.Empty;

    [ObservableProperty]
    private int heartRateTrendIndicatorSampleRate = 4;

    [ObservableProperty]
    private double heartRateTrendIndicatorSensitivity = 0.65;

    [ObservableProperty]
    private bool hideCurrentHeartRate = false;

    [ObservableProperty]
    private string highHeartRateText = "hot";

    [ObservableProperty]
    private int highTemperatureThreshold = 100;

    [ObservableProperty]
    private string lowHeartRateText = "sleepy";

    [ObservableProperty]
    private int lowTemperatureThreshold = 60;

    [ObservableProperty]
    private bool magicHeartIconPrefix = true;

    [ObservableProperty]
    private bool magicHeartRateIcons = true;

    [ObservableProperty]
    bool pulsoidStatsEnabled = true;

    [ObservableProperty]
    private List<PulsoidTrendSymbolSet> pulsoidTrendSymbols = new();

    [ObservableProperty]
    private PulsoidTrendSymbolSet selectedPulsoidTrendSymbol = new();

    [ObservableProperty]
    private StatisticsTimeRange selectedStatisticsTimeRange = StatisticsTimeRange._24h;

    [ObservableProperty]
    private bool sentMCBHeartrateInfo = false;

    [ObservableProperty]
    private bool sentMCBHeartrateInfoLegacy = false;

    [ObservableProperty]
    private bool separateTitleWithEnter = false;

    [ObservableProperty]
    bool showAverageHeartRate = true;

    [ObservableProperty]
    private bool showBPMSuffix = false;

    [ObservableProperty]
    bool showCalories = false;

    [ObservableProperty]
    bool showDuration = false;

    [ObservableProperty]
    private bool showHeartRateTrendIndicator = true;

    [ObservableProperty]
    bool showMaximumHeartRate = true;

    [ObservableProperty]
    bool showMinimumHeartRate = true;

    [ObservableProperty]
    bool showStatsTimeRange = false;

    [ObservableProperty]
    private bool showTemperatureText = true;

    [ObservableProperty]
    private bool smoothHeartRate = true;

    [ObservableProperty]
    private int smoothHeartRateTimeSpan = 4;

    [ObservableProperty]
    private bool smoothOSCHeartRate = true;

    [ObservableProperty]
    private int smoothOSCHeartRateTimeSpan = 4;

    [ObservableProperty]
    private List<StatisticsTimeRange> statisticsTimeRanges = new();

    [ObservableProperty]
    bool trendIndicatorBehindStats = true;

    [ObservableProperty]
    private int unchangedHeartRateTimeoutInSec = 30;

    // Encrypted OAuth access token
    private string _accessTokenOAuthEncrypted = string.Empty;
    private string _accessTokenOAuth = string.Empty;

    [JsonIgnore]
    public string AccessTokenOAuth
    {
        get => _accessTokenOAuth;
        set
        {
            if (_accessTokenOAuth != value)
            {
                _accessTokenOAuth = value ?? string.Empty;
                EncryptionMethods.TryProcessToken(ref _accessTokenOAuth, ref _accessTokenOAuthEncrypted, true);
                OnPropertyChanged(nameof(AccessTokenOAuth));
                OnPropertyChanged(nameof(AccessTokenOAuthEncrypted));
            }
        }
    }

    public string AccessTokenOAuthEncrypted
    {
        get => _accessTokenOAuthEncrypted;
        set
        {
            if (_accessTokenOAuthEncrypted != value)
            {
                _accessTokenOAuthEncrypted = value ?? string.Empty;
                EncryptionMethods.TryProcessToken(ref _accessTokenOAuthEncrypted, ref _accessTokenOAuth, false);
                OnPropertyChanged(nameof(AccessTokenOAuthEncrypted));
                OnPropertyChanged(nameof(AccessTokenOAuth));
            }
        }
    }

    internal string _fullSettingsPath;

    /// <summary>
    /// Load settings from disk. If no file or corrupted, returns a new instance.
    /// </summary>
    public static PulsoidModuleSettings LoadSettings(string settingsPath)
    {
        if (File.Exists(settingsPath))
        {
            string settingsJson = File.ReadAllText(settingsPath);

            if (string.IsNullOrWhiteSpace(settingsJson) || settingsJson.All(c => c == '\0'))
            {
                Logging.WriteInfo("The settings JSON file is empty or corrupted.");
                return new PulsoidModuleSettings { _fullSettingsPath = settingsPath };
            }

            try
            {
                var settings = JsonConvert.DeserializeObject<PulsoidModuleSettings>(settingsJson);
                if (settings != null) settings._fullSettingsPath = settingsPath;
                return settings ?? new PulsoidModuleSettings { _fullSettingsPath = settingsPath };
            }
            catch (JsonException ex)
            {
                Logging.WriteInfo($"Error parsing settings JSON: {ex.Message}");
                return new PulsoidModuleSettings { _fullSettingsPath = settingsPath };
            }
        }
        else
        {
            Logging.WriteInfo("Settings file does not exist, returning new settings instance.");
            return new PulsoidModuleSettings { _fullSettingsPath = settingsPath };
        }
    }

    /// <summary>
    /// Save current settings to disk as JSON.
    /// </summary>
    public void SaveSettings()
    {
        try
        {
            var settingsJson = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(_fullSettingsPath, settingsJson);
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"Error saving settings: {ex.Message}");
        }
    }
}
