using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Utilities;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Classes.Modules;

public partial class PulsoidModule : ObservableObject, IModule
{
    private CancellationTokenSource? _cts;
    private bool _disposed;
    private readonly IAppState _appState;
    private readonly IUiDispatcher _dispatcher;
    private readonly IToastService? _toast;
    private volatile bool _pulsoidErrorShown;
    private volatile bool _statsErrorShown;
    private volatile bool _statisticsUnavailable;

    private readonly IOscSender _oscSender;
    private IOscSender OscSender => _oscSender;

    private readonly IntegrationSettings _integrationSettings;

    private readonly PulsoidOAuthHandler _oAuth;
    private PulsoidOAuthHandler OAuth => _oAuth;

    private readonly IPulsoidClient _client;

    private readonly Queue<int> _heartRateHistory = new();

    private readonly Queue<Tuple<DateTime, int>> _heartRates = new();
    private DateTime _lastStateChangeTime = DateTime.MinValue;
    private DateTime _lastMessageReceivedTime = DateTime.Now;
    private readonly TimeSpan _inactivityThreshold = TimeSpan.FromSeconds(15);
    private static readonly Random _random = new Random();

    private readonly Queue<int> _oscHeartRates = new();
    private readonly object _oscHeartRatesLock = new object();
    private int _isProcessing = 0;
    private DateTime _lastStatsFetchUtc = DateTime.MinValue;
    private DateTime _lastTokenValidationUtc = DateTime.MinValue;
    private DateTime _lastInactivityLogUtc = DateTime.MinValue;
    private static readonly TimeSpan _statsFetchInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _tokenValidationInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan _inactivityLogInterval = TimeSpan.FromSeconds(30);
    private int _previousHeartRate = -1;
    private System.Timers.Timer _processDataTimer;
    private readonly TimeSpan _stateChangeDebounce = TimeSpan.FromSeconds(2);
    private int _unchangedHeartRateCount = 0;

    private bool GotReadingThisInterval = false;

    [ObservableProperty]
    private int heartRate;

    private int HeartRateFromSocket = 0;

    [ObservableProperty]
    private DateTime heartRateLastUpdate = DateTime.Now;
    private bool isMonitoringStarted = false;

    private static readonly TimeSpan MonitorStopTimeout = TimeSpan.FromSeconds(3);

    private int _startInProgress;
    private Task? _monitorLoop;

    [ObservableProperty]
    private bool pulsoidAccessError = false;

    [ObservableProperty]
    private string pulsoidAccessErrorTxt = string.Empty;

    [ObservableProperty]
    private bool pulsoidDeviceOnline = false;
    public PulsoidStatisticsResponse? PulsoidStatistics;

    private readonly ISettingsProvider<PulsoidModuleSettings> _settingsProvider;
    public PulsoidModuleSettings Settings => _settingsProvider.Value;

    public string Name => "Pulsoid";
    public bool IsEnabled { get; set; } = true;
    public bool IsRunning => isMonitoringStarted;
    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task StartAsync(CancellationToken ct = default) => CheckMonitoringConditionsAsync();

    public async Task StopAsync(CancellationToken ct = default) { await StopMonitoringHeartRateAsync(); }

    public void SaveSettings() => _settingsProvider.FlushPendingSave();

    public PulsoidModule(IAppState appState, IPulsoidClient client, IUiDispatcher dispatcher, IOscSender oscSender, IntegrationSettings integrationSettings, PulsoidOAuthHandler oAuth, ISettingsProvider<PulsoidModuleSettings> settingsProvider, IToastService? toast = null)
    {
        _appState = appState;
        _client = client;
        _dispatcher = dispatcher;
        _oscSender = oscSender;
        _integrationSettings = integrationSettings;
        _oAuth = oAuth;
        _toast = toast;
        _settingsProvider = settingsProvider;
        RefreshTrendSymbols();
        RefreshTimeRanges();

        _client.HeartRateReceived += OnHeartRateReceived;
        _client.ConnectionFailed += OnConnectionFailed;
        _client.ConnectionStateChanged += OnConnectionStateChanged;

        Settings.PropertyChanged += PropertyChangedHandler;

        _processDataTimer = new System.Timers.Timer
        {
            AutoReset = true,
            Interval = 1000
        };
        _processDataTimer.Elapsed += (sender, e) =>
        {
            _dispatcher.BeginInvoke(() => _ = ProcessDataAsync());
        };

        RestoreAuthStateFromSettings();
    }

