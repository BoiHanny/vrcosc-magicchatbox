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
using Newtonsoft.Json.Linq;
using vrcosc_magicchatbox.DataAndSecurity;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;



namespace vrcosc_magicchatbox.Classes.Modules
{
    public partial class PulsoidModuleSettings : ObservableObject
    {

        private const string SettingsFileName = "PulsoidModuleSettings.json";

        [ObservableProperty]

        List<PulsoidTrendSymbolSet> pulsoidTrendSymbols = new();

        [ObservableProperty]
        PulsoidTrendSymbolSet selectedPulsoidTrendSymbol = new();
        

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

    public partial class PulsoidModule : ObservableObject
    {

        private CancellationTokenSource? _cts;
        private readonly Queue<Tuple<DateTime, int>> _heartRates = new();
        private readonly Queue<int> _heartRateHistory = new();
        private int _previousHeartRate = -1;
        private int _unchangedHeartRateCount = 0;

        [ObservableProperty]
        public PulsoidModuleSettings settings;

        public PulsoidModule()
        {
            Settings = PulsoidModuleSettings.LoadSettings();

            RefreshTrendSymbols();
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
                // If the previously selected symbol exists, select it again
                Settings.SelectedPulsoidTrendSymbol = Settings.PulsoidTrendSymbols.FirstOrDefault(s => s.CombinedTrendSymbol == Settings.SelectedPulsoidTrendSymbol.CombinedTrendSymbol);
            }
            else
            {
                // If it doesn't exist or if there was no selection, default to the first symbol in the list
                Settings.SelectedPulsoidTrendSymbol = Settings.PulsoidTrendSymbols.FirstOrDefault();
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





        public static void UpdateFormattedHeartRateText()
        {
            ViewModel.Instance.FormattedLowHeartRateText = DataController.TransformToSuperscript(ViewModel.Instance.LowHeartRateText);
            ViewModel.Instance.FormattedHighHeartRateText = DataController.TransformToSuperscript(ViewModel.Instance.HighHeartRateText);
        }

        public void PropertyChangedHandler(object sender, PropertyChangedEventArgs e)
        {
            if (IsRelevantPropertyChange(e.PropertyName))
            {
                if (ShouldStartMonitoring())
                {
                    StartMonitoringHeartRateAsync();
                }
                else
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
                   propertyName == nameof(ViewModel.Instance.IntgrHeartRate_DESKTOP);
        }


        private void StopMonitoringHeartRateAsync()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }
        }

        private void StartMonitoringHeartRateAsync()
        {
            if (_cts != null)
            {
                return;
            }

            _cts = new CancellationTokenSource();
            UpdateFormattedHeartRateText();
            Task.Run(async () => await HeartRateMonitoringLoopAsync(_cts.Token), _cts.Token);
        }

