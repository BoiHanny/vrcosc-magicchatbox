using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Utilities;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.Models;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Main Pulsoid monitoring module that connects to the Pulsoid WebSocket,
/// processes heart rate data, and sends updates to VRChat via OSC.
/// </summary>
public partial class PulsoidModule : ObservableObject, IModule
{
    private CancellationTokenSource _cts;
    private bool _disposed;
    private readonly IAppState _appState;
    private readonly IUiDispatcher _dispatcher;
    private readonly IToastService? _toast;
    private volatile bool _pulsoidErrorShown;

    private readonly IOscSender _oscSender;
    private IOscSender OscSender => _oscSender;

    private readonly IntegrationSettings _integrationSettings;

    private readonly PulsoidOAuthHandler _oAuth;
    private PulsoidOAuthHandler OAuth => _oAuth;

    private readonly IPulsoidClient _client;

    private readonly Queue<int> _heartRateHistory = new();

    // For normal smoothing (time-based)
    private readonly Queue<Tuple<DateTime, int>> _heartRates = new();
    private DateTime _lastStateChangeTime = DateTime.MinValue;
    private DateTime _lastMessageReceivedTime = DateTime.Now;
    private readonly TimeSpan _inactivityThreshold = TimeSpan.FromSeconds(15);
    private static readonly Random _random = new Random();

    // For OSC smoothing (count-based)
    private readonly Queue<int> _oscHeartRates = new();
    private readonly object _oscHeartRatesLock = new object();
    private int _isProcessing = 0;
    private DateTime _lastStatsFetchUtc = DateTime.MinValue;
    private DateTime _lastTokenValidationUtc = DateTime.MinValue;
    private DateTime _lastInactivityLogUtc = DateTime.MinValue;
    private static readonly TimeSpan _statsFetchInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _tokenValidationInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _inactivityLogInterval = TimeSpan.FromSeconds(30);
    private int _previousHeartRate = -1;
    private System.Timers.Timer _processDataTimer;
    private readonly TimeSpan _stateChangeDebounce = TimeSpan.FromSeconds(2);
    private int _unchangedHeartRateCount = 0;

    [ObservableProperty]
    private string formattedHighHeartRateText;

    [ObservableProperty]
    private string formattedLowHeartRateText;
    private bool GotReadingThisInterval = false;

    [ObservableProperty]
    private int heartRate;

    private int HeartRateFromSocket = 0;

    [ObservableProperty]
    private DateTime heartRateLastUpdate = DateTime.Now;
    private bool isMonitoringStarted = false;

    [ObservableProperty]
    private bool pulsoidAccessError = false;

    [ObservableProperty]
    private string pulsoidAccessErrorTxt = string.Empty;

    [ObservableProperty]
    private bool pulsoidDeviceOnline = false;
    public PulsoidStatisticsResponse PulsoidStatistics;

    [ObservableProperty]
    public PulsoidModuleSettings settings;

    public string Name => "Pulsoid";
    public bool IsEnabled { get; set; } = true;
    public bool IsRunning => isMonitoringStarted;
    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task StartAsync(CancellationToken ct = default) => Task.CompletedTask;
    public async Task StopAsync(CancellationToken ct = default) { await StopMonitoringHeartRateAsync(); }
    public void SaveSettings() => Settings?.SaveSettings();

    public PulsoidModule(IAppState appState, IPulsoidClient client, IUiDispatcher dispatcher, IOscSender oscSender, IntegrationSettings integrationSettings, PulsoidOAuthHandler oAuth, IEnvironmentService env, IToastService? toast = null)
    {
        _appState = appState;
        _client = client;
        _dispatcher = dispatcher;
        _oscSender = oscSender;
        _integrationSettings = integrationSettings;
        _oAuth = oAuth;
        _toast = toast;
        var settingsPath = Path.Combine(env.DataPath, "PulsoidModuleSettings.json");
        Settings = PulsoidModuleSettings.LoadSettings(settingsPath);
        RefreshTrendSymbols();
        RefreshTimeRanges();

        // Subscribe to client events (fire on background threads — marshal to UI where needed)
        _client.HeartRateReceived += OnHeartRateReceived;
        _client.ConnectionFailed += OnConnectionFailed;
        _client.ConnectionStateChanged += OnConnectionStateChanged;

        _processDataTimer = new System.Timers.Timer
        {
            AutoReset = true,
            Interval = 1000
        };
        _processDataTimer.Elapsed += (sender, e) =>
        {
            _dispatcher.BeginInvoke(() => _ = ProcessDataAsync());
        };

        _ = CheckMonitoringConditionsAsync();
    }

