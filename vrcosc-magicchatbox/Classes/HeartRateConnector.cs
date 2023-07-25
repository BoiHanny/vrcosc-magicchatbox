using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes
{
    public class HeartRateConnector
    {

        private CancellationTokenSource? _cts;
        private readonly Queue<Tuple<DateTime, int>> _heartRates = new();
        private readonly Queue<int> _heartRateHistory = new();
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


        public void PropertyChangedHandler(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.Instance.IntgrHeartRate))
            {
                if (ViewModel.Instance.IntgrHeartRate)
                {
                    StartMonitoringHeartRateAsync();
                }
                else
                {
                    StopMonitoringHeartRateAsync();
                }
            }
        }

        private void StartMonitoringHeartRateAsync()
        {
            if (_cts != null)
            {
                return;
            }

            _cts = new CancellationTokenSource();
            Task.Run(async () => await HeartRateMonitoringLoopAsync(_cts.Token), _cts.Token);
        }

        private async Task HeartRateMonitoringLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {

                DateTime startTime = DateTime.UtcNow;

                try
                {

                    int heartRate = await GetHeartRateViaHttpAsync();
                    if (heartRate != -1)
                    {
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
                            while (_heartRates.Count > 0 && (DateTime.UtcNow - _heartRates.Peek().Item1 > TimeSpan.FromSeconds(ViewModel.Instance.SmoothHeartRateTimeSpan)))
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
                                    ViewModel.Instance.HeartRateTrendIndicator = "⤴️";
                                }
                                else if (slope < -ViewModel.Instance.HeartRateTrendIndicatorSensitivity)
                                {
                                    ViewModel.Instance.HeartRateTrendIndicator = "⤵️";
                                }
                                else
                                {
                                    ViewModel.Instance.HeartRateTrendIndicator = "";
                                }
                            }
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
                    Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
                }

                int scanInterval = ViewModel.Instance.HeartRateScanInterval_v1 > 0 ? ViewModel.Instance.HeartRateScanInterval_v1 : 5;
                TimeSpan elapsedTime = DateTime.UtcNow - startTime;
                TimeSpan remainingDelay = TimeSpan.FromSeconds(scanInterval) - elapsedTime;

                if (remainingDelay > TimeSpan.Zero)
                {
                    await Task.Delay(remainingDelay, cancellationToken);
                }



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
        }

        public static async Task<int> GetHeartRateViaHttpAsync()
        {

            string accessToken = ViewModel.Instance.PulsoidAccessToken;
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
                    ViewModel.Instance.PulsoidAccessErrorTxt = "";
                });
                return json["data"]["heart_rate"].Value<int>();
                
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ViewModel.Instance.PulsoidAccessError = true;
                ViewModel.Instance.PulsoidAccessErrorTxt = ex.Message;
                    });
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
                return -1; // Return an error code or handle the error as needed
            }
        }


    }
}
