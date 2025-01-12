using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Web;
using System.Threading;
using System.Linq;
using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System.Net.WebSockets;
using System.Text;
using System.Collections.Concurrent;
using System.Timers;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels.Models;

namespace vrcosc_magicchatbox.Classes.Modules
{
    /// <summary>
    /// Holds user-specific settings for the Pulsoid module and provides serialization to JSON.
    /// </summary>
    public partial class PulsoidModuleSettings : ObservableObject
    {
        private const string SettingsFileName = "PulsoidModuleSettings.json";

        [ObservableProperty]
        private bool applyHeartRateAdjustment = false;

        [ObservableProperty]
        private int currentHeartIconIndex = 0;

        [ObservableProperty]
        private string currentHeartRateTitle = "Heart Rate";

        [ObservableProperty]
        private bool disableLegacySupport = false;

        [ObservableProperty]
        private bool enableHeartRateOfflineCheck = true;

        [ObservableProperty]
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

        public static string GetFullSettingsPath()
        {
            return Path.Combine(ViewModel.Instance.DataPath, SettingsFileName);
        }

        /// <summary>
        /// Load settings from disk. If no file or corrupted, returns a new instance.
        /// </summary>
        public static PulsoidModuleSettings LoadSettings()
        {
            var settingsPath = GetFullSettingsPath();

            if (File.Exists(settingsPath))
            {
                string settingsJson = File.ReadAllText(settingsPath);

                if (string.IsNullOrWhiteSpace(settingsJson) || settingsJson.All(c => c == '\0'))
                {
                    Logging.WriteInfo("The settings JSON file is empty or corrupted.");
                    return new PulsoidModuleSettings();
                }

                try
                {
                    var settings = JsonConvert.DeserializeObject<PulsoidModuleSettings>(settingsJson);
                    return settings ?? new PulsoidModuleSettings();
                }
                catch (JsonException ex)
                {
                    Logging.WriteInfo($"Error parsing settings JSON: {ex.Message}");
                    return new PulsoidModuleSettings();
                }
            }
            else
            {
                Logging.WriteInfo("Settings file does not exist, returning new settings instance.");
                return new PulsoidModuleSettings();
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
                File.WriteAllText(GetFullSettingsPath(), settingsJson);
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"Error saving settings: {ex.Message}");
            }
        }
    }

    public enum StatisticsTimeRange
    {
        [Description("24h")]
        _24h,
        [Description("7d")]
        _7d,
        [Description("30d")]
        _30d
    }

    public class HeartRateData
    {
        public int HeartRate { get; set; }
        public DateTime MeasuredAt { get; set; }
    }

    public partial class PulsoidStatisticsResponse
    {
        public int average_beats_per_minute { get; set; } = 0;
        public int calories_burned_in_kcal { get; set; } = 0;
        public int maximum_beats_per_minute { get; set; } = 0;
        public int minimum_beats_per_minute { get; set; } = 0;
        public int streamed_duration_in_seconds { get; set; } = 0;
    }

    /// <summary>
    /// Main Pulsoid monitoring module that connects to the Pulsoid WebSocket,
    /// processes heart rate data, and sends updates to VRChat via OSC.
    /// </summary>
    public partial class PulsoidModule : ObservableObject
    {
        private CancellationTokenSource _cts;
        private readonly object _fetchLock = new object();

        private readonly Queue<int> _heartRateHistory = new();

        // For normal smoothing (time-based)
        private readonly Queue<Tuple<DateTime, int>> _heartRates = new();
        private bool _isFetchingStatistics = false;
        private DateTime _lastStateChangeTime = DateTime.MinValue;

        // For OSC smoothing (count-based)
        private readonly Queue<int> _oscHeartRates = new();
        private int _previousHeartRate = -1;
        private System.Timers.Timer _processDataTimer;
        private readonly TimeSpan _stateChangeDebounce = TimeSpan.FromSeconds(2);
        private HttpClient _StatisticsClient = new HttpClient();
        private int _unchangedHeartRateCount = 0;
        private ClientWebSocket _webSocket;

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

        public PulsoidModule()
        {
            Settings = PulsoidModuleSettings.LoadSettings();
            RefreshTrendSymbols();
            RefreshTimeRanges();

            _processDataTimer = new System.Timers.Timer
            {
                AutoReset = true,
                Interval = 1000
            };
            _processDataTimer.Elapsed += (sender, e) =>
            {
                if (Application.Current != null)
                {
                    Application.Current.Dispatcher.Invoke(ProcessData);
                }
            };

            CheckMonitoringConditions();
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

        private void CheckMonitoringConditions()
        {
            if (ShouldStartMonitoring() && !isMonitoringStarted)
            {
                StartMonitoringHeartRateAsync().ConfigureAwait(false);
            }
            else if (!ShouldStartMonitoring())
            {
                StopMonitoringHeartRateAsync();
            }
        }

        private async Task ConnectToWebSocketAsync(string accessToken, CancellationToken cancellationToken)
        {
            _webSocket = new ClientWebSocket();
            _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            _webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(5);

            try
            {
                await _webSocket.ConnectAsync(new Uri("wss://dev.pulsoid.net/api/v1/data/real_time"), cancellationToken).ConfigureAwait(false);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    PulsoidAccessError = false;
                    PulsoidAccessErrorTxt = "";
                });

                _processDataTimer.Start();

                await HeartRateMonitoringLoopAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (WebSocketException ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    PulsoidAccessError = true;
                    PulsoidAccessErrorTxt = ex.Message;
                });
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        private async Task FetchPulsoidStatisticsAsync(string accessToken)
        {
            lock (_fetchLock)
            {
                if (_isFetchingStatistics) return;
                _isFetchingStatistics = true;
            }

            try
            {
                string timeRangeDescription = Settings.SelectedStatisticsTimeRange.GetDescription();
                string requestUri = $"https://dev.pulsoid.net/api/v1/statistics?time_range={timeRangeDescription}";

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUri);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Headers.Add("User-Agent", "Vrcosc-MagicChatbox");
                request.Headers.Add("Accept", "application/json");

                HttpResponseMessage response = await _StatisticsClient.SendAsync(request).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Debug.WriteLine($"Error fetching Pulsoid statistics: {response.StatusCode}, Content: {errorContent}");
                    return;
                }

                string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                PulsoidStatistics = JsonConvert.DeserializeObject<PulsoidStatisticsResponse>(content);

                if (PulsoidStatistics != null && Settings.ApplyHeartRateAdjustment)
                {
                    PulsoidStatistics.maximum_beats_per_minute += Settings.HeartRateAdjustment;
                    PulsoidStatistics.minimum_beats_per_minute += Settings.HeartRateAdjustment;
                    PulsoidStatistics.average_beats_per_minute += Settings.HeartRateAdjustment;

                    // Ensure values are clamped to 0-255 as per HR range if desired
                    PulsoidStatistics.maximum_beats_per_minute = Math.Clamp(PulsoidStatistics.maximum_beats_per_minute, 0, 255);
                    PulsoidStatistics.minimum_beats_per_minute = Math.Clamp(PulsoidStatistics.minimum_beats_per_minute, 0, 255);
                    PulsoidStatistics.average_beats_per_minute = Math.Clamp(PulsoidStatistics.average_beats_per_minute, 0, 255);
                }
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"HttpRequestException: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"General Exception: {ex.Message}");
            }
            finally
            {
                lock (_fetchLock)
                {
                    _isFetchingStatistics = false;
                }
            }
        }

        private int GetOSCHeartRate()
        {
            if (!Settings.SmoothOSCHeartRate || _oscHeartRates.Count == 0)
            {
                return HeartRateFromSocket;
            }
            else
            {
                return (int)Math.Round(_oscHeartRates.Average());
            }
        }

        private void HandleHeartRateMessage(string message)
        {
            int rawHR = ParseHeartRateFromMessage(message);
            if (rawHR == -1) return;

            if (Settings.ApplyHeartRateAdjustment)
            {
                rawHR += Settings.HeartRateAdjustment;
                rawHR = Math.Clamp(rawHR, 0, 255);
            }

            HeartRateFromSocket = rawHR;
            HeartRateLastUpdate = DateTime.Now;

            _oscHeartRates.Enqueue(rawHR);
            while (_oscHeartRates.Count > Settings.SmoothOSCHeartRateTimeSpan)
                _oscHeartRates.Dequeue();

            GotReadingThisInterval = true;

            if (ViewModel.Instance.IntgrHeartRate_OSC)
            {
                SendHRToOSC(true);
            }
        }

        private async Task HeartRateMonitoringLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[1024];

            try
            {
                while (_webSocket != null && _webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    WebSocketReceiveResult result;
                    try
                    {
                        result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
                    }
                    catch (WebSocketException wex)
                    {
                        Logging.WriteInfo($"WebSocketException: {wex.Message}");
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (IOException ioex)
                    {
                        Logging.WriteInfo($"IOException while reading from WebSocket: {ioex.Message}");
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken).ConfigureAwait(false);
                        break;
                    }

                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    HandleHeartRateMessage(message);
                }
            }
            finally
            {
                if (ShouldStartMonitoring() && !isMonitoringStarted)
                {
                    await Task.Delay(5000).ConfigureAwait(false);
                    await Application.Current.Dispatcher.InvokeAsync(() => StartMonitoringHeartRateAsync()).Task.ConfigureAwait(false);
                }
            }
        }

        private int ParseHeartRateFromMessage(string message)
        {
            try
            {
                var json = JsonConvert.DeserializeObject<dynamic>(message);
                return (int)json.data.heart_rate;
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
                return -1;
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

            OSCSender.SendOscParam($"{baseAddress}_Ones", ones);
            OSCSender.SendOscParam($"{baseAddress}_Tens", tens);
            OSCSender.SendOscParam($"{baseAddress}_Hundreds", hundreds);
        }

        /// <summary>
        /// Send HR and associated parameters to the avatar via OSC.
        /// </summary>
        /// <param name="isHRBeat">True if this is triggered by a new HR reading, false if a fallback update.</param>
        private void SendHRToOSC(bool isHRBeat)
        {
            if (!ViewModel.Instance.IntgrHeartRate_OSC) return;

            bool isHRConnected = ViewModel.Instance.PulsoidAuthConnected;
            bool isHRActive = PulsoidDeviceOnline;

            int hrValueForOSC = GetOSCHeartRate();
            if (hrValueForOSC <= 0) return;

            float hrPercent = hrValueForOSC / 255f;
            float fullHRPercent = (hrValueForOSC / 127.5f) - 1f;

            OSCSender.SendOscParam("/avatar/parameters/isHRConnected", isHRConnected);
            OSCSender.SendOscParam("/avatar/parameters/isHRActive", isHRActive);
            OSCSender.SendOscParam("/avatar/parameters/isHRBeat", isHRBeat);
            OSCSender.SendOscParam("/avatar/parameters/HRPercent", hrPercent);
            OSCSender.SendOscParam("/avatar/parameters/FullHRPercent", fullHRPercent);
            OSCSender.SendOscParam("/avatar/parameters/HR", hrValueForOSC);

            if (!Settings.DisableLegacySupport)
            {
                int ones = hrValueForOSC % 10;
                int tens = (hrValueForOSC / 10) % 10;
                int hundreds = hrValueForOSC / 100;

                OSCSender.SendOscParam("/avatar/parameters/onesHR", ones);
                OSCSender.SendOscParam("/avatar/parameters/tensHR", tens);
                OSCSender.SendOscParam("/avatar/parameters/hundredsHR", hundreds);
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

            OSCSender.SendOscParam("/avatar/parameters/MCB_Heartrate_Hot", isHot);
            OSCSender.SendOscParam("/avatar/parameters/MCB_Heartrate_Sleepy", isSleepy);
            OSCSender.SendOscParam("/avatar/parameters/MCB_Heartrate_TrendUp", trendUp);
            OSCSender.SendOscParam("/avatar/parameters/MCB_Heartrate_TrendDown", trendDown);

            if (!Settings.SentMCBHeartrateInfoLegacy)
            {
                OSCSender.SendOscParam("/avatar/parameters/MCB_Heartrate_Min", PulsoidStatistics.minimum_beats_per_minute);
                OSCSender.SendOscParam("/avatar/parameters/MCB_Heartrate_Max", PulsoidStatistics.maximum_beats_per_minute);
                OSCSender.SendOscParam("/avatar/parameters/MCB_Heartrate_Avg", PulsoidStatistics.average_beats_per_minute);
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
            if (_cts != null || isMonitoringStarted) return;

            isMonitoringStarted = true;
            string accessToken = ViewModel.Instance.PulsoidAccessTokenOAuth;
            if (string.IsNullOrEmpty(accessToken))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    isMonitoringStarted = false;
                    PulsoidAccessError = true;
                    TriggerPulsoidAuthConnected(false);
                    PulsoidAccessErrorTxt = "No Pulsoid connection found. Please connect with the Pulsoid Authentication server.";
                });
                return;
            }

            bool isTokenValid = await PulsoidOAuthHandler.Instance.ValidateTokenAsync(accessToken).ConfigureAwait(false);
            if (!isTokenValid)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    isMonitoringStarted = false;
                    PulsoidAccessError = true;
                    TriggerPulsoidAuthConnected(false);
                    PulsoidAccessErrorTxt = "Expired access token. Please reconnect.";
                });
                return;
            }

            _cts = new CancellationTokenSource();
            UpdateFormattedHeartRateText();

            try
            {
                await ConnectToWebSocketAsync(accessToken, _cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    PulsoidAccessError = true;
                    PulsoidAccessErrorTxt = ex.Message;
                });
                Logging.WriteException(ex);
            }
        }

        private void StopMonitoringHeartRateAsync()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            if (_webSocket != null && _webSocket.State == WebSocketState.Open)
            {
                try
                {
                    _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None).Wait();
                }
                catch { /* Ignore exceptions on closing */ }
                _webSocket.Dispose();
                _webSocket = null;
            }

            if (_processDataTimer.Enabled)
                _processDataTimer.Stop();

            isMonitoringStarted = false;
        }

        private void UpdateHeartRateIcon(int hr)
        {
            if (HeartRate != hr)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    HeartRate = hr;
                });
            }
            if (Settings.MagicHeartRateIcons)
            {
                Settings.HeartRateIcon = Settings.HeartIcons[Settings.CurrentHeartIconIndex];
                Settings.CurrentHeartIconIndex = (Settings.CurrentHeartIconIndex + 1) % Settings.HeartIcons.Count;
            }

            if (Settings.ShowTemperatureText)
            {
                if (hr < Settings.LowTemperatureThreshold)
                {
                    Settings.HeartRateIcon = Settings.HeartRateIcon + FormattedLowHeartRateText;
                }
                else if (hr >= Settings.HighTemperatureThreshold)
                {
                    Settings.HeartRateIcon = Settings.HeartRateIcon + FormattedHighHeartRateText;
                }
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

        public void DisconnectSession()
        {
            StopMonitoringHeartRateAsync();
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
                displayTextBuilder.Append(Settings.HeartRateIcon);
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
                    statsList[i] = DataController.TransformToSuperscript(statsList[i]);
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

        public bool IsRelevantPropertyChange(string propertyName)
        {
            return propertyName == nameof(ViewModel.Instance.IntgrHeartRate) ||
                   propertyName == nameof(ViewModel.Instance.IsVRRunning) ||
                   propertyName == nameof(ViewModel.Instance.IntgrHeartRate_VR) ||
                   propertyName == nameof(ViewModel.Instance.IntgrHeartRate_DESKTOP) ||
                   propertyName == nameof(ViewModel.Instance.IntgrHeartRate_OSC) ||
                   propertyName == nameof(ViewModel.Instance.PulsoidAccessTokenOAuthEncrypted) ||
                   propertyName == nameof(ViewModel.Instance.PulsoidAuthConnected) ||
                   propertyName == nameof(ViewModel.Instance.PulsoidAccessTokenOAuth);
        }

        public void OnApplicationClosing()
        {
            Settings.SaveSettings();
        }

        public async void ProcessData()
        {
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
                await FetchPulsoidStatisticsAsync(ViewModel.Instance.PulsoidAccessTokenOAuth).ConfigureAwait(false);
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

            if (ViewModel.Instance.IntgrHeartRate_OSC && !GotReadingThisInterval)
            {
                SendHRToOSC(false);
            }

            ResetIntervalFlag();
        }

        public void PropertyChangedHandler(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.HeartRateScanInterval))
            {
                _processDataTimer.Interval = Settings.HeartRateScanInterval * 1000;
                return;
            }

            if (IsRelevantPropertyChange(e.PropertyName))
            {
                CheckMonitoringConditions();
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
            return ViewModel.Instance.IntgrHeartRate && ViewModel.Instance.IsVRRunning && ViewModel.Instance.IntgrHeartRate_VR ||
                   ViewModel.Instance.IntgrHeartRate && !ViewModel.Instance.IsVRRunning && ViewModel.Instance.IntgrHeartRate_DESKTOP ||
                   ViewModel.Instance.IntgrHeartRate_OSC;
        }

        public void TriggerPulsoidAuthConnected(bool newValue)
        {
            bool currentvalue = ViewModel.Instance.PulsoidAuthConnected;
            if (newValue != currentvalue)
            {
                ViewModel.Instance.PulsoidAuthConnected = newValue;
            }
        }

        public void UpdateFormattedHeartRateText()
        {
            FormattedLowHeartRateText = DataController.TransformToSuperscript(Settings.LowHeartRateText);
            FormattedHighHeartRateText = DataController.TransformToSuperscript(Settings.HighHeartRateText);
        }

    }


    public class PulsoidTrendSymbolSet
    {
        public string CombinedTrendSymbol => $"{UpwardTrendSymbol} - {DownwardTrendSymbol}";
        public string DownwardTrendSymbol { get; set; } = "↓";
        public string UpwardTrendSymbol { get; set; } = "↑";
    }

    /// <summary>
    /// Handles Pulsoid OAuth token validation and browser-based authentication flow.
    /// </summary>
    public class PulsoidOAuthHandler : IDisposable
    {
        private static readonly Lazy<PulsoidOAuthHandler> lazyInstance =
            new Lazy<PulsoidOAuthHandler>(() => new PulsoidOAuthHandler());
        private bool disposed = false;

        private readonly HttpClient httpClient = new HttpClient();
        private HttpListener httpListener;
        private readonly object listenerLock = new object();
        private HttpListener secondListener;

        private PulsoidOAuthHandler() { }

        private async Task SendBrowserCloseResponseAsync(HttpListenerResponse response)
        {
            const string responseString = @"
    <html>
        <head>
            <script type='text/javascript'>
                var fragment = window.location.hash.substring(1);
                var xhttp = new XMLHttpRequest();
                xhttp.open('POST', 'http://localhost:7385/', true);
                xhttp.send(fragment);

                window.location.replace('https://pulsoid.net/ui/integrations');
            </script>
        </head>
        <body></body>
    </html>";

            var buffer = Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    StopListeners();
                    httpClient.Dispose();
                }
                disposed = true;
            }
        }

        public async Task<string> AuthenticateUserAsync(string authorizationEndpoint)
        {
            try
            {
                string token = null;

                if (httpListener == null || secondListener == null)
                    throw new InvalidOperationException("Listeners are not started");

                Process.Start(new ProcessStartInfo { FileName = authorizationEndpoint, UseShellExecute = true });

                var context1 = await httpListener.GetContextAsync();
                await SendBrowserCloseResponseAsync(context1.Response);

                var context2 = await secondListener.GetContextAsync();
                using (var reader = new StreamReader(context2.Request.InputStream))
                {
                    token = await reader.ReadToEndAsync();
                }

                return token;
            }
            catch (Exception ex)
            {
                Logging.WriteException(new Exception("Authentication failed.", ex), MSGBox: true);
                return null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public static Dictionary<string, string> ParseQueryString(string queryString)
        {
            var nvc = HttpUtility.ParseQueryString(queryString);
            return nvc.AllKeys.ToDictionary(k => k, k => nvc[k]);
        }

        public void StartListeners()
        {
            lock (listenerLock)
            {
                if (httpListener == null)
                {
                    httpListener = new HttpListener { Prefixes = { "http://localhost:7384/" } };
                    httpListener.Start();
                }

                if (secondListener == null)
                {
                    secondListener = new HttpListener { Prefixes = { "http://localhost:7385/" } };
                    secondListener.Start();
                }
            }
        }

        public void StopListeners()
        {
            lock (listenerLock)
            {
                httpListener?.Stop();
                httpListener?.Close();
                httpListener = null;

                secondListener?.Stop();
                secondListener?.Close();
                secondListener = null;
            }
        }

        public async Task<bool> ValidateTokenAsync(string accessToken)
        {
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, "https://dev.pulsoid.net/api/v1/token/validate"))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    var response = await httpClient.SendAsync(request).ConfigureAwait(false);

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var tokenInfo = JsonConvert.DeserializeObject<TokenInfo>(content);

                        var requiredScopes = new[] { "data:heart_rate:read", "profile:read", "data:statistics:read" };
                        return requiredScopes.All(scope => tokenInfo.Scopes.Contains(scope));
                    }
                    else
                    {
                        Logging.WriteInfo($"Token validation failed with status code {response.StatusCode}");
                        return false;
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                Logging.WriteException(ex, MSGBox: false);
                return false;
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
                return false;
            }
        }

        public static PulsoidOAuthHandler Instance => lazyInstance.Value;

        private class TokenInfo
        {
            [JsonProperty("scopes")]
            public string[] Scopes { get; set; }
        }
    }
}
