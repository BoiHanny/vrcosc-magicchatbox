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
using vrcosc_magicchatbox.ViewModels;
using System.Windows;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.DataAndSecurity;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System.Net.WebSockets;
using System.Text;
using vrcosc_magicchatbox.ViewModels.Models;



namespace vrcosc_magicchatbox.Classes.Modules
{
    public partial class PulsoidModuleSettings : ObservableObject
    {
        private const string SettingsFileName = "PulsoidModuleSettings.json";

        [ObservableProperty]
        private List<PulsoidTrendSymbolSet> pulsoidTrendSymbols = new();

        [ObservableProperty]
        private int currentHeartIconIndex = 0;

        [ObservableProperty]
        private string heartRateTrendIndicator = string.Empty;

        [ObservableProperty]
        private bool enableHeartRateOfflineCheck = true;

        [ObservableProperty]
        private int unchangedHeartRateTimeoutInSec = 30;

        [ObservableProperty]
        private bool smoothHeartRate = true;

        [ObservableProperty]
        private int smoothHeartRateTimeSpan = 4; 

        [ObservableProperty]
        private bool smoothOSCHeartRate = true; 

        [ObservableProperty]
        private int smoothOSCHeartRateTimeSpan = 4; 

        [ObservableProperty]
        private bool showHeartRateTrendIndicator = true;

        [ObservableProperty]
        private int heartRateTrendIndicatorSampleRate = 4;

        [ObservableProperty]
        private double heartRateTrendIndicatorSensitivity = 0.65;

        [ObservableProperty]
        private bool hideCurrentHeartRate = false;

        [ObservableProperty]
        private bool showTemperatureText = true;

        [ObservableProperty]
        private bool magicHeartRateIcons = true;

        [ObservableProperty]
        private bool magicHeartIconPrefix = true;

        [ObservableProperty]
        private List<string> heartIcons = new List<string> { "❤️", "💖", "💗", "💙", "💚", "💛", "💜" };

        [ObservableProperty]
        private string heartRateIcon = "❤️";

        [ObservableProperty]
        private bool separateTitleWithEnter = false;

        [ObservableProperty]
        private int lowTemperatureThreshold = 60;

        [ObservableProperty]
        private int highTemperatureThreshold = 100;

        [ObservableProperty]
        private bool applyHeartRateAdjustment = false;

        [ObservableProperty]
        private int heartRateAdjustment = -5;

        [ObservableProperty]
        private int heartRateScanInterval = 1;

        [ObservableProperty]
        private string lowHeartRateText = "sleepy";

        [ObservableProperty]
        private string highHeartRateText = "hot";

        [ObservableProperty]
        private bool showBPMSuffix = false;

        [ObservableProperty]
        private string currentHeartRateTitle = "Heart Rate";

        [ObservableProperty]
        private bool heartRateTitle = false;

        [ObservableProperty]
        private PulsoidTrendSymbolSet selectedPulsoidTrendSymbol = new();

        [ObservableProperty]
        private StatisticsTimeRange selectedStatisticsTimeRange = StatisticsTimeRange._24h;

        [ObservableProperty]
        private List<StatisticsTimeRange> statisticsTimeRanges = new();

        [ObservableProperty]
        bool pulsoidStatsEnabled = true;

        [ObservableProperty]
        bool showCalories = false;

        [ObservableProperty]
        bool showAverageHeartRate = true;

        [ObservableProperty]
        bool showMinimumHeartRate = true;

        [ObservableProperty]
        bool showMaximumHeartRate = true;

        [ObservableProperty]
        bool showDuration = false;

        [ObservableProperty]
        bool showStatsTimeRange = false;

        [ObservableProperty]
        bool trendIndicatorBehindStats = true;

        public void SaveSettings()
        {
            var settingsJson = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(GetFullSettingsPath(), settingsJson);
        }

        public static string GetFullSettingsPath()
        {
            return Path.Combine(ViewModel.Instance.DataPath, SettingsFileName);
        }

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

                    if (settings != null)
                    {
                        return settings;
                    }
                    else
                    {
                        Logging.WriteInfo("Failed to deserialize the settings JSON.");
                        return new PulsoidModuleSettings();
                    }
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
        public DateTime MeasuredAt { get; set; }
        public int HeartRate { get; set; }
    }

