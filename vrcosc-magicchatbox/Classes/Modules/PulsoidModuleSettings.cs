using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Classes.Modules;

public partial class PulsoidModuleSettings : VersionedSettings
{
    [ObservableProperty]
    private bool applyHeartRateAdjustment = false;

    [ObservableProperty]
    private bool throttleHR = false;

    [ObservableProperty]
    private int throttleMaxAdditional = 10;

    [ObservableProperty]
    private int throttleHRMax = 105;

    partial void OnThrottleHRMaxChanged(int value)
    {
        if (value < 40) ThrottleHRMax = 40;
        else if (value > 199) ThrottleHRMax = 199;
    }

    [ObservableProperty]
    [property: JsonIgnore]
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
    [property: JsonIgnore]
    private string heartRateIcon = "❤️";

    [ObservableProperty]
    private int heartRateScanInterval = 1;

    [ObservableProperty]
    private bool heartRateTitle = false;

    [ObservableProperty]
    [property: JsonIgnore]
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

    private string _accessTokenOAuthEncrypted = string.Empty;
    private string _accessTokenOAuth = string.Empty;
    private bool _tokenEncryptionFailed;
    private bool _storedTokenUnreadable;

    [JsonIgnore]
    public bool TokenEncryptionFailed
    {
        get => _tokenEncryptionFailed;
        private set
        {
            if (_tokenEncryptionFailed == value)
                return;

            _tokenEncryptionFailed = value;
            OnPropertyChanged(nameof(TokenEncryptionFailed));
        }
    }

    [JsonIgnore]
    public bool StoredTokenUnreadable
    {
        get => _storedTokenUnreadable;
        private set
        {
            if (_storedTokenUnreadable == value)
                return;

            _storedTokenUnreadable = value;
            OnPropertyChanged(nameof(StoredTokenUnreadable));
        }
    }

    [JsonIgnore]
    public string AccessTokenOAuth
    {
        get => _accessTokenOAuth;
        set
        {
            string incoming = value ?? string.Empty;

            if (incoming.Length == 0)
            {
                ClearStoredToken();
                return;
            }

            if (_accessTokenOAuth == incoming && !_storedTokenUnreadable && !_tokenEncryptionFailed)
                return;

            bool encrypted = TryProtectToken(incoming, out string cipher);

            _accessTokenOAuth = incoming;
            StoredTokenUnreadable = false;

            if (encrypted && !string.IsNullOrEmpty(cipher))
            {
                _accessTokenOAuthEncrypted = cipher;
                TokenEncryptionFailed = false;
            }
            else
            {
                TokenEncryptionFailed = true;
                if (_accessTokenOAuthEncrypted.Length > 0 && !StoredCipherDecryptsTo(incoming))
                    _accessTokenOAuthEncrypted = string.Empty;

                Logging.WriteException(
                    new InvalidOperationException(
                        "Pulsoid access token could not be encrypted with DPAPI. Heart rate works for this session, but the token will not survive a restart."),
                    MSGBox: false);
            }

            OnPropertyChanged(nameof(AccessTokenOAuth));
            OnPropertyChanged(nameof(AccessTokenOAuthEncrypted));
        }
    }

    public string AccessTokenOAuthEncrypted
    {
        get => _accessTokenOAuthEncrypted;
        set
        {
            string incoming = value ?? string.Empty;
            if (_accessTokenOAuthEncrypted == incoming)
                return;

            _accessTokenOAuthEncrypted = incoming;

            if (incoming.Length == 0)
            {
                _accessTokenOAuth = string.Empty;
                TokenEncryptionFailed = false;
                StoredTokenUnreadable = false;
            }
            else
            {
                bool decrypted = TryUnprotectToken(incoming, out string plain);

                if (decrypted && !string.IsNullOrEmpty(plain))
                {
                    _accessTokenOAuth = plain;
                    TokenEncryptionFailed = false;
                    StoredTokenUnreadable = false;
                }
                else
                {
                    _accessTokenOAuth = string.Empty;
                    TokenEncryptionFailed = false;
                    StoredTokenUnreadable = true;
                    Logging.WriteException(
                        new InvalidOperationException(
                            "Stored Pulsoid access token could not be decrypted with DPAPI. The encrypted value has been kept on disk; the user must reconnect to use heart rate on this account."),
                        MSGBox: false);
                }
            }

            OnPropertyChanged(nameof(AccessTokenOAuthEncrypted));
            OnPropertyChanged(nameof(AccessTokenOAuth));
        }
    }

    public void ClearStoredToken()
    {
        _accessTokenOAuth = string.Empty;
        _accessTokenOAuthEncrypted = string.Empty;
        TokenEncryptionFailed = false;
        StoredTokenUnreadable = false;

        OnPropertyChanged(nameof(AccessTokenOAuth));
        OnPropertyChanged(nameof(AccessTokenOAuthEncrypted));
    }

    private bool StoredCipherDecryptsTo(string plaintext)
        => TryUnprotectToken(_accessTokenOAuthEncrypted, out string plain)
           && string.Equals(plain, plaintext, StringComparison.Ordinal);

    protected virtual bool TryProtectToken(string plaintext, out string ciphertext)
    {
        string source = plaintext;
        string destination = string.Empty;
        bool ok = EncryptionMethods.TryProcessToken(ref source, ref destination, isEncryption: true);
        ciphertext = destination;
        return ok;
    }

    protected virtual bool TryUnprotectToken(string ciphertext, out string plaintext)
    {
        string source = ciphertext;
        string destination = string.Empty;
        bool ok = EncryptionMethods.TryProcessToken(ref source, ref destination, isEncryption: false);
        plaintext = destination;
        return ok;
    }
}
