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
        private bool showHeartRateTrendIndicator = true;

        [ObservableProperty]
        private int heartRateTrendIndicatorSampleRate = 5;

        [ObservableProperty]
        private double heartRateTrendIndicatorSensitivity = 0.65;

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
        bool showCalories = true;

        [ObservableProperty]
        bool showAverageHeartRate = true;

        [ObservableProperty]
        bool showMinimumHeartRate = true;

        [ObservableProperty]
        bool showMaximumHeartRate = true;

        [ObservableProperty]
        bool showDuration = true;

        [ObservableProperty]
        bool showStatsTimeRange = false;

        [ObservableProperty]
        bool trendIndicatorBehindStats = true;

        [ObservableProperty]
        bool showMinHeartRate = true;


        public void SaveSettings()
        {
            var settingsJson = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(GetFullSettingsPath(), settingsJson);
        }

        public static string GetFullSettingsPath()
        {
            return Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vrcosc-MagicChatbox"), SettingsFileName);
        }

        public static PulsoidModuleSettings LoadSettings()
        {
            var settingsPath = GetFullSettingsPath();

            if (File.Exists(settingsPath))
            {
                string settingsJson = File.ReadAllText(settingsPath);

                if (string.IsNullOrWhiteSpace(settingsJson) || settingsJson.All(c => c == '\0'))
                {
                    Logging.WriteInfo("he settings JSON file is empty or corrupted.");
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
        private readonly Queue<Tuple<DateTime, int>> _heartRates = new();
        private readonly Queue<int> _heartRateHistory = new();
        private int HeartRateFromSocket = 0;
        private System.Timers.Timer _processDataTimer;
        private int _previousHeartRate = -1;
        private int _unchangedHeartRateCount = 0;
        public PulsoidStatisticsResponse PulsoidStatistics;
        private HttpClient _StatisticsClient = new HttpClient();
        private readonly object _fetchLock = new object();
        private bool _isFetchingStatistics = false;

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
            _processDataTimer.Elapsed += (sender, e) => Application.Current.Dispatcher.Invoke(ProcessData);
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

                    if (Settings.ApplyHeartRateAdjustment)
                    {
                        PulsoidStatistics = JsonConvert.DeserializeObject<PulsoidStatisticsResponse>(content);
                        PulsoidStatistics.maximum_beats_per_minute += Settings.HeartRateAdjustment;
                        PulsoidStatistics.minimum_beats_per_minute += Settings.HeartRateAdjustment;
                        PulsoidStatistics.average_beats_per_minute += Settings.HeartRateAdjustment;
                    }
                    else

                        PulsoidStatistics = JsonConvert.DeserializeObject<PulsoidStatisticsResponse>(content);
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
                new PulsoidTrendSymbolSet { UpwardTrendSymbol = "⤴️", DownwardTrendSymbol = "⤵️" },
                new PulsoidTrendSymbolSet { UpwardTrendSymbol = "⬆", DownwardTrendSymbol = "⬇" },
                new PulsoidTrendSymbolSet { UpwardTrendSymbol = "↑", DownwardTrendSymbol = "↓" },
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
                if (ShouldStartMonitoring() && !isMonitoringStarted)
                {
                    StartMonitoringHeartRateAsync();
                }
                else if (!ShouldStartMonitoring())
                {
                    StopMonitoringHeartRateAsync();
                }
            }
        }




        public bool ShouldStartMonitoring()
        {
            return ViewModel.Instance.IntgrHeartRate && ViewModel.Instance.IsVRRunning && ViewModel.Instance.IntgrHeartRate_VR ||
                   ViewModel.Instance.IntgrHeartRate && !ViewModel.Instance.IsVRRunning && ViewModel.Instance.IntgrHeartRate_DESKTOP;
        }

        public bool IsRelevantPropertyChange(string propertyName)
        {
            return propertyName == nameof(ViewModel.Instance.IntgrHeartRate) ||
                   propertyName == nameof(ViewModel.Instance.IsVRRunning) ||
                   propertyName == nameof(ViewModel.Instance.IntgrHeartRate_VR) ||
                   propertyName == nameof(ViewModel.Instance.IntgrHeartRate_DESKTOP) ||
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
            { StopMonitoringHeartRateAsync(); }

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

        public void ProcessData()
        {
            if(HeartRateFromSocket <= 0)
            {
                PulsoidDeviceOnline = false;
                return;
            }
            else
                {
                PulsoidDeviceOnline = true;
            }

            int heartRate = HeartRateFromSocket;

            if(Settings.PulsoidStatsEnabled)
                _ = Task.Run(() => FetchPulsoidStatisticsAsync(ViewModel.Instance.PulsoidAccessTokenOAuth));

            // New logic to handle unchanged heart rate readings
            if (heartRate == _previousHeartRate)
            {
                _unchangedHeartRateCount++;
            }
            else
            {
                _unchangedHeartRateCount = 0; // Reset if the heart rate has changed
                _previousHeartRate = heartRate; // Update previous heart rate
            }

            // Determine if the Pulsoid device should be considered offline
            if (Settings.EnableHeartRateOfflineCheck && _unchangedHeartRateCount >= Settings.UnchangedHeartRateTimeoutInSec)
            {
                PulsoidDeviceOnline = false; // Set the device as offline
                return;
            }
            else
            {
                PulsoidDeviceOnline = true; // Otherwise, consider it online

            }

            // If SmoothHeartRate_v1 is true, calculate and use average heart rate
            if (Settings.SmoothHeartRate)
            {
                // Record the heart rate with the current time
                _heartRates.Enqueue(new Tuple<DateTime, int>(DateTime.UtcNow, heartRate));

                // Remove old data
                while (_heartRates.Count > 0 && DateTime.UtcNow - _heartRates.Peek().Item1 > TimeSpan.FromSeconds(Settings.SmoothHeartRateTimeSpan))
                {
                    _heartRates.Dequeue();
                }

                // Calculate average heart rate over the defined timespan
                heartRate = (int)_heartRates.Average(t => t.Item2);
            }

            // Record the heart rate for trend analysis
            if (Settings.ShowHeartRateTrendIndicator)
            {
                // Only keep the last N heart rates, where N is HeartRateTrendIndicatorSampleRate
                if (_heartRateHistory.Count >= Settings.HeartRateTrendIndicatorSampleRate)
                {
                    _heartRateHistory.Dequeue();
                }

                _heartRateHistory.Enqueue(heartRate);

                // Update the trend indicator
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
            // Update the heart rate icon
            if (Settings.MagicHeartRateIcons)
            {
                // Always cycle through heart icons
                Settings.HeartRateIcon = Settings.HeartIcons[Settings.CurrentHeartIconIndex];
                Settings.CurrentHeartIconIndex = (Settings.CurrentHeartIconIndex + 1) % Settings.HeartIcons.Count;
            }
            // Append additional icons based on heart rate, if the toggle is enabled
            if (Settings.ShowTemperatureText)
            {
                if (heartRate < Settings.LowTemperatureThreshold)
                {
                    Settings.HeartRateIcon = Settings.HeartIcons[Settings.CurrentHeartIconIndex] + FormattedLowHeartRateText;
                }
                else if (heartRate >= Settings.HighTemperatureThreshold)
                {
                    Settings.HeartRateIcon = Settings.HeartIcons[Settings.CurrentHeartIconIndex] + FormattedHighHeartRateText;
                }
            }
            else
                Settings.HeartRateIcon = Settings.HeartIcons[Settings.CurrentHeartIconIndex];

            if (HeartRate != heartRate)
            {
                HeartRate = heartRate;
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
                displayTextBuilder.Append(Settings.HeartRateIcon + " ");
            }

            displayTextBuilder.Append(HeartRate.ToString());

            if (Settings.ShowBPMSuffix)
            {
                displayTextBuilder.Append(" bpm");
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
                if (Settings.ShowMinimumHeartRate || Settings.ShowMinHeartRate)
                {
                    statsList.Add($"{PulsoidStatistics.minimum_beats_per_minute} Min");
                }
                if (Settings.ShowMaximumHeartRate || Settings.ShowMaximumHeartRate)
                {
                    statsList.Add($"{PulsoidStatistics.maximum_beats_per_minute} Max");
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
            try
            {
                var buffer = new byte[1024];

                while (_webSocket != null && _webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
                    }
                    else
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        int heartRate = ParseHeartRateFromMessage(message);
                        Debug.WriteLine(heartRate);
                        if (heartRate != -1)
                        {
                            // Apply the adjustment if ApplyHeartRateAdjustment is true
                            if (Settings.ApplyHeartRateAdjustment)
                            {
                                heartRate += Settings.HeartRateAdjustment;
                            }

                            HeartRateFromSocket = heartRate;
                            HeartRateLastUpdate = DateTime.Now;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }


    }

    public class PulsoidTrendSymbolSet
    {
        public string UpwardTrendSymbol { get; set; } = "⤴️";
        public string DownwardTrendSymbol { get; set; } = "⤵️";
        public string CombinedTrendSymbol => $"{UpwardTrendSymbol} - {DownwardTrendSymbol}";
    }
    public class PulsoidOAuthHandler
    {
        private static readonly Lazy<PulsoidOAuthHandler> lazyInstance =
            new Lazy<PulsoidOAuthHandler>(() => new PulsoidOAuthHandler());

        public static PulsoidOAuthHandler Instance => lazyInstance.Value;

        private readonly HttpClient httpClient = new HttpClient();
        private HttpListener httpListener;
        private HttpListener secondListener;

        private PulsoidOAuthHandler() { }

        public void StartListeners()
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

        public void StopListeners()
        {
            httpListener?.Stop();
            httpListener = null;
            secondListener?.Stop();
            secondListener = null;
        }

        public async Task<string> AuthenticateUserAsync(string authorizationEndpoint)
        {
            string token = null;

            if (httpListener == null || secondListener == null) throw new InvalidOperationException("Listeners are not started");

            Process.Start(new ProcessStartInfo { FileName = authorizationEndpoint, UseShellExecute = true });

            // Intercept OAuth2 redirect and return HTML/JS page
            var context1 = await httpListener.GetContextAsync();
            await SendBrowserCloseResponseAsync(context1.Response);

            // Second listener to get the fragment
            var context2 = await secondListener.GetContextAsync();
            var reader = new StreamReader(context2.Request.InputStream);
            token = reader.ReadToEnd();

            return token;
        }

        public async Task<bool> ValidateTokenAsync(string accessToken)
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await httpClient.GetAsync("https://dev.pulsoid.net/api/v1/token/validate");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var tokenInfo = JsonConvert.DeserializeObject<TokenInfo>(content);

                var requiredScopes = new[] { "data:heart_rate:read", "profile:read", "data:statistics:read" };
                return requiredScopes.All(scope => tokenInfo.Scopes.Contains(scope));
            }

            return false;
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