    public partial class PulsoidStatisticsResponse
    {

        public int maximum_beats_per_minute { get; set; } = 0;
        public int minimum_beats_per_minute { get; set; } = 0;
        public int average_beats_per_minute { get; set; } = 0;
        public int streamed_duration_in_seconds { get; set; } = 0;
        public int calories_burned_in_kcal { get; set; } = 0;
    }

    public partial class PulsoidModule : ObservableObject
    {
        private bool isMonitoringStarted = false;
        private ClientWebSocket _webSocket;
        private CancellationTokenSource _cts;

        // For normal smoothing (time-based)
        private readonly Queue<Tuple<DateTime, int>> _heartRates = new();

        // For OSC smoothing (count-based)
        private readonly Queue<int> _oscHeartRates = new();

        private readonly Queue<int> _heartRateHistory = new();
        private int HeartRateFromSocket = 0;
        private System.Timers.Timer _processDataTimer;
        private int _previousHeartRate = -1;
        private int _unchangedHeartRateCount = 0;
        public PulsoidStatisticsResponse PulsoidStatistics;
        private HttpClient _StatisticsClient = new HttpClient();
        private readonly object _fetchLock = new object();
        private bool _isFetchingStatistics = false;

        // Flag to track if a reading arrived this interval
        private bool GotReadingThisInterval = false;

        [ObservableProperty]
        private int heartRate;

        [ObservableProperty]
        private bool pulsoidDeviceOnline = false;

        [ObservableProperty]
        private DateTime heartRateLastUpdate = DateTime.Now;

        [ObservableProperty]
        private string formattedLowHeartRateText;

        [ObservableProperty]
        private string formattedHighHeartRateText;

        [ObservableProperty]
        private bool pulsoidAccessError = false;

        [ObservableProperty]
        private string pulsoidAccessErrorTxt = string.Empty;

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

