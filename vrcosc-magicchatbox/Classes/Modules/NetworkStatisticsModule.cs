using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Timers;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.Modules
{
    public class NetworkStatisticsModule : INotifyPropertyChanged, IDisposable
    {
        private Timer _updateTimer;
        public bool IsInitialized { get; private set; }
        public double interval { get; set; } = 1000;
        private double _currentDownloadSpeedMbps;
        private double _currentUploadSpeedMbps;
        private double _maxDownloadSpeedMbps;
        private double _maxUploadSpeedMbps;
        private double _networkUtilization;
        private double _totalDownloadedMB;
        private double _totalUploadedMB;


        public void PropertyChangedHandler(object sender, PropertyChangedEventArgs e)
        {
            if (IsRelevantPropertyChange(e.PropertyName))
            {
                if (ShouldStartMonitoring())
                {
                    if (!IsInitialized)
                    {
                        InitializeNetworkStatsAsync();
                        IsInitialized = true;
                    }
                    StartModule();
                }
                else
                {
                    if (!IsInitialized)
                    {
                        InitializeNetworkStatsAsync();
                        IsInitialized = true;
                    }
                    StopModule();
                }
            }
        }


        public bool ShouldStartMonitoring()
        {
            return ViewModel.Instance.IntgrNetworkStatistics && ViewModel.Instance.IsVRRunning && ViewModel.Instance.IntgrNetworkStatistics_VR ||
                   ViewModel.Instance.IntgrNetworkStatistics && !ViewModel.Instance.IsVRRunning && ViewModel.Instance.IntgrNetworkStatistics_DESKTOP;
        }

        public bool IsRelevantPropertyChange(string propertyName)
        {
            return propertyName == nameof(ViewModel.Instance.IntgrNetworkStatistics) ||
                   propertyName == nameof(ViewModel.Instance.IsVRRunning) ||
                   propertyName == nameof(ViewModel.Instance.IntgrNetworkStatistics_VR) ||
                   propertyName == nameof(ViewModel.Instance.IntgrNetworkStatistics_DESKTOP);
        }

        private PerformanceCounter downloadCounter;
        private PerformanceCounter uploadCounter;
        private DateTime previousUpdateTime;

        public double TotalDownloadedMB
        {
            get { return _totalDownloadedMB; }
            set { SetProperty(ref _totalDownloadedMB, value); }
        }

        public double TotalUploadedMB
        {
            get { return _totalUploadedMB; }
            set { SetProperty(ref _totalUploadedMB, value); }
        }

        public double CurrentDownloadSpeedMbps
        {
            get { return _currentDownloadSpeedMbps; }
            set { SetProperty(ref _currentDownloadSpeedMbps, value); }
        }

        public double CurrentUploadSpeedMbps
        {
            get { return _currentUploadSpeedMbps; }
            set { SetProperty(ref _currentUploadSpeedMbps, value); }
        }

        public double MaxDownloadSpeedMbps
        {
            get { return _maxDownloadSpeedMbps; }
            set { SetProperty(ref _maxDownloadSpeedMbps, value); }
        }

        public double MaxUploadSpeedMbps
        {
            get { return _maxUploadSpeedMbps; }
            set { SetProperty(ref _maxUploadSpeedMbps, value); }
        }

        public double NetworkUtilization
        {
            get { return _networkUtilization; }
            set { SetProperty(ref _networkUtilization, value); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public NetworkStatisticsModule(double interval)
        {
               this.interval = interval;
            ViewModel.Instance.PropertyChanged += PropertyChangedHandler;
            if (ShouldStartMonitoring())
            {
                if(!IsInitialized)
                {
                    InitializeNetworkStatsAsync();
                    IsInitialized = true;
                }
                StartModule();
            }
                
        }

        public void StartModule()
        {
            if (_updateTimer != null && !_updateTimer.Enabled)
            {
                _updateTimer.Start();
            }
        }

        public void StopModule()
        {
            if (_updateTimer != null && _updateTimer.Enabled)
            {
                _updateTimer.Stop();
            }
        }

        public void ForceUpdate()
        {
            OnTimedEvent(null, null);
        }

        private async Task InitializeNetworkStatsAsync()
        {
            // Attempt to initialize performance counters with an active network interface
            await Task.Run(() =>
            {
                if (!InitializePerformanceCounters())
            {
                // If initialization fails, subscribe to the NetworkAddressChanged event to retry when the network status changes
                NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
            }
                });

            // Initialize and start the update timer
            _updateTimer = new Timer(interval)
            {
                AutoReset = true,
                Enabled = false
            };
            _updateTimer.Elapsed += OnTimedEvent;

            previousUpdateTime = DateTime.Now;
        }

        private bool InitializePerformanceCounters()
        {
            // Dispose of existing counters
            downloadCounter?.Dispose();
            uploadCounter?.Dispose();

            // Attempt to select an active network interface
            NetworkInterface activeNetworkInterface = GetActiveNetworkInterface();

            if (activeNetworkInterface != null)
            {
                // Get the correct instance name for the PerformanceCounter
                string instanceName = GetInstanceNameForPerformanceCounter(activeNetworkInterface);
                if (instanceName == null)
                {
                    // Handle the case where no matching instance name is found
                    // For example, log this issue and use a default instance name or skip setting up counters
                    return false;
                }

                // Initialize Performance Counters for the selected network interface
                downloadCounter = new PerformanceCounter("Network Interface", "Bytes Received/sec", instanceName);
                uploadCounter = new PerformanceCounter("Network Interface", "Bytes Sent/sec", instanceName);

                // Update maximum speeds based on the active network interface
                MaxDownloadSpeedMbps = activeNetworkInterface.Speed / 8e6; // Convert from bits to Megabytes
                MaxUploadSpeedMbps = activeNetworkInterface.Speed / 8e6; // Convert from bits to Megabytes

                return true; // Initialization succeeded
            }

            return false; // No active network interface was found
        }

        private string GetInstanceNameForPerformanceCounter(NetworkInterface networkInterface)
        {
            // Get the performance counter category for network interfaces
            var category = new PerformanceCounterCategory("Network Interface");
            // Get all instance names for the network interface category
            string[] instanceNames = category.GetInstanceNames();

            // Normalize network interface description to match performance counter naming conventions
            string normalizedInterfaceName = NormalizeInterfaceName(networkInterface.Description);

            // Try to find a performance counter instance name that matches the normalized network interface name
            foreach (string instanceName in instanceNames)
            {
                // Use the normalized name for matching
                if (instanceName.Equals(normalizedInterfaceName, StringComparison.OrdinalIgnoreCase))
                {
                    return instanceName; // Found a matching instance name
                }
            }

            // If no match is found, return null or handle as appropriate for your application
            return null; // or throw new InvalidOperationException if that is your preferred behavior
        }

        private string NormalizeInterfaceName(string interfaceName)
        {
            // Replace invalid characters based on the mappings provided by Microsoft documentation
            return interfaceName
                .Replace('(', '[')
                .Replace(')', ']')
                .Replace('#', '_')
                .Replace('\\', '_')
                .Replace('/', '_');
            // Add any other normalization rules if needed
        }


        private NetworkInterface GetActiveNetworkInterface()
        {
            // Retrieve all network interfaces
            NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

            // Select the first active network interface that is up and has an IPv4 address and is not a loopback or tunnel
            foreach (var ni in networkInterfaces)
            {
                if (ni.OperationalStatus == OperationalStatus.Up &&
                    // more that 0 less that 10000 (10Gbps)
                    ni.Speed > 0 && ni.Speed < 10000000000 &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                    ni.GetIPProperties().UnicastAddresses.Count > 0) // Check for an IPv4 address
                {
                    return ni;
                }
            }
            return null; // No active network interface found
        }


        private void ResetCurrentStatistics()
        {
            // Reset current network statistics
            CurrentDownloadSpeedMbps = 0;
            CurrentUploadSpeedMbps = 0;
            NetworkUtilization = 0;
        }

        private void OnNetworkAddressChanged(object sender, EventArgs e)
        {
            // When the network address changes, try to re-initialize the performance counters
            if (InitializePerformanceCounters())
            {
                // If the re-initialization is successful, reset the current statistics
                ResetCurrentStatistics();
            }
            else
            {
                // Log or handle the error if no active network interface is found after a network change
                // This is application-specific and you should decide how to handle this case
            }
        }
        public string GenerateDescription()
        {
            const int maxLineWidth = 25;
            var separator = " | ";
            List<string> lines = new List<string>();
            string currentLine = "";

            // List to store individual network stats descriptions
            var networkStatsDescriptions = new List<string>();

            if (ViewModel.Instance.NetworkStats_ShowCurrentDown)
                networkStatsDescriptions.Add($"{ConvertToSuperScriptIfNeeded("Down: ")} {FormatSpeed(CurrentDownloadSpeedMbps)}");

            if (ViewModel.Instance.NetworkStats_ShowCurrentUp)
                networkStatsDescriptions.Add($"{ConvertToSuperScriptIfNeeded("Up: ")} {FormatSpeed(CurrentUploadSpeedMbps)}");

            if (ViewModel.Instance.NetworkStats_ShowMaxDown)
                networkStatsDescriptions.Add($"{ConvertToSuperScriptIfNeeded("Max Down: ")} {FormatSpeed(MaxDownloadSpeedMbps)}");

            if (ViewModel.Instance.NetworkStats_ShowMaxUp)
                networkStatsDescriptions.Add($"{ConvertToSuperScriptIfNeeded("Max Up: ")}{FormatSpeed(MaxUploadSpeedMbps)}");

            if (ViewModel.Instance.NetworkStats_ShowTotalDown)
                networkStatsDescriptions.Add($"{ConvertToSuperScriptIfNeeded("Total Down: ")}{FormatData(TotalDownloadedMB)}");

            if (ViewModel.Instance.NetworkStats_ShowTotalUp)
                networkStatsDescriptions.Add($"{ConvertToSuperScriptIfNeeded("Total Up: ")}{FormatData(TotalUploadedMB)}");

            if (ViewModel.Instance.NetworkStats_ShowNetworkUtilization)
                networkStatsDescriptions.Add($"{ConvertToSuperScriptIfNeeded("Network utilization: ")}{NetworkUtilization:N2} %");


            foreach (var description in networkStatsDescriptions)
            {
                if (currentLine.Length + description.Length > maxLineWidth)
                {
                    lines.Add(currentLine.TrimEnd());
                    currentLine = "";
                }

                if (currentLine.Length > 0)
                {
                    currentLine += separator;
                }

                currentLine +=  description;
            }

            if (currentLine.Length > 0)
            {
                lines.Add(currentLine.TrimEnd());
            }

            return string.Join("\v", lines);
        }

        private string FormatSpeed(double speedMbps)
        {
            // Convert and format speed based on its magnitude
            if (speedMbps < 1)
                return $"{speedMbps * 1000:N2} {ConvertToSuperScriptIfNeeded("Kbps")}";
            else if (speedMbps >= 1000)
                return $"{speedMbps / 1000:N2} {ConvertToSuperScriptIfNeeded("Gbps")}";
            else
                return $"{speedMbps:N2} {ConvertToSuperScriptIfNeeded("Mbps")}";
        }

        private string ConvertToSuperScriptIfNeeded(string unitstring)
        {
            if (ViewModel.Instance.NetworkStats_StyledCharacters)
            {
                return DataController.TransformToSuperscript(unitstring.Replace(":", ""));
            }
            else
            {
                return unitstring;
            }
        }

        private string FormatData(double dataMB)
        {
            // Convert and format data based on its magnitude
            if (dataMB < 1)
                return $"{dataMB * 1000:N2} {ConvertToSuperScriptIfNeeded("KB")}";
            else if (dataMB >= 1000)
                return dataMB >= 1000000 ? $"{dataMB / 1e6:N2} TB" : $"{dataMB / 1000:N2} {ConvertToSuperScriptIfNeeded("GB")}";
            else
                return $"{dataMB:N2} {ConvertToSuperScriptIfNeeded("MB")}";
        }


        private void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            try
            {
                // Current time
                DateTime currentTime = DateTime.Now;

                // Calculate the number of seconds elapsed
                double elapsedSeconds = (currentTime - previousUpdateTime).TotalSeconds;
                previousUpdateTime = currentTime;

                // Fetch current network usage once
                double currentDownloadBytes = downloadCounter.NextValue();
                double currentUploadBytes = uploadCounter.NextValue();

                // Calculate speeds in Mbps
                CurrentDownloadSpeedMbps = currentDownloadBytes / 1e6 * 8;
                CurrentUploadSpeedMbps = currentUploadBytes / 1e6 * 8;

                // Update total downloaded and uploaded data in MB
                TotalDownloadedMB += currentDownloadBytes / 1e6 * elapsedSeconds;
                TotalUploadedMB += currentUploadBytes / 1e6 * elapsedSeconds;

                // Update network utilization
                NetworkUtilization = Math.Min(100, (CurrentDownloadSpeedMbps / MaxDownloadSpeedMbps) * 100);
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
            }
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
            {
                return false;
            }
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public void Dispose()
        {
            _updateTimer?.Stop();
            _updateTimer?.Dispose();
            downloadCounter?.Dispose();
            uploadCounter?.Dispose();
            NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
        }
    }
}