    public void RestoreAuthStateFromSettings()
    {
        if (Settings.StoredTokenUnreadable)
        {
            SetAuthState(PulsoidAuthState.Unreadable);
            PulsoidAccessError = true;
            PulsoidAccessErrorTxt = "The saved Pulsoid token could not be decrypted on this Windows account. Please reconnect.";
            Logging.WriteInfo("Pulsoid: stored token present but undecryptable; asking the user to reconnect.");
            return;
        }

        if (string.IsNullOrWhiteSpace(Settings.AccessTokenOAuth))
        {
            SetAuthState(PulsoidAuthState.NoToken);
            return;
        }

        SetAuthState(PulsoidAuthState.Unverified);
        PulsoidAccessError = false;
        PulsoidAccessErrorTxt = string.Empty;
        Logging.WriteInfo("Pulsoid: restored saved sign-in from settings (pending verification).");
    }

    private void OnHeartRateReceived(int rawHR)
    {
        HandleHeartRateMessage(rawHR);
    }

    private void OnConnectionFailed(PulsoidConnectionError error, string message)
    {
        if (error == PulsoidConnectionError.StatisticsUnavailable)
        {
            _statisticsUnavailable = true;
            Logging.WriteInfo($"Pulsoid statistics disabled for this session: {message}");
            _dispatcher.BeginInvoke(() => PulsoidStatistics = null);

            if (!_statsErrorShown)
            {
                _statsErrorShown = true;
                _toast?.Show("💓 Pulsoid", message, ToastType.Warning, key: "pulsoid-stats");
            }
            return;
        }

        if (!_pulsoidErrorShown)
        {
            _pulsoidErrorShown = true;
            _toast?.Show("💓 Pulsoid Error", message,
                error == PulsoidConnectionError.TokenInvalid ? ToastType.Error : ToastType.Warning,
                key: "pulsoid-error");
        }

        _dispatcher.BeginInvoke(() =>
        {
            PulsoidAccessError = true;
            PulsoidAccessErrorTxt = message;

            if (error == PulsoidConnectionError.TokenInvalid)
                SetAuthState(PulsoidAuthState.Rejected);
            else if (error != PulsoidConnectionError.SubscriptionRequired)
                MarkUnreachableIfSignedIn();
        });
    }

    private void OnConnectionStateChanged(bool connected)
    {
        if (connected)
        {
            _pulsoidErrorShown = false;
            _statsErrorShown = false;
            _statisticsUnavailable = false;
            _dispatcher.BeginInvoke(() =>
            {
                PulsoidAccessError = false;
                PulsoidAccessErrorTxt = "";
                SetAuthState(PulsoidAuthState.Authenticated);
            });
            _processDataTimer.Start();
        }
    }

    private void HandleHeartRateMessage(int rawHR)
    {
        if (rawHR <= 0) return;

        _lastMessageReceivedTime = DateTime.Now;

        if (Settings.ApplyHeartRateAdjustment)
        {
            rawHR += Settings.HeartRateAdjustment;
            rawHR = Math.Clamp(rawHR, 0, 255);
        }

        if (Settings.ThrottleHR)
        {
            rawHR = ApplyThrottle(rawHR);
        }

        HeartRateFromSocket = rawHR;

        _dispatcher.BeginInvoke(() => HeartRateLastUpdate = DateTime.Now);

        lock (_oscHeartRatesLock)
        {
            _oscHeartRates.Enqueue(rawHR);
            while (_oscHeartRates.Count > Settings.SmoothOSCHeartRateTimeSpan)
                _oscHeartRates.Dequeue();
        }

        GotReadingThisInterval = true;

        if (_integrationSettings.IntgrHeartRate_OSC)
        {
            SendHRToOSC(true);
        }
    }

    private static double CalculateSlope(Queue<int> values)
    {
        int count = values.Count;
        double avgX = (count - 1) / 2.0;
        double avgY = values.Average();

        double sumXY = 0;
        double sumXX = 0;

        for (int i = 0; i < count; i++)
        {
            sumXY += (i - avgX) * (values.ElementAt(i) - avgY);
            sumXX += Math.Pow(i - avgX, 2);
        }

        double slope = sumXY / sumXX;
        return slope;
    }