        private async Task FetchPulsoidStatisticsAsync(string accessToken)
        {
            lock (_fetchLock)
            {
                if (_isFetchingStatistics)
                {
                    return;
                }
                _isFetchingStatistics = true;
            }

            try
            {
                string timeRangeDescription = Settings.SelectedStatisticsTimeRange.GetDescription();
                string requestUri = $"https://dev.pulsoid.net/api/v1/statistics?time_range={timeRangeDescription}";

                try
                {
                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUri);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    request.Headers.Add("User-Agent", "Vrcosc-MagicChatbox");
                    request.Headers.Add("Accept", "application/json");

                    HttpResponseMessage response = await _StatisticsClient.SendAsync(request);

                    if (!response.IsSuccessStatusCode)
                    {
                        string errorContent = await response.Content.ReadAsStringAsync();
                        Debug.WriteLine($"Error fetching Pulsoid statistics: {response.StatusCode}, Content: {errorContent}");
                        return;
                    }

                    string content = await response.Content.ReadAsStringAsync();
                    PulsoidStatistics = JsonConvert.DeserializeObject<PulsoidStatisticsResponse>(content);

                    if (PulsoidStatistics != null && Settings.ApplyHeartRateAdjustment)
                    {
                        PulsoidStatistics.maximum_beats_per_minute += Settings.HeartRateAdjustment;
                        PulsoidStatistics.minimum_beats_per_minute += Settings.HeartRateAdjustment;
                        PulsoidStatistics.average_beats_per_minute += Settings.HeartRateAdjustment;
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
            }
            finally
            {
                lock (_fetchLock)
                {
                    _isFetchingStatistics = false;
                }
            }
        }

        public void OnApplicationClosing()
        {
            Settings.SaveSettings();
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

        public void UpdateFormattedHeartRateText()
        {
            FormattedLowHeartRateText = DataController.TransformToSuperscript(Settings.LowHeartRateText);
            FormattedHighHeartRateText = DataController.TransformToSuperscript(Settings.HighHeartRateText);
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

        private void CheckMonitoringConditions()
        {
            if (ShouldStartMonitoring() && !isMonitoringStarted)
            {
                StartMonitoringHeartRateAsync();
            }
            else if (!ShouldStartMonitoring())
            {
                StopMonitoringHeartRateAsync();
            }
        }

        public bool ShouldStartMonitoring()
        {
            return ViewModel.Instance.IntgrHeartRate && ViewModel.Instance.IsVRRunning && ViewModel.Instance.IntgrHeartRate_VR ||
                   ViewModel.Instance.IntgrHeartRate && !ViewModel.Instance.IsVRRunning && ViewModel.Instance.IntgrHeartRate_DESKTOP || ViewModel.Instance.IntgrHeartRate_OSC;
        }

        public bool IsRelevantPropertyChange(string propertyName)
        {
            return propertyName == nameof(ViewModel.Instance.IntgrHeartRate) ||
                   propertyName == nameof(ViewModel.Instance.IsVRRunning) ||
                   propertyName == nameof(ViewModel.Instance.IntgrHeartRate_VR) ||
                   propertyName == nameof(ViewModel.Instance.IntgrHeartRate_DESKTOP) ||
                   propertyName == nameof(ViewModel.Instance.IntgrHeartRate_OSC) ||
                   propertyName == nameof(ViewModel.Instance.PulsoidAccessTokenOAuthEncrypted) || propertyName == nameof(ViewModel.Instance.PulsoidAuthConnected) || propertyName == nameof(ViewModel.Instance.PulsoidAccessTokenOAuth);
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
                _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None).Wait();
                _webSocket.Dispose();
                _webSocket = null;
            }

            if (_processDataTimer.Enabled)
                _processDataTimer.Stop();

            isMonitoringStarted = false;
        }

        private async Task ConnectToWebSocketAsync(string accessToken, CancellationToken cancellationToken)
        {
            _webSocket = new ClientWebSocket();
            _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            _webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(5);

            try
            {
                await _webSocket.ConnectAsync(new Uri("wss://dev.pulsoid.net/api/v1/data/real_time"), cancellationToken);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    PulsoidAccessError = false;
                    PulsoidAccessErrorTxt = "";
                });

                _processDataTimer.Start();

                await HeartRateMonitoringLoopAsync(cancellationToken);
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

        public void DisconnectSession()
        {
            StopMonitoringHeartRateAsync();
        }

        private async void StartMonitoringHeartRateAsync()
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
                    PulsoidAccessErrorTxt = "No Pulsoid connection found. Please connect with the Pulsoid Authentication server";
                });
                return;
            }

            bool isTokenValid = await PulsoidOAuthHandler.Instance.ValidateTokenAsync(accessToken);
            if (!isTokenValid)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    isMonitoringStarted = false;
                    PulsoidAccessError = true;
                    TriggerPulsoidAuthConnected(false);
                    PulsoidAccessErrorTxt = "Expired access token. Please reconnect with the Pulsoid Authentication server";
                });

                return;
            }

            _cts = new CancellationTokenSource();
            UpdateFormattedHeartRateText();
            await ConnectToWebSocketAsync(accessToken, _cts.Token);
        }

        public void TriggerPulsoidAuthConnected(bool newValue)
        {
            bool currentvalue = ViewModel.Instance.PulsoidAuthConnected;
            if (newValue == currentvalue) return;
            else
            {
                ViewModel.Instance.PulsoidAuthConnected = newValue;
            }
        }

        private int ParseHeartRateFromMessage(string message)
        {
            try
            {
                var json = JsonConvert.DeserializeObject<dynamic>(message);
                return json.data.heart_rate;
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
                return -1;
            }
        }

        public async void ProcessData()
        {
            if (HeartRateFromSocket <= 0)
            {
                PulsoidDeviceOnline = false;
                ResetIntervalFlag();
                return;
            }
            else
            {
                PulsoidDeviceOnline = true;
            }

            int hr = HeartRateFromSocket;

            if (Settings.PulsoidStatsEnabled)
                await FetchPulsoidStatisticsAsync(ViewModel.Instance.PulsoidAccessTokenOAuth).ConfigureAwait(false);


            // Unchanged HR logic
            if (hr == _previousHeartRate)
            {
                _unchangedHeartRateCount++;
            }
            else
            {
                _unchangedHeartRateCount = 0;
                _previousHeartRate = hr;
            }

            if (Settings.EnableHeartRateOfflineCheck && _unchangedHeartRateCount >= Settings.UnchangedHeartRateTimeoutInSec)
            {
                PulsoidDeviceOnline = false;
                ResetIntervalFlag();
                return;
            }
            else
            {
                PulsoidDeviceOnline = true;
            }

            // Normal smoothing (time-based)
            if (Settings.SmoothHeartRate)
            {
                _heartRates.Enqueue(new Tuple<DateTime, int>(DateTime.UtcNow, hr));
                while (_heartRates.Count > 0 && DateTime.UtcNow - _heartRates.Peek().Item1 > TimeSpan.FromSeconds(Settings.SmoothHeartRateTimeSpan))
                {
                    _heartRates.Dequeue();
                }
                hr = (int)_heartRates.Average(t => t.Item2);
            }

            // Trend indicator
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

            // Magic heart rate icons
            if (Settings.MagicHeartRateIcons)
            {
                Settings.HeartRateIcon = Settings.HeartIcons[Settings.CurrentHeartIconIndex];
                Settings.CurrentHeartIconIndex = (Settings.CurrentHeartIconIndex + 1) % Settings.HeartIcons.Count;
            }

            if (Settings.ShowTemperatureText)
            {
                if (hr < Settings.LowTemperatureThreshold)
                {
                    Settings.HeartRateIcon = Settings.HeartIcons[Settings.CurrentHeartIconIndex] + FormattedLowHeartRateText;
                }
                else if (hr >= Settings.HighTemperatureThreshold)
                {
                    Settings.HeartRateIcon = Settings.HeartIcons[Settings.CurrentHeartIconIndex] + FormattedHighHeartRateText;
                }
            }
            else
                Settings.HeartRateIcon = Settings.HeartIcons[Settings.CurrentHeartIconIndex];

            if (HeartRate != hr)
            {
                HeartRate = hr;
            }

            // If no reading arrived this interval, we still send an OSC update now if enabled
            if (ViewModel.Instance.IntgrHeartRate_OSC && !GotReadingThisInterval)
            {
                // This ensures no large gap without updating OSC
                SendHRToOSC(false);
            }

            // Reset for next interval
            ResetIntervalFlag();
        }

        private void ResetIntervalFlag()
        {
            GotReadingThisInterval = false;
        }

        // Get OSC HR value (applies OSC smoothing if enabled)
        private int GetOSCHeartRate()
        {
            if (!Settings.SmoothOSCHeartRate || _oscHeartRates.Count == 0)
            {
                // No smoothing for OSC or no data yet
                return HeartRateFromSocket;
            }
            else
            {
                // Smoothing for OSC: average the last N values
                return (int)Math.Round(_oscHeartRates.Average());
            }
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

            if (Settings.PulsoidStatsEnabled)
            {
                List<string> statsList = new List<string>();

                if (PulsoidStatistics != null)
                {
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
                        // Handle gracefully, e.g., attempt reconnect or just break
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        // Operation was canceled because StopMonitoring was called
                        break;
                    }
                    catch (IOException ioex)
                    {
                        Logging.WriteInfo($"IOException while reading from WebSocket: {ioex.Message}");
                        // Similarly handle gracefully
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
                    // Attempt to reconnect after a short delay
                    await Task.Delay(5000).ConfigureAwait(false);
                    await Application.Current.Dispatcher.InvokeAsync(StartMonitoringHeartRateAsync);
                }
            }
        }

        private void HandleHeartRateMessage(string message)
        {
            int rawHR = ParseHeartRateFromMessage(message);
            if (rawHR == -1) return;

            // Apply adjustment
            if (Settings.ApplyHeartRateAdjustment)
            {
                rawHR += Settings.HeartRateAdjustment;
                rawHR = Math.Clamp(rawHR, 0, 255);
            }

            HeartRateFromSocket = rawHR;
            HeartRateLastUpdate = DateTime.Now;

            // Keep track for OSC smoothing
            _oscHeartRates.Enqueue(rawHR);
            while (_oscHeartRates.Count > Settings.SmoothOSCHeartRateTimeSpan)
                _oscHeartRates.Dequeue();

            // We got a reading this interval
            GotReadingThisInterval = true;

            // Immediately send OSC data if enabled
            if (ViewModel.Instance.IntgrHeartRate_OSC)
            {
                // isHRBeat = true for immediate updates
                SendHRToOSC(true);
            }
        }

        // Send OSC parameters method
        private void SendHRToOSC(bool isHRBeat)
        {
            if (!ViewModel.Instance.IntgrHeartRate_OSC) return; // Only send if enabled

            bool isHRConnected = ViewModel.Instance.PulsoidAuthConnected; // Authenticated and token valid
            bool isHRActive = PulsoidDeviceOnline;                       // Device considered online

            int hrValueForOSC = GetOSCHeartRate();
            if (hrValueForOSC <= 0) return;

            // Map HR to [0,1] for HRPercent
            float hrPercent = (float)hrValueForOSC / 255.0f;
            // FullHRPercent maps 0->-1, 255->1
            float fullHRPercent = ((float)hrValueForOSC / 127.5f) - 1.0f;

            // Send parameters over OSC
            OSCSender.SendOscParam("/avatar/parameters/isHRConnected", isHRConnected);
            OSCSender.SendOscParam("/avatar/parameters/isHRActive", isHRActive);
            OSCSender.SendOscParam("/avatar/parameters/isHRBeat", isHRBeat);
            OSCSender.SendOscParam("/avatar/parameters/HRPercent", hrPercent);
            OSCSender.SendOscParam("/avatar/parameters/FullHRPercent", fullHRPercent);
            OSCSender.SendOscParam("/avatar/parameters/HR", hrValueForOSC);
        }
    }

    public class PulsoidTrendSymbolSet
    {
        public string UpwardTrendSymbol { get; set; } = "↑";
        public string DownwardTrendSymbol { get; set; } = "↓";
        public string CombinedTrendSymbol => $"{UpwardTrendSymbol} - {DownwardTrendSymbol}";
    }


    // No changes to PulsoidOAuthHandler or other classes required, unless needed for consistency.

    public class PulsoidOAuthHandler : IDisposable
    {
        private static readonly Lazy<PulsoidOAuthHandler> lazyInstance =
            new Lazy<PulsoidOAuthHandler>(() => new PulsoidOAuthHandler());

        public static PulsoidOAuthHandler Instance => lazyInstance.Value;

        private readonly HttpClient httpClient = new HttpClient();
        private HttpListener httpListener;
        private HttpListener secondListener;
        private bool disposed = false;
        private readonly object listenerLock = new object();

        private PulsoidOAuthHandler() { }

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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
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

                // Intercept OAuth2 redirect and return HTML/JS page
                var context1 = await httpListener.GetContextAsync();
                await SendBrowserCloseResponseAsync(context1.Response);

                // Second listener to get the fragment
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


        public async Task<bool> ValidateTokenAsync(string accessToken)
        {
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, "https://dev.pulsoid.net/api/v1/token/validate"))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    var response = await httpClient.SendAsync(request);

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var tokenInfo = JsonConvert.DeserializeObject<TokenInfo>(content);

                        var requiredScopes = new[] { "data:heart_rate:read", "profile:read", "data:statistics:read" };
                        return requiredScopes.All(scope => tokenInfo.Scopes.Contains(scope));
                    }

                    return false;
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(new Exception("Token validation failed.", ex), MSGBox: false);
                return false;
            }
        }


        private class TokenInfo
        {
            [JsonProperty("scopes")]
            public string[] Scopes { get; set; }
        }

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

                // Redirect to Pulsoid integrations page
                window.location.replace('https://pulsoid.net/ui/integrations');
            </script>
        </head>
        <body></body>
    </html>";

            var buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }

        public static Dictionary<string, string> ParseQueryString(string queryString)
        {
            var nvc = HttpUtility.ParseQueryString(queryString);
            return nvc.AllKeys.ToDictionary(k => k, k => nvc[k]);
        }
    }


}