    private void OnHeartRateReceived(int rawHR)
    {
        HandleHeartRateMessage(rawHR);
    }

    private void OnConnectionFailed(PulsoidConnectionError error, string message)
    {
        if (!_pulsoidErrorShown)
        {
            _pulsoidErrorShown = true;
            _toast?.Show("💓 Pulsoid Error", message, ToastType.Error, key: "pulsoid-error");
        }

        _dispatcher.BeginInvoke(() =>
        {
            PulsoidAccessError = true;
            PulsoidAccessErrorTxt = message;
            if (error == PulsoidConnectionError.TokenInvalid)
                TriggerPulsoidAuthConnected(false);
        });
    }

    private void OnConnectionStateChanged(bool connected)
    {
        if (connected)
        {
            _pulsoidErrorShown = false;
            _dispatcher.BeginInvoke(() =>
            {
                PulsoidAccessError = false;
                PulsoidAccessErrorTxt = "";
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
        double avgX = count / 2.0;
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

        const int maxHumanHR = 200; // Absolute physiological limit
        int baseHR = Settings.ThrottleHRMax;
        int allowedSpread = Settings.ThrottleMaxAdditional;

        int excess = rawHR - baseHR;
        int compressibleRange = maxHumanHR - baseHR;

        // Integer-based proportional scaling (no floating points)
        int scaledAdjustment = (excess * allowedSpread) / compressibleRange;

        // Smart randomness that decreases with higher HR
        int variance = excess switch
        {
            < 30 => _random.Next(-3, 4),  // ±3 BPM when close to base
            < 60 => _random.Next(-2, 3),  // ±2 BPM
            _ => _random.Next(-1, 2)      // ±1 BPM at extreme highs
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

    /// <summary>
    /// Send HR and associated parameters to the avatar via OSC.
    /// </summary>
    /// <param name="isHRBeat">True if this is triggered by a new HR reading, false if a fallback update.</param>
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

        if (!Settings.SentMCBHeartrateInfoLegacy)
        {
            OscSender.SendOscParam("/avatar/parameters/MCB_Heartrate_Min", PulsoidStatistics.minimum_beats_per_minute);
            OscSender.SendOscParam("/avatar/parameters/MCB_Heartrate_Max", PulsoidStatistics.maximum_beats_per_minute);
            OscSender.SendOscParam("/avatar/parameters/MCB_Heartrate_Avg", PulsoidStatistics.average_beats_per_minute);
        }
        else
        {
            // Send min/max/avg as ones, tens, hundreds
            SendHeartRateDigits("/avatar/parameters/MCB_Heartrate_Min", PulsoidStatistics.minimum_beats_per_minute);
            SendHeartRateDigits("/avatar/parameters/MCB_Heartrate_Max", PulsoidStatistics.maximum_beats_per_minute);
            SendHeartRateDigits("/avatar/parameters/MCB_Heartrate_Avg", PulsoidStatistics.average_beats_per_minute);
        }
    }

    private async Task StartMonitoringHeartRateAsync()
    {
        if (isMonitoringStarted)
        {
            if (_client.IsConnected)
                return;

            await StopMonitoringHeartRateAsync();
        }

        if (_cts != null)
            return;

        isMonitoringStarted = true;
        string accessToken = Settings.AccessTokenOAuth;
        if (string.IsNullOrEmpty(accessToken))
        {
            _dispatcher.BeginInvoke(() =>
            {
                isMonitoringStarted = false;
                PulsoidAccessError = true;
                TriggerPulsoidAuthConnected(false);
                PulsoidAccessErrorTxt = "No Pulsoid connection found. Please connect with the Pulsoid Authentication server.";
            });
            if (!_pulsoidErrorShown)
            {
                _pulsoidErrorShown = true;
                _toast?.Show("💓 Pulsoid", "No Pulsoid connection. Please connect your account.", ToastType.Warning, key: "pulsoid-error");
            }
            return;
        }

        bool isTokenValid = await OAuth.ValidateTokenAsync(accessToken).ConfigureAwait(false);
        if (!isTokenValid)
        {
            _dispatcher.BeginInvoke(() =>
            {
                isMonitoringStarted = false;
                PulsoidAccessError = true;
                TriggerPulsoidAuthConnected(false);
                PulsoidAccessErrorTxt = "Expired access token. Please reconnect.";
            });
            if (!_pulsoidErrorShown)
            {
                _pulsoidErrorShown = true;
                _toast?.Show("💓 Pulsoid", "Expired access token. Please reconnect.", ToastType.Warning, key: "pulsoid-error");
            }
            return;
        }

        _cts = new CancellationTokenSource();
        UpdateFormattedHeartRateText();

        try
        {
            // Client handles connection, reconnection, and message receiving internally
            await _client.ConnectAsync(accessToken, _cts.Token).ConfigureAwait(false);
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
    }

    private async Task StopMonitoringHeartRateAsync()
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

        Settings.HeartRateIcon = GetSanitizedHeartRateIcon(Settings.HeartRateIcon);

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
            if (_heartRateHistory.Count >= Settings.HeartRateTrendIndicatorSampleRate)
            {
                _heartRateHistory.Dequeue();
            }

            _heartRateHistory.Enqueue(hr);

            if (_heartRateHistory.Count > 1)
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
    {
        if (Settings.EnableHeartRateOfflineCheck && !PulsoidDeviceOnline)
            return string.Empty;

        if (HeartRate <= 0)
            return string.Empty;

        StringBuilder displayTextBuilder = new StringBuilder();

        if (Settings.MagicHeartIconPrefix)
        {
            displayTextBuilder.Append(GetHeartRatePrefixText());
        }

        bool showCurrentHeartRate = true;

        if (Settings.PulsoidStatsEnabled)
        {
            showCurrentHeartRate = !Settings.HideCurrentHeartRate;
        }

        if (showCurrentHeartRate)
        {
            displayTextBuilder.Append(" " + HeartRate.ToString());

            if (Settings.ShowBPMSuffix)
            {
                displayTextBuilder.Append(" bpm");
            }
        }

        if (Settings.ShowHeartRateTrendIndicator && !Settings.TrendIndicatorBehindStats)
        {
            displayTextBuilder.Append($" {Settings.HeartRateTrendIndicator}");
        }

        if (Settings.PulsoidStatsEnabled && PulsoidStatistics != null)
        {
            List<string> statsList = new List<string>();

            if (Settings.ShowCalories)
            {
                statsList.Add($"{PulsoidStatistics.calories_burned_in_kcal} kcal");
            }
            if (Settings.ShowAverageHeartRate)
            {
                statsList.Add($"{PulsoidStatistics.average_beats_per_minute} Avg");
            }
            if (Settings.ShowMaximumHeartRate)
            {
                statsList.Add($"{PulsoidStatistics.maximum_beats_per_minute} Max");
            }
            if (Settings.ShowMinimumHeartRate)
            {
                statsList.Add($"{PulsoidStatistics.minimum_beats_per_minute} Min");
            }
            if (Settings.ShowDuration)
            {
                TimeSpan duration = TimeSpan.FromSeconds(PulsoidStatistics.streamed_duration_in_seconds);
                string formattedDuration = duration.ToString(@"hh\:mm\:ss");

                if (Settings.ShowStatsTimeRange)
                {
                    string timeRangeDescription = Settings.SelectedStatisticsTimeRange.GetDescription();
                    statsList.Add($"duration over {timeRangeDescription} {formattedDuration} ");
                }
                else
                {
                    statsList.Add($"duration {formattedDuration}");
                }
            }

            for (int i = 0; i < statsList.Count; i++)
            {
                statsList[i] = TextUtilities.TransformToSuperscript(statsList[i]);
            }

            if (statsList.Count > 0)
            {
                string statslist = string.Join("|", statsList);
                displayTextBuilder.Append($" {statslist}");
            }
        }

        if (Settings.ShowHeartRateTrendIndicator && Settings.TrendIndicatorBehindStats)
        {
            displayTextBuilder.Append($" {Settings.HeartRateTrendIndicator}");
        }

        if (Settings.HeartRateTitle)
        {
            string titleSeparator = Settings.SeparateTitleWithEnter ? "\v" : ": ";
            string hrTitle = Settings.CurrentHeartRateTitle + titleSeparator;
            displayTextBuilder.Insert(0, hrTitle);
        }

        return displayTextBuilder.ToString();
    }

    private string GetHeartRatePrefixText()
    {
        string heartIcon = GetSanitizedHeartRateIcon(Settings.HeartRateIcon);
        string statusText = GetTemperatureStatusText(HeartRate);
        return heartIcon + statusText;
    }

    private string GetTemperatureStatusText(int hr)
    {
        if (!Settings.ShowTemperatureText)
            return string.Empty;

        if (hr < Settings.LowTemperatureThreshold)
            return FormattedLowHeartRateText;

        if (hr >= Settings.HighTemperatureThreshold)
            return FormattedHighHeartRateText;

        return string.Empty;
    }

    private string GetSanitizedHeartRateIcon(string icon)
    {
        string sanitized = icon ?? string.Empty;
        sanitized = StripRepeatedSuffix(sanitized, FormattedLowHeartRateText);
        sanitized = StripRepeatedSuffix(sanitized, FormattedHighHeartRateText);
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

    public bool IsRelevantPropertyChange(string propertyName)
    {
        return propertyName == nameof(_integrationSettings.IntgrHeartRate) ||
               propertyName == nameof(_appState.IsVRRunning) ||
               propertyName == nameof(_integrationSettings.IntgrHeartRate_VR) ||
               propertyName == nameof(_integrationSettings.IntgrHeartRate_DESKTOP) ||
               propertyName == nameof(_integrationSettings.IntgrHeartRate_OSC) ||
               propertyName == nameof(_appState.PulsoidAuthConnected);
    }

    public void OnApplicationClosing()
    {
        Settings.SaveSettings();
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
                    bool tokenValid = await OAuth.ValidateTokenAsync(Settings.AccessTokenOAuth);
                    if (!tokenValid)
                    {
                        _dispatcher.BeginInvoke(() =>
                        {
                            PulsoidAccessError = true;
                            PulsoidAccessErrorTxt = "Access token invalid or revoked. Please reconnect.";
                            TriggerPulsoidAuthConnected(false);
                        });
                        await StopMonitoringHeartRateAsync();
                        return;
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

            if (Settings.PulsoidStatsEnabled)
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

    public void PropertyChangedHandler(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Settings.HeartRateScanInterval))
        {
            _processDataTimer.Interval = Settings.HeartRateScanInterval * 1000;
            return;
        }

        if (e.PropertyName == nameof(Settings.AccessTokenOAuth))
        {
            _ = CheckMonitoringConditionsAsync();
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

        var symbolExists = Settings.PulsoidTrendSymbols.Any(s => s.CombinedTrendSymbol == Settings.SelectedPulsoidTrendSymbol.CombinedTrendSymbol);

        if (symbolExists)
        {
            Settings.SelectedPulsoidTrendSymbol = Settings.PulsoidTrendSymbols.FirstOrDefault(s => s.CombinedTrendSymbol == Settings.SelectedPulsoidTrendSymbol.CombinedTrendSymbol);
        }
        else
        {
            Settings.SelectedPulsoidTrendSymbol = Settings.PulsoidTrendSymbols.FirstOrDefault();
        }
    }

    public bool ShouldStartMonitoring()
    {
        return _integrationSettings.IntgrHeartRate && _appState.IsVRRunning && _integrationSettings.IntgrHeartRate_VR ||
               _integrationSettings.IntgrHeartRate && !_appState.IsVRRunning && _integrationSettings.IntgrHeartRate_DESKTOP ||
               _integrationSettings.IntgrHeartRate_OSC;
    }

    public void TriggerPulsoidAuthConnected(bool newValue)
    {
        bool currentvalue = _appState.PulsoidAuthConnected;
        if (newValue != currentvalue)
        {
            _appState.PulsoidAuthConnected = newValue;
        }
    }

    public void UpdateFormattedHeartRateText()
    {
        FormattedLowHeartRateText = TextUtilities.TransformToSuperscript(Settings.LowHeartRateText);
        FormattedHighHeartRateText = TextUtilities.TransformToSuperscript(Settings.HighHeartRateText);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Unsubscribe from client events to prevent leak
        _client.HeartRateReceived -= OnHeartRateReceived;
        _client.ConnectionFailed -= OnConnectionFailed;
        _client.ConnectionStateChanged -= OnConnectionStateChanged;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        _processDataTimer?.Stop();
        _processDataTimer?.Dispose();
    }
}