        private async Task HeartRateMonitoringLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {

                DateTime startTime = DateTime.UtcNow;

                try
                {
                    if (string.IsNullOrEmpty(ViewModel.Instance.PulsoidAccessTokenOAuth))
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ViewModel.Instance.PulsoidAuthConnected = false;
                        });
                        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                        continue;
                    }
                    int heartRate = await GetHeartRateViaHttpAsync();
                    if (heartRate != -1)
                    {
                        // Check if the heart rate is the same as the previous reading
                        if (heartRate == _previousHeartRate)
                        {
                            _unchangedHeartRateCount++;
                        }
                        else
                        {
                            _unchangedHeartRateCount = 0; // Reset if the heart rate has changed
                            _previousHeartRate = heartRate; // Update previous heart rate
                        }

                        // Check if we should perform the offline check
                        if (ViewModel.Instance.EnableHeartRateOfflineCheck && _unchangedHeartRateCount >= ViewModel.Instance.UnchangedHeartRateLimit)
                        {

                            ViewModel.Instance.PulsoidDeviceOnline = false; // Set the device as offline
                        }
                        else
                        {
                            ViewModel.Instance.PulsoidDeviceOnline = true; // Otherwise, consider it online
                        }
                        // Apply the adjustment if ApplyHeartRateAdjustment is true
                        if (ViewModel.Instance.ApplyHeartRateAdjustment)
                        {
                            heartRate += ViewModel.Instance.HeartRateAdjustment;
                        }

                        // Ensure the adjusted heart rate is not negative
                        heartRate = Math.Max(0, heartRate);

                        // If SmoothHeartRate_v1 is true, calculate and use average heart rate
                        if (ViewModel.Instance.SmoothHeartRate_v1)
                        {
                            // Record the heart rate with the current time
                            _heartRates.Enqueue(new Tuple<DateTime, int>(DateTime.UtcNow, heartRate));

                            // Remove old data
                            while (_heartRates.Count > 0 && DateTime.UtcNow - _heartRates.Peek().Item1 > TimeSpan.FromSeconds(ViewModel.Instance.SmoothHeartRateTimeSpan))
                            {
                                _heartRates.Dequeue();
                            }

                            // Calculate average heart rate over the defined timespan
                            heartRate = (int)_heartRates.Average(t => t.Item2);
                        }

                        // Record the heart rate for trend analysis
                        if (ViewModel.Instance.ShowHeartRateTrendIndicator)
                        {
                            // Only keep the last N heart rates, where N is HeartRateTrendIndicatorSampleRate
                            if (_heartRateHistory.Count >= ViewModel.Instance.HeartRateTrendIndicatorSampleRate)
                            {
                                _heartRateHistory.Dequeue();
                            }

                            _heartRateHistory.Enqueue(heartRate);

                            // Update the trend indicator
                            if (_heartRateHistory.Count > 1)
                            {

                                double slope = CalculateSlope(_heartRateHistory);
                                if (slope > ViewModel.Instance.HeartRateTrendIndicatorSensitivity)
                                {
                                    ViewModel.Instance.HeartRateTrendIndicator = Settings.SelectedPulsoidTrendSymbol.UpwardTrendSymbol;
                                }
                                else if (slope < -ViewModel.Instance.HeartRateTrendIndicatorSensitivity)
                                {
                                    ViewModel.Instance.HeartRateTrendIndicator = Settings.SelectedPulsoidTrendSymbol.DownwardTrendSymbol;
                                }
                                else
                                {
                                    ViewModel.Instance.HeartRateTrendIndicator = "";
                                }
                            }
                        }

                        if (ViewModel.Instance.MagicHeartRateIcons)
                        {
                            // Always cycle through heart icons
                            ViewModel.Instance.HeartRateIcon = ViewModel.Instance.HeartIcons[ViewModel.Instance.CurrentHeartIconIndex];
                            ViewModel.Instance.CurrentHeartIconIndex = (ViewModel.Instance.CurrentHeartIconIndex + 1) % ViewModel.Instance.HeartIcons.Count;
                        }

                        // Append additional icons based on heart rate, if the toggle is enabled
                        if (ViewModel.Instance.ShowTemperatureText)
                        {
                            if (heartRate < ViewModel.Instance.LowTemperatureThreshold)
                            {
                                ViewModel.Instance.HeartRateIcon = ViewModel.Instance.HeartIcons[ViewModel.Instance.CurrentHeartIconIndex] + ViewModel.Instance.FormattedLowHeartRateText;
                            }
                            else if (heartRate >= ViewModel.Instance.HighTemperatureThreshold)
                            {
                                ViewModel.Instance.HeartRateIcon = ViewModel.Instance.HeartIcons[ViewModel.Instance.CurrentHeartIconIndex] + ViewModel.Instance.FormattedHighHeartRateText;
                            }
                        }
                        else
                        {
                            ViewModel.Instance.HeartRateIcon = ViewModel.Instance.HeartIcons[ViewModel.Instance.CurrentHeartIconIndex];
                        }



                        if (ViewModel.Instance.HeartRate != heartRate)
                        {
                            ViewModel.Instance.HeartRateLastUpdate = DateTime.Now;
                            ViewModel.Instance.HeartRate = heartRate;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ViewModel.Instance.PulsoidAccessError = true;
                        ViewModel.Instance.PulsoidAccessErrorTxt = ex.Message;
                    });
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                    Logging.WriteException(ex, MSGBox: false);
                }

