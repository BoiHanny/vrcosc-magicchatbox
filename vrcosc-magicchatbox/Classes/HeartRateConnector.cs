using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes
{
    public class HeartRateConnector
    {
        
        private CancellationTokenSource _cts;

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
                        // Apply the adjustment
                        heartRate += ViewModel.Instance.HeartRateAdjustment;

                        // Ensure the adjusted heart rate is not negative
                        heartRate = Math.Max(0, heartRate);
                        if (ViewModel.Instance.HeartRate != heartRate)
                        {
                            ViewModel.Instance.HeartRateLastUpdate = DateTime.Now;
                            ViewModel.Instance.HeartRate = heartRate;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log the exception here, for example using Logging.WriteException(ex);
                    // You may want to add a short delay before continuing to prevent rapid retries in case of persistent errors
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }

                int scanInterval = ViewModel.Instance.HeartRateScanInterval > 0 ? ViewModel.Instance.HeartRateScanInterval : 5;

                // Calculate the time it took to get the heart rate
                TimeSpan elapsedTime = DateTime.UtcNow - startTime;

                // Calculate the remaining time for the delay
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

        public async Task<int> GetHeartRateViaHttpAsync()
        {
            string accessToken = ViewModel.Instance.PulsoidAccessToken;
            string url = "https://dev.pulsoid.net/api/v1/data/heart_rate/latest";

            try
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var response = await httpClient.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    JObject json = JObject.Parse(jsonResponse);

                    return json["data"]["heart_rate"].Value<int>();
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
                return -1; // Return an error code or handle the error as needed
            }
        }


    }
}
