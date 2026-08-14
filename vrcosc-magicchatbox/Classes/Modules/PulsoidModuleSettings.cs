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
    private bool _tokenEncryptionFailed;
    private bool _storedTokenUnreadable;

    /// <summary>
    /// True when DPAPI refused to <em>protect</em> a token we are holding in memory. The credential
    /// itself is fine and heart rate works for the rest of this session; it simply will not survive
    /// a restart. Never serialized: it describes this session, not the credential.
    /// This is deliberately separate from <see cref="StoredTokenUnreadable"/> — conflating the two
    /// meant an encrypt failure silently disabled heart rate and blamed decryption for it.
    /// </summary>
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

    /// <summary>
    /// True when a ciphertext exists on disk but DPAPI could not <em>unprotect</em> it on this
    /// Windows account, so there is nothing usable in memory at all. Never serialized. While it is
    /// set the stored ciphertext has deliberately been left alone rather than overwritten with
    /// nothing — it may well decrypt on the account it came from.
    /// </summary>
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
                // An explicit clear is the user disconnecting: both halves go, unconditionally.
                // Guarding on the plaintext alone made this a silent no-op after a failed decrypt,
                // where the plaintext is already empty but the ciphertext on disk is not.
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
                // Encryption failed. Keep the working plaintext for this session — heart rate is
                // perfectly usable — but do not leave a *different* credential sitting in the
                // ciphertext: the flag is not persisted, so the next launch would decrypt the old
                // blob cleanly and silently sign the user back in as the superseded token.
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
                    // Decryption failed (different Windows account, restored profile, corrupt blob).
                    // The ciphertext stays exactly as it is on disk — it may well decrypt elsewhere —
                    // but the failure is made visible instead of presenting a silently empty token.
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

    /// <summary>
    /// Forgets the Pulsoid credential completely: plaintext, ciphertext and both protection flags.
    /// This is what Disconnect must call. Assigning <see cref="string.Empty"/> to
    /// <see cref="AccessTokenOAuth"/> routes here for the same reason, but going through an
    /// explicit method makes it obvious that clearing is unconditional and never value-guarded.
    /// </summary>
    public void ClearStoredToken()
    {
        _accessTokenOAuth = string.Empty;
        _accessTokenOAuthEncrypted = string.Empty;
        TokenEncryptionFailed = false;
        StoredTokenUnreadable = false;

        // Raised unconditionally: the settings provider only writes to disk when it hears a
        // change, and a clear that stays in memory is exactly the bug this method exists to fix.
        OnPropertyChanged(nameof(AccessTokenOAuth));
        OnPropertyChanged(nameof(AccessTokenOAuthEncrypted));
    }

    /// <summary>True when the ciphertext currently on disk decrypts to exactly this plaintext.</summary>
    private bool StoredCipherDecryptsTo(string plaintext)
        => TryUnprotectToken(_accessTokenOAuthEncrypted, out string plain)
           && string.Equals(plain, plaintext, StringComparison.Ordinal);

    /// <summary>
    /// DPAPI protect, isolated behind a seam because it cannot be made to fail on demand on a
    /// healthy machine, and the behaviour on failure is the whole point of the encrypt/unreadable
    /// split. Tests override it; nothing else should.
    /// </summary>
    protected virtual bool TryProtectToken(string plaintext, out string ciphertext)
    {
        string source = plaintext;
        string destination = null;
        bool ok = EncryptionMethods.TryProcessToken(ref source, ref destination, isEncryption: true);
        ciphertext = destination;
        return ok;
    }

    /// <summary>DPAPI unprotect. See <see cref="TryProtectToken"/> for why this is virtual.</summary>
    protected virtual bool TryUnprotectToken(string ciphertext, out string plaintext)
    {
        string source = ciphertext;
        string destination = null;
        bool ok = EncryptionMethods.TryProcessToken(ref source, ref destination, isEncryption: false);
        plaintext = destination;
        return ok;
    }
}