                int scanInterval = ViewModel.Instance.HeartRateScanInterval_v2 > 0 ? ViewModel.Instance.HeartRateScanInterval_v2 : 5;
                TimeSpan elapsedTime = DateTime.UtcNow - startTime;
                TimeSpan remainingDelay = TimeSpan.FromSeconds(scanInterval) - elapsedTime;

                if (remainingDelay > TimeSpan.Zero)
                {
                    await Task.Delay(remainingDelay, cancellationToken);
                }



            }
        }



        public static async Task<int> GetHeartRateViaHttpAsync()
        {

            string accessToken = ViewModel.Instance.PulsoidAccessTokenOAuth;
            if (string.IsNullOrEmpty(accessToken))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ViewModel.Instance.PulsoidAccessError = true;
                    ViewModel.Instance.PulsoidAccessErrorTxt = "No Pulsoid connection found. Please connect with the Pulsoid Authentication server";
                });
                return -1;
            }

            string url = "https://dev.pulsoid.net/api/v1/data/heart_rate/latest";

            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var jsonResponse = await response.Content.ReadAsStringAsync();
                JObject json = JObject.Parse(jsonResponse);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ViewModel.Instance.PulsoidAccessError = false;
                    ViewModel.Instance.PulsoidAuthConnected = true;
                    ViewModel.Instance.PulsoidAccessErrorTxt = "";
                });
                return json["data"]["heart_rate"].Value<int>();

            }
            catch (HttpRequestException httpEx)
            {
                string errorMessage = httpEx.Message; // Default to the exception's message

                switch (httpEx.StatusCode)
                {
                    case HttpStatusCode.Forbidden:
                        errorMessage = "Connection invalid or your subscription has expired. Please check your subscription.";
                        break;
                    case HttpStatusCode.PreconditionFailed:
                        errorMessage = "Connection successful, but no heart rate device detected. Ensure a device is connected to your account and has sent values.";
                        break;
                    case HttpStatusCode.NotFound:
                        errorMessage = "Endpoint not found. Please check if the Pulsoid API URL has changed.";
                        break;
                    case HttpStatusCode.InternalServerError:
                        errorMessage = "The Pulsoid server encountered an error. Please try again later.";
                        break;
                    case HttpStatusCode.RequestTimeout:
                        errorMessage = "Request timed out. Please check your internet connection.";
                        break;
                    case HttpStatusCode.BadGateway:
                        errorMessage = "Pulsoid server is currently experiencing issues. Please try again later.";
                        break;
                    case HttpStatusCode.ServiceUnavailable:
                        errorMessage = "Pulsoid service is currently unavailable. Please wait a moment and try again.";
                        break;
                    case HttpStatusCode.Unauthorized:
                        errorMessage = "Unauthorized access. Try again to connect.";
                        break;
                    case HttpStatusCode.BadRequest:
                        errorMessage = "Bad request. Ensure you're sending the correct data to Pulsoid.";
                        break;
                    case HttpStatusCode.TooManyRequests:
                        errorMessage = "You've sent too many requests in a short time. Please wait for a while and try again.";
                        break;
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ViewModel.Instance.PulsoidAccessError = true;
                    ViewModel.Instance.PulsoidAuthConnected = false;
                    ViewModel.Instance.PulsoidAccessErrorTxt = errorMessage;
                });
                Logging.WriteException(httpEx, MSGBox: false);
                return -1;
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ViewModel.Instance.PulsoidAccessError = true;
                    ViewModel.Instance.PulsoidAuthConnected = false;
                    ViewModel.Instance.PulsoidAccessErrorTxt = ex.Message;
                });
                Logging.WriteException(ex, MSGBox: false);
                return -1;
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

            return response.IsSuccessStatusCode;
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
