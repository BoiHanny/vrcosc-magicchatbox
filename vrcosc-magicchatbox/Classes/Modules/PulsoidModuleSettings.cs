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

    private string _accessTokenOAuthEncrypted = string.Empty;
    private string _accessTokenOAuth = string.Empty;
    private bool _tokenProtectionFailed;

    /// <summary>
    /// True when DPAPI could not protect or unprotect the Pulsoid token on this machine/account.
    /// Never serialized: it describes this session's ability to use the credential, not the
    /// credential itself. When it is set, the stored ciphertext has deliberately been left alone
    /// rather than overwritten with nothing.
    /// </summary>
    [JsonIgnore]
    public bool TokenProtectionFailed
    {
        get => _tokenProtectionFailed;
        private set
        {
            if (_tokenProtectionFailed == value)
                return;

            _tokenProtectionFailed = value;
            OnPropertyChanged(nameof(TokenProtectionFailed));
        }
    }

    [JsonIgnore]
    public string AccessTokenOAuth
    {
        get => _accessTokenOAuth;
        set
        {
            string incoming = value ?? string.Empty;
            if (_accessTokenOAuth == incoming)
                return;

            if (incoming.Length == 0)
            {
                // An explicit clear is the user disconnecting: both halves go.
                _accessTokenOAuth = string.Empty;
                _accessTokenOAuthEncrypted = string.Empty;
                TokenProtectionFailed = false;
            }
            else
            {
                string plain = incoming;
                string cipher = null;
                bool encrypted = EncryptionMethods.TryProcessToken(ref plain, ref cipher, isEncryption: true);

                _accessTokenOAuth = incoming;

                if (encrypted && !string.IsNullOrEmpty(cipher))
                {
                    _accessTokenOAuthEncrypted = cipher;
                    TokenProtectionFailed = false;
                }
                else
                {
                    // Encryption failed. Keep the working plaintext for this session, but leave
                    // whatever ciphertext is already stored untouched — writing null here is what
                    // silently destroys a perfectly good saved token at the next debounced save.
                    TokenProtectionFailed = true;
                    Logging.WriteException(
                        new InvalidOperationException(
                            "Pulsoid access token could not be encrypted with DPAPI. The previously saved token was left untouched, and this one will not survive a restart."),
                        MSGBox: false);
                }
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
                TokenProtectionFailed = false;
            }
            else
            {
                string cipher = incoming;
                string plain = null;
                bool decrypted = EncryptionMethods.TryProcessToken(ref cipher, ref plain, isEncryption: false);

                if (decrypted && !string.IsNullOrEmpty(plain))
                {
                    _accessTokenOAuth = plain;
                    TokenProtectionFailed = false;
                }
                else
                {
                    // Decryption failed (different Windows account, restored profile, corrupt blob).
                    // The ciphertext stays exactly as it is on disk — it may well decrypt elsewhere —
                    // but the failure is made visible instead of presenting a silently empty token.
                    _accessTokenOAuth = string.Empty;
                    TokenProtectionFailed = true;
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

}