    private async Task RestartForNewTokenAsync()
    {
        try
        {
            _pulsoidErrorShown = false;
            _statsErrorShown = false;
            _statisticsUnavailable = false;

            await StopMonitoringHeartRateAsync().ConfigureAwait(false);
            await CheckMonitoringConditionsAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"Error restarting Pulsoid after a token change: {ex.Message}");
        }
    }

    private async Task CheckMonitoringConditionsAsync()
    {
        try
        {
            if (ShouldStartMonitoring() && !isMonitoringStarted)
            {
                await StartMonitoringHeartRateAsync().ConfigureAwait(false);
            }
            else if (!ShouldStartMonitoring())
            {
                await StopMonitoringHeartRateAsync();
            }
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"Error checking Pulsoid monitoring conditions: {ex.Message}");
        }
    }

    private int ApplyThrottle(int rawHR)
    {
        if (!Settings.ThrottleHR || rawHR <= Settings.ThrottleHRMax)
            return rawHR;

        const int maxHumanHR = 200;        int baseHR = Settings.ThrottleHRMax;
        int allowedSpread = Settings.ThrottleMaxAdditional;

        int excess = rawHR - baseHR;
        int compressibleRange = Math.Max(1, maxHumanHR - baseHR);

        int scaledAdjustment = (excess * allowedSpread) / compressibleRange;

        int variance = excess switch
        {
            < 30 => _random.Next(-3, 4),
            < 60 => _random.Next(-2, 3),
            _ => _random.Next(-1, 2)
        };

        return Math.Clamp(
            baseHR + scaledAdjustment + variance,
            baseHR,
            baseHR + allowedSpread
        );
    }

    private int GetOSCHeartRate()
    {
        lock (_oscHeartRatesLock)
        {
            if (!Settings.SmoothOSCHeartRate || _oscHeartRates.Count == 0)
            {
                return HeartRateFromSocket;
            }

            return (int)Math.Round(_oscHeartRates.Average());
        }
    }

    private void ResetIntervalFlag()
    {
        GotReadingThisInterval = false;
    }

    private void SendHeartRateDigits(string baseAddress, int hrValue)
    {
        int ones = hrValue % 10;
        int tens = (hrValue / 10) % 10;
        int hundreds = hrValue / 100;

        OscSender.SendOscParam($"{baseAddress}_Ones", ones);
        OscSender.SendOscParam($"{baseAddress}_Tens", tens);
        OscSender.SendOscParam($"{baseAddress}_Hundreds", hundreds);
    }

    private void SendHRToOSC(bool isHRBeat)
    {
        if (!_integrationSettings.IntgrHeartRate_OSC) return;

        bool isHRConnected = _appState.PulsoidAuthConnected;
        bool isHRActive = PulsoidDeviceOnline;

        int hrValueForOSC = GetOSCHeartRate();
        if (hrValueForOSC <= 0) return;

        float hrPercent = hrValueForOSC / 255f;
        float fullHRPercent = (hrValueForOSC / 127.5f) - 1f;

        OscSender.SendOscParam("/avatar/parameters/isHRConnected", isHRConnected);
        OscSender.SendOscParam("/avatar/parameters/isHRActive", isHRActive);
        OscSender.SendOscParam("/avatar/parameters/isHRBeat", isHRBeat);
        OscSender.SendOscParam("/avatar/parameters/HRPercent", hrPercent);
        OscSender.SendOscParam("/avatar/parameters/FullHRPercent", fullHRPercent);
        OscSender.SendOscParam("/avatar/parameters/HR", hrValueForOSC);

        if (!Settings.DisableLegacySupport)
        {
            int ones = hrValueForOSC % 10;
            int tens = (hrValueForOSC / 10) % 10;
            int hundreds = hrValueForOSC / 100;

            OscSender.SendOscParam("/avatar/parameters/onesHR", ones);
            OscSender.SendOscParam("/avatar/parameters/tensHR", tens);
            OscSender.SendOscParam("/avatar/parameters/hundredsHR", hundreds);
        }

        if (Settings.SentMCBHeartrateInfo && PulsoidStatistics != null)
        {
            SendMCBHeartRateInfo(hrValueForOSC);
        }
    }

    private void SendMCBHeartRateInfo(int hrValueForOSC)
    {
        bool isHot = hrValueForOSC >= Settings.HighTemperatureThreshold;
        bool isSleepy = hrValueForOSC < Settings.LowTemperatureThreshold;

        bool trendUp = Settings.HeartRateTrendIndicator == Settings.SelectedPulsoidTrendSymbol.UpwardTrendSymbol;
        bool trendDown = Settings.HeartRateTrendIndicator == Settings.SelectedPulsoidTrendSymbol.DownwardTrendSymbol;

        OscSender.SendOscParam("/avatar/parameters/MCB_Heartrate_Hot", isHot);
        OscSender.SendOscParam("/avatar/parameters/MCB_Heartrate_Sleepy", isSleepy);
        OscSender.SendOscParam("/avatar/parameters/MCB_Heartrate_TrendUp", trendUp);
        OscSender.SendOscParam("/avatar/parameters/MCB_Heartrate_TrendDown", trendDown);

        var stats = PulsoidStatistics;
        if (stats == null)
            return;

        if (!Settings.SentMCBHeartrateInfoLegacy)
        {
            OscSender.SendOscParam("/avatar/parameters/MCB_Heartrate_Min", stats.minimum_beats_per_minute);
            OscSender.SendOscParam("/avatar/parameters/MCB_Heartrate_Max", stats.maximum_beats_per_minute);
            OscSender.SendOscParam("/avatar/parameters/MCB_Heartrate_Avg", stats.average_beats_per_minute);
        }
        else
        {
            SendHeartRateDigits("/avatar/parameters/MCB_Heartrate_Min", stats.minimum_beats_per_minute);
            SendHeartRateDigits("/avatar/parameters/MCB_Heartrate_Max", stats.maximum_beats_per_minute);
            SendHeartRateDigits("/avatar/parameters/MCB_Heartrate_Avg", stats.average_beats_per_minute);
        }
    }

    private Task StartMonitoringHeartRateAsync()
    {
        if (Interlocked.CompareExchange(ref _startInProgress, 1, 0) == 1)
            return _monitorLoop ?? Task.CompletedTask;

        Task loop = RunMonitorLoopAsync();
        _monitorLoop = loop;
        return loop;
    }

    private async Task RunMonitorLoopAsync()
    {
        try
        {
            await StartMonitoringCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _startInProgress, 0);
        }
    }

    private async Task StartMonitoringCoreAsync()
    {
        if (isMonitoringStarted)
        {
            if (_client.IsConnected)
                return;

            await StopMonitoringCoreAsync();
        }

        if (_cts != null)
            return;

        isMonitoringStarted = true;
        string accessToken = Settings.AccessTokenOAuth;

        if (Settings.StoredTokenUnreadable)
        {
            _dispatcher.BeginInvoke(() =>
            {
                isMonitoringStarted = false;
                PulsoidAccessError = true;
                SetAuthState(PulsoidAuthState.Unreadable);
                PulsoidAccessErrorTxt = "The saved Pulsoid token could not be decrypted on this Windows account. Please reconnect.";
            });
            if (!_pulsoidErrorShown)
            {
                _pulsoidErrorShown = true;
                _toast?.Show("💓 Pulsoid", "The saved Pulsoid token could not be decrypted. Please reconnect.", ToastType.Error, key: "pulsoid-error");
            }
            return;
        }

        if (Settings.TokenEncryptionFailed && !string.IsNullOrEmpty(accessToken))
        {
            Logging.WriteInfo("Pulsoid: token could not be encrypted for storage; connecting with the in-memory token for this session.");
        }

        if (string.IsNullOrEmpty(accessToken))
        {
            _dispatcher.BeginInvoke(() =>
            {
                isMonitoringStarted = false;
                PulsoidAccessError = true;
                SetAuthState(PulsoidAuthState.NoToken);
                PulsoidAccessErrorTxt = "No Pulsoid connection found. Please connect with the Pulsoid Authentication server.";
            });
            if (!_pulsoidErrorShown)
            {
                _pulsoidErrorShown = true;
                _toast?.Show("💓 Pulsoid", "No Pulsoid connection. Please connect your account.", ToastType.Warning, key: "pulsoid-error");
            }
            return;
        }

        var validation = await OAuth.ValidateTokenAsync(accessToken).ConfigureAwait(false);

        if (validation == PulsoidTokenValidation.Invalid)
        {
            _dispatcher.BeginInvoke(() =>
            {
                isMonitoringStarted = false;
                PulsoidAccessError = true;
                SetAuthState(PulsoidAuthState.Rejected);
                PulsoidAccessErrorTxt = "Pulsoid rejected the saved token. Please reconnect.";
            });
            if (!_pulsoidErrorShown)
            {
                _pulsoidErrorShown = true;
                _toast?.Show("💓 Pulsoid", "Pulsoid rejected the saved token. Please reconnect.", ToastType.Warning, key: "pulsoid-error");
            }
            return;
        }

        if (validation == PulsoidTokenValidation.Unknown)
        {
            Logging.WriteInfo("Pulsoid token could not be verified right now; connecting with the saved sign-in anyway.");
            _dispatcher.BeginInvoke(() =>
            {
                MarkUnreachableIfSignedIn();
                PulsoidAccessError = true;
                PulsoidAccessErrorTxt = "Can't reach Pulsoid right now — retrying with your saved sign-in.";
            });
        }
        else
        {
            _dispatcher.BeginInvoke(() =>
            {
                SetAuthState(PulsoidAuthState.Authenticated);
                PulsoidAccessError = false;
                PulsoidAccessErrorTxt = string.Empty;
            });
        }

        var cts = new CancellationTokenSource();
        _cts = cts;

        try
        {
            await _client.ConnectAsync(accessToken, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _dispatcher.BeginInvoke(() =>
            {
                PulsoidAccessError = true;
                PulsoidAccessErrorTxt = ex.Message;
            });
            Logging.WriteException(ex);
        }
        finally
        {
            if (ReferenceEquals(_cts, cts))
            {
                cts.Dispose();
                _cts = null;
                isMonitoringStarted = false;
                _pulsoidErrorShown = false;
            }
        }
    }

    private async Task StopMonitoringHeartRateAsync()
    {
        await StopMonitoringCoreAsync().ConfigureAwait(false);

        Task? loop = _monitorLoop;
        if (loop is { IsCompleted: false })
        {
            try
            {
                await loop.WaitAsync(MonitorStopTimeout).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                Logging.WriteInfo(
                    $"Pulsoid connection loop did not stop within {MonitorStopTimeout.TotalSeconds:0.#}s; carrying on.");
            }
            catch
            {
            }
        }

        _monitorLoop = null;
    }

    private async Task StopMonitoringCoreAsync()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        await _client.DisconnectAsync().ConfigureAwait(false);

        if (_processDataTimer.Enabled)
            _processDataTimer.Stop();

        isMonitoringStarted = false;
    }

    private void UpdateHeartRateIcon(int hr)
    {
        if (HeartRate != hr)
        {
            _dispatcher.BeginInvoke(() =>
            {
                HeartRate = hr;
            });
        }

        Settings.HeartRateIcon = GetSanitizedHeartRateIcon(Settings, Settings.HeartRateIcon);

        if (Settings.MagicHeartRateIcons && Settings.HeartIcons != null && Settings.HeartIcons.Count > 0)
        {
            Settings.HeartRateIcon = Settings.HeartIcons[Settings.CurrentHeartIconIndex];
            Settings.CurrentHeartIconIndex = (Settings.CurrentHeartIconIndex + 1) % Settings.HeartIcons.Count;
        }
    }

    private void UpdateHeartRateTrendIndicator(int hr)
    {
        if (Settings.ShowHeartRateTrendIndicator)
        {
            int sampleRate = Math.Max(1, Settings.HeartRateTrendIndicatorSampleRate);
            if (_heartRateHistory.Count >= sampleRate)
            {
                _heartRateHistory.Dequeue();
            }

            _heartRateHistory.Enqueue(hr);

            if (sampleRate <= 1)
            {
                Settings.HeartRateTrendIndicator = "";
            }
            else if (_heartRateHistory.Count > 1)
            {
                double slope = CalculateSlope(_heartRateHistory);
                if (slope > Settings.HeartRateTrendIndicatorSensitivity)
                {
                    Settings.HeartRateTrendIndicator = Settings.SelectedPulsoidTrendSymbol.UpwardTrendSymbol;
                }
                else if (slope < -Settings.HeartRateTrendIndicatorSensitivity)
                {
                    Settings.HeartRateTrendIndicator = Settings.SelectedPulsoidTrendSymbol.DownwardTrendSymbol;
                }
                else
                {
                    Settings.HeartRateTrendIndicator = "";
                }
            }
        }
    }

    public async Task DisconnectSession()
    {
        await StopMonitoringHeartRateAsync();
    }

    public string GetHeartRateString()
        => BuildHeartRateString(Settings, HeartRate, PulsoidDeviceOnline, PulsoidStatistics);

    public static string BuildHeartRateString(
        PulsoidModuleSettings settings,
        int heartRate,
        bool deviceOnline,
        PulsoidStatisticsResponse? stats)
    {
        if (settings.EnableHeartRateOfflineCheck && !deviceOnline)
            return string.Empty;

        if (heartRate <= 0)
            return string.Empty;

        StringBuilder displayTextBuilder = new StringBuilder();

        if (settings.MagicHeartIconPrefix)
        {
            displayTextBuilder.Append(GetHeartRatePrefixText(settings, heartRate));
        }

        bool showCurrentHeartRate = true;

        if (settings.PulsoidStatsEnabled)
        {
            showCurrentHeartRate = !settings.HideCurrentHeartRate;
        }

        if (showCurrentHeartRate)
        {
            displayTextBuilder.Append(" " + heartRate.ToString());

            if (settings.ShowBPMSuffix)
            {
                displayTextBuilder.Append(" " + TextUtilities.TransformToSuperscript("bpm"));
            }
        }

        if (settings.ShowHeartRateTrendIndicator && !settings.TrendIndicatorBehindStats)
        {
            displayTextBuilder.Append($" {settings.HeartRateTrendIndicator}");
        }

        if (settings.PulsoidStatsEnabled && stats != null)
        {
            List<string> statsList = new List<string>();

            if (settings.ShowCalories)
            {
                statsList.Add(Stat(stats.calories_burned_in_kcal.ToString(), "kcal"));
            }
            if (settings.ShowAverageHeartRate)
            {
                statsList.Add(Stat(stats.average_beats_per_minute.ToString(), "avg"));
            }
            if (settings.ShowMaximumHeartRate)
            {
                statsList.Add(Stat(stats.maximum_beats_per_minute.ToString(), "max"));
            }
            if (settings.ShowMinimumHeartRate)
            {
                statsList.Add(Stat(stats.minimum_beats_per_minute.ToString(), "min"));
            }
            if (settings.ShowDuration)
            {
                TimeSpan duration = TimeSpan.FromSeconds(stats.streamed_duration_in_seconds);
                string formattedDuration = duration.ToString(@"hh\:mm\:ss");

                string durationLabel = settings.ShowStatsTimeRange
                    ? $"duration over {settings.SelectedStatisticsTimeRange.GetDescription()}"
                    : "duration";

                statsList.Add(Stat(formattedDuration, durationLabel));
            }

            if (statsList.Count > 0)
            {
                string statslist = string.Join("|", statsList);
                displayTextBuilder.Append($" {statslist}");
            }
        }

        if (settings.ShowHeartRateTrendIndicator && settings.TrendIndicatorBehindStats)
        {
            displayTextBuilder.Append($" {settings.HeartRateTrendIndicator}");
        }

        if (settings.HeartRateTitle)
        {
            string titleSeparator = settings.SeparateTitleWithEnter ? "\v" : ": ";
            string hrTitle = settings.CurrentHeartRateTitle + titleSeparator;
            displayTextBuilder.Insert(0, hrTitle);
        }

        return displayTextBuilder.ToString();
    }

    private static string Stat(string value, string label)
        => $"{value} {TextUtilities.TransformToSuperscript(label)}";

    private static string GetHeartRatePrefixText(PulsoidModuleSettings settings, int hr)
    {
        string heartIcon = GetSanitizedHeartRateIcon(settings, settings.HeartRateIcon);
        string statusText = GetTemperatureStatusText(settings, hr);
        return heartIcon + statusText;
    }

    private static string GetTemperatureStatusText(PulsoidModuleSettings settings, int hr)
    {
        if (!settings.ShowTemperatureText)
            return string.Empty;

        if (hr < settings.LowTemperatureThreshold)
            return TextUtilities.TransformToSuperscript(settings.LowHeartRateText);

        if (hr >= settings.HighTemperatureThreshold)
            return TextUtilities.TransformToSuperscript(settings.HighHeartRateText);

        return string.Empty;
    }

    private static string GetSanitizedHeartRateIcon(PulsoidModuleSettings settings, string icon)
    {
        string sanitized = icon ?? string.Empty;
        sanitized = StripRepeatedSuffix(sanitized, TextUtilities.TransformToSuperscript(settings.LowHeartRateText));
        sanitized = StripRepeatedSuffix(sanitized, TextUtilities.TransformToSuperscript(settings.HighHeartRateText));
        return sanitized;
    }

    private static string StripRepeatedSuffix(string value, string suffix)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(suffix))
            return value;

        while (value.EndsWith(suffix, StringComparison.Ordinal))
        {
            value = value.Substring(0, value.Length - suffix.Length);
        }

        return value;
    }

    public bool IsRelevantPropertyChange(string? propertyName)
    {
        return propertyName == nameof(_integrationSettings.IntgrHeartRate) ||
               propertyName == nameof(_appState.IsVRRunning) ||
               propertyName == nameof(_integrationSettings.IntgrHeartRate_VR) ||
               propertyName == nameof(_integrationSettings.IntgrHeartRate_DESKTOP) ||
               propertyName == nameof(_integrationSettings.IntgrHeartRate_OSC) ||
               propertyName == nameof(_appState.PulsoidAuthConnected);
    }

    public async Task ProcessDataAsync()
    {
        if (Interlocked.Exchange(ref _isProcessing, 1) == 1)
            return;

        try
        {
            TimeSpan inactivity = DateTime.Now - _lastMessageReceivedTime;
            if (inactivity > _inactivityThreshold)
            {
                var nowUtc = DateTime.UtcNow;
                if (nowUtc - _lastTokenValidationUtc >= _tokenValidationInterval)
                {
                    _lastTokenValidationUtc = nowUtc;
                    var validation = await OAuth.ValidateTokenAsync(Settings.AccessTokenOAuth);
                    if (validation == PulsoidTokenValidation.Invalid)
                    {
                        _dispatcher.BeginInvoke(() =>
                        {
                            PulsoidAccessError = true;
                            PulsoidAccessErrorTxt = "Pulsoid rejected the saved token. Please reconnect.";
                            SetAuthState(PulsoidAuthState.Rejected);
                        });
                        await StopMonitoringHeartRateAsync();
                        return;
                    }

                    if (validation == PulsoidTokenValidation.Unknown)
                    {
                        _dispatcher.BeginInvoke(() =>
                        {
                            MarkUnreachableIfSignedIn();
                            PulsoidAccessErrorTxt = "Can't reach Pulsoid right now — your sign-in is kept.";
                        });
                    }
                    else
                    {
                        _dispatcher.BeginInvoke(() => SetAuthState(PulsoidAuthState.Authenticated));
                    }
                }

                if (nowUtc - _lastInactivityLogUtc >= _inactivityLogInterval)
                {
                    Logging.WriteInfo($"No messages received for {inactivity.TotalSeconds} seconds, device might be offline.");
                    _lastInactivityLogUtc = nowUtc;
                }

                PulsoidDeviceOnline = false;
                return;
            }

            bool shouldBeOnline = HeartRateFromSocket > 0;

            if (shouldBeOnline)
            {
                if (HeartRateFromSocket == _previousHeartRate)
                {
                    _unchangedHeartRateCount++;
                }
                else
                {
                    _unchangedHeartRateCount = 0;
                    _previousHeartRate = HeartRateFromSocket;
                }

                if (Settings.EnableHeartRateOfflineCheck && _unchangedHeartRateCount >= Settings.UnchangedHeartRateTimeoutInSec)
                {
                    shouldBeOnline = false;
                    ResetIntervalFlag();
                    Logging.WriteInfo($"HR unchanged for {_unchangedHeartRateCount} seconds. Marking offline.");
                }
            }

            DateTime currentTime = DateTime.Now;
            if (PulsoidDeviceOnline != shouldBeOnline)
            {
                if ((currentTime - _lastStateChangeTime) > _stateChangeDebounce)
                {
                    PulsoidDeviceOnline = shouldBeOnline;
                    _lastStateChangeTime = currentTime;

                    if (!PulsoidDeviceOnline)
                    {
                        Logging.WriteInfo("Pulsoid device went offline.");
                        ResetIntervalFlag();
                    }
                    else
                    {
                        Logging.WriteInfo("Pulsoid device is online.");
                    }
                }
            }

            if (!PulsoidDeviceOnline)
            {
                return;
            }

            int hr = HeartRateFromSocket;

            if (Settings.PulsoidStatsEnabled && !_statisticsUnavailable)
            {
                var nowUtc = DateTime.UtcNow;
                if (nowUtc - _lastStatsFetchUtc >= _statsFetchInterval)
                {
                    _lastStatsFetchUtc = nowUtc;
                    string timeRange = Settings.SelectedStatisticsTimeRange.GetDescription();
                    var stats = await _client.FetchStatisticsAsync(Settings.AccessTokenOAuth, timeRange);
                    if (stats != null)
                    {
                        if (Settings.ApplyHeartRateAdjustment)
                        {
                            stats.maximum_beats_per_minute = Math.Clamp(stats.maximum_beats_per_minute + Settings.HeartRateAdjustment, 0, 255);
                            stats.minimum_beats_per_minute = Math.Clamp(stats.minimum_beats_per_minute + Settings.HeartRateAdjustment, 0, 255);
                            stats.average_beats_per_minute = Math.Clamp(stats.average_beats_per_minute + Settings.HeartRateAdjustment, 0, 255);
                        }
                        PulsoidStatistics = stats;
                    }
                }
            }

            if (Settings.SmoothHeartRate)
            {
                var now = DateTime.UtcNow;
                _heartRates.Enqueue(new Tuple<DateTime, int>(now, hr));
                while (_heartRates.Count > 0 && now - _heartRates.Peek().Item1 > TimeSpan.FromSeconds(Settings.SmoothHeartRateTimeSpan))
                {
                    _heartRates.Dequeue();
                }
                if (_heartRates.Count > 0)
                {
                    hr = (int)_heartRates.Average(t => t.Item2);
                }
            }

            UpdateHeartRateTrendIndicator(hr);
            UpdateHeartRateIcon(hr);

            if (HeartRate != hr)
            {
                HeartRate = hr;
            }

            if (_integrationSettings.IntgrHeartRate_OSC && !GotReadingThisInterval)
            {
                SendHRToOSC(false);
            }

            ResetIntervalFlag();
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
        finally
        {
            Interlocked.Exchange(ref _isProcessing, 0);
        }
    }

    public void PropertyChangedHandler(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Settings.HeartRateScanInterval))
        {
            _processDataTimer.Interval = Settings.HeartRateScanInterval * 1000;
            return;
        }

        if (e.PropertyName == nameof(Settings.AccessTokenOAuth))
        {
            _ = RestartForNewTokenAsync();
            return;
        }

        if (IsRelevantPropertyChange(e.PropertyName))
        {
            _ = CheckMonitoringConditionsAsync();
        }
    }

    public void RefreshTimeRanges()
    {
        Settings.StatisticsTimeRanges = new List<StatisticsTimeRange>
    {
        StatisticsTimeRange._24h,
        StatisticsTimeRange._7d,
        StatisticsTimeRange._30d
    };

        var rangeExists = Settings.StatisticsTimeRanges.Any(r => r == Settings.SelectedStatisticsTimeRange);
        if (!rangeExists)
        {
            Settings.SelectedStatisticsTimeRange = Settings.StatisticsTimeRanges.FirstOrDefault();
        }
    }

    public void RefreshTrendSymbols()
    {
        Settings.PulsoidTrendSymbols = new List<PulsoidTrendSymbolSet>
    {
        new PulsoidTrendSymbolSet { UpwardTrendSymbol = "↑", DownwardTrendSymbol = "↓" },
        new PulsoidTrendSymbolSet { UpwardTrendSymbol = "⤴️", DownwardTrendSymbol = "⤵️" },
        new PulsoidTrendSymbolSet { UpwardTrendSymbol = "⬆", DownwardTrendSymbol = "⬇" },
        new PulsoidTrendSymbolSet { UpwardTrendSymbol = "↗", DownwardTrendSymbol = "↘" },
        new PulsoidTrendSymbolSet { UpwardTrendSymbol = "🔺", DownwardTrendSymbol = "🔻" },
    };

        var selectedSymbol = Settings.SelectedPulsoidTrendSymbol?.CombinedTrendSymbol;
        var matchingSymbol = Settings.PulsoidTrendSymbols.FirstOrDefault(s => s.CombinedTrendSymbol == selectedSymbol);

        Settings.SelectedPulsoidTrendSymbol = matchingSymbol ?? Settings.PulsoidTrendSymbols[0];
    }

    public bool ShouldStartMonitoring()
    {
        return _integrationSettings.IntgrHeartRate && _appState.IsVRRunning && _integrationSettings.IntgrHeartRate_VR ||
               _integrationSettings.IntgrHeartRate && !_appState.IsVRRunning && _integrationSettings.IntgrHeartRate_DESKTOP ||
               _integrationSettings.IntgrHeartRate_OSC;
    }

    public void SetAuthState(PulsoidAuthState newState)
    {
        if (_appState.PulsoidAuthState != newState)
            _appState.PulsoidAuthState = newState;
    }

    private void MarkUnreachableIfSignedIn()
    {
        if (_appState.PulsoidAuthState is PulsoidAuthState.Authenticated or PulsoidAuthState.Unverified)
            _appState.PulsoidAuthState = PulsoidAuthState.Unreachable;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _client.HeartRateReceived -= OnHeartRateReceived;
        _client.ConnectionFailed -= OnConnectionFailed;
        _client.ConnectionStateChanged -= OnConnectionStateChanged;
        Settings.PropertyChanged -= PropertyChangedHandler;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        _processDataTimer?.Stop();
        _processDataTimer?.Dispose();
    }
}
