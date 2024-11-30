using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.Modules
{
    public class NetworkStatisticsModule : INotifyPropertyChanged, IDisposable
    {
        private Timer _updateTimer;
        private NetworkInterface _activeNetworkInterface;
        private bool _isMonitoring;
        private long _previousBytesReceived;
        private long _previousBytesSent;
        private readonly Dispatcher _dispatcher;

        public bool IsInitialized { get; private set; }
        public double Interval { get; set; } = 1000;

        private double _currentDownloadSpeedMbps;
        private double _currentUploadSpeedMbps;
        private double _maxDownloadSpeedMbps;
        private double _maxUploadSpeedMbps;
        private double _networkUtilization;
        private double _totalDownloadedMB;
        private double _totalUploadedMB;

        public event PropertyChangedEventHandler PropertyChanged;

        // New property to control max speed source
        public bool UseInterfaceMaxSpeed { get; set; } = false;

        public NetworkStatisticsModule(double interval)
        {
            Interval = interval;
            _dispatcher = Application.Current.Dispatcher;
            ViewModel.Instance.PropertyChanged += PropertyChangedHandler;

            if (ShouldStartMonitoring())
            {
                InitializeNetworkStats();
                StartModule();
            }
        }

        private void PropertyChangedHandler(object sender, PropertyChangedEventArgs e)
        {
            if (IsRelevantPropertyChange(e.PropertyName))
            {
                if (ShouldStartMonitoring())
                {
                    if (!IsInitialized)
                    {
                        InitializeNetworkStats();
                    }
                    StartModule();
                }
                else
                {
                    StopModule();
                }
            }
        }

        private bool ShouldStartMonitoring()
        {
            return ViewModel.Instance.IntgrNetworkStatistics &&
                   ((ViewModel.Instance.IsVRRunning && ViewModel.Instance.IntgrNetworkStatistics_VR) ||
                    (!ViewModel.Instance.IsVRRunning && ViewModel.Instance.IntgrNetworkStatistics_DESKTOP));
        }

        private bool IsRelevantPropertyChange(string propertyName)
        {
            return propertyName == nameof(ViewModel.Instance.IntgrNetworkStatistics) ||
                   propertyName == nameof(ViewModel.Instance.IsVRRunning) ||
                   propertyName == nameof(ViewModel.Instance.IntgrNetworkStatistics_VR) ||
                   propertyName == nameof(ViewModel.Instance.IntgrNetworkStatistics_DESKTOP);
        }

        private void InitializeNetworkStats()
        {
            _activeNetworkInterface = GetActiveNetworkInterface();
            if (_activeNetworkInterface != null)
            {
                if (UseInterfaceMaxSpeed)
                {
                    var speedInMbps = _activeNetworkInterface.Speed / 1e6;
                    MaxDownloadSpeedMbps = speedInMbps;
                    MaxUploadSpeedMbps = speedInMbps;
                }
                else
                {
                    MaxDownloadSpeedMbps = 0;
                    MaxUploadSpeedMbps = 0;
                }

                var stats = _activeNetworkInterface.GetIPv4Statistics();
                _previousBytesReceived = stats.BytesReceived;
                _previousBytesSent = stats.BytesSent;

                IsInitialized = true;
            }
            else
            {
                // Handle the case when no active network interface is found
                Logging.WriteException(new Exception("No active network interface found"), MSGBox: false);
                IsInitialized = false;
            }
        }

        private NetworkInterface GetActiveNetworkInterface()
        {
            try
            {
                var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni =>
                        ni.OperationalStatus == OperationalStatus.Up &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                        ni.GetIPProperties().UnicastAddresses.Count > 0).ToList();

                if (networkInterfaces.Count == 0)
                    return null;

                // If there's only one active network interface, return it directly
                if (networkInterfaces.Count == 1)
                {
                    return networkInterfaces.First();
                }

                // Measure initial bytes sent/received
                var interfaceStats = new List<InterfaceStats>();
                foreach (var ni in networkInterfaces)
                {
                    var stats = ni.GetIPv4Statistics();
                    interfaceStats.Add(new InterfaceStats
                    {
                        NetworkInterface = ni,
                        InitialBytesReceived = stats.BytesReceived,
                        InitialBytesSent = stats.BytesSent
                    });
                }

                // Wait for a short interval
                Thread.Sleep(500);

                // Measure bytes sent/received after the interval
                foreach (var stat in interfaceStats)
                {
                    var stats = stat.NetworkInterface.GetIPv4Statistics();
                    stat.BytesReceivedDiff = stats.BytesReceived - stat.InitialBytesReceived;
                    stat.BytesSentDiff = stats.BytesSent - stat.InitialBytesSent;
                    stat.TotalBytesDiff = stat.BytesReceivedDiff + stat.BytesSentDiff;
                }

                // Select the network interface with the highest total bytes difference
                var mostActiveInterface = interfaceStats.OrderByDescending(s => s.TotalBytesDiff).FirstOrDefault();

                if (mostActiveInterface != null && mostActiveInterface.TotalBytesDiff > 0)
                {
                    return mostActiveInterface.NetworkInterface;
                }
                else
                {
                    // If no activity detected or measurement fails, fallback to selecting the first interface
                    return networkInterfaces.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                // Log the exception and fallback to the first available network interface
                Logging.WriteException(ex, MSGBox: false);
                return NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(ni =>
                        ni.OperationalStatus == OperationalStatus.Up &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                        ni.GetIPProperties().UnicastAddresses.Count > 0);
            }
        }

        private class InterfaceStats
        {
            public NetworkInterface NetworkInterface { get; set; }
            public long InitialBytesReceived { get; set; }
            public long InitialBytesSent { get; set; }
            public long BytesReceivedDiff { get; set; }
            public long BytesSentDiff { get; set; }
            public long TotalBytesDiff { get; set; }
        }

        public void StartModule()
        {
            if (_isMonitoring)
                return;

            _updateTimer = new Timer(OnTimedEvent, null, 0, (int)Interval);
            _isMonitoring = true;
        }

        public void StopModule()
        {
            if (!_isMonitoring)
                return;

            _updateTimer?.Change(Timeout.Infinite, 0);
            _updateTimer?.Dispose();
            _updateTimer = null;
            _isMonitoring = false;
        }

        private void OnTimedEvent(object state)
        {
            try
            {
                if (_activeNetworkInterface == null)
                {
                    InitializeNetworkStats();
                    if (_activeNetworkInterface == null)
                        return;
                }

                if (UseInterfaceMaxSpeed != ViewModel.Instance.NetworkStats_UseInterfaceMaxSpeed)
                {
                    UseInterfaceMaxSpeed = ViewModel.Instance.NetworkStats_UseInterfaceMaxSpeed;
                    MaxDownloadSpeedMbps = 0;
                    MaxUploadSpeedMbps = 0;
                }

                var stats = _activeNetworkInterface.GetIPv4Statistics();

                // Calculate the differences since the last check
                var bytesReceivedDiff = stats.BytesReceived - _previousBytesReceived;
                var bytesSentDiff = stats.BytesSent - _previousBytesSent;

                // Update previous values
                _previousBytesReceived = stats.BytesReceived;
                _previousBytesSent = stats.BytesSent;

                // Calculate speeds in Mbps
                var intervalInSeconds = Interval / 1000;
                var downloadSpeed = (bytesReceivedDiff * 8) / 1e6 / intervalInSeconds;
                var uploadSpeed = (bytesSentDiff * 8) / 1e6 / intervalInSeconds;

                // Update total downloaded and uploaded data in MB
                var totalDownloaded = TotalDownloadedMB + bytesReceivedDiff / 1e6;
                var totalUploaded = TotalUploadedMB + bytesSentDiff / 1e6;

                // Update maximum observed speeds if not using interface max speed
                if (!UseInterfaceMaxSpeed)
                {
                    if (downloadSpeed > MaxDownloadSpeedMbps)
                        MaxDownloadSpeedMbps = downloadSpeed;

                    if (uploadSpeed > MaxUploadSpeedMbps)
                        MaxUploadSpeedMbps = uploadSpeed;
                }

                // Determine the max speed to use for utilization calculation
                var maxDownloadSpeed = UseInterfaceMaxSpeed
                    ? _activeNetworkInterface.Speed / 1e6
                    : MaxDownloadSpeedMbps;

                var maxUploadSpeed = UseInterfaceMaxSpeed
                    ? _activeNetworkInterface.Speed / 1e6
                    : MaxUploadSpeedMbps;

                // Update network utilization
                var utilization = maxDownloadSpeed > 0
                    ? Math.Min(100, (downloadSpeed / maxDownloadSpeed) * 100)
                    : 0;

                // Update properties on the UI thread
                _dispatcher.BeginInvoke(new Action(() =>
                {
                    CurrentDownloadSpeedMbps = downloadSpeed;
                    CurrentUploadSpeedMbps = uploadSpeed;
                    TotalDownloadedMB = totalDownloaded;
                    TotalUploadedMB = totalUploaded;
                    NetworkUtilization = utilization;
                    MaxDownloadSpeedMbps = maxDownloadSpeed;
                    MaxUploadSpeedMbps = maxUploadSpeed;
                }));
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
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
                networkStatsDescriptions.Add($"{ConvertToSuperScriptIfNeeded("Max Up: ")} {FormatSpeed(MaxUploadSpeedMbps)}");

            if (ViewModel.Instance.NetworkStats_ShowTotalDown)
                networkStatsDescriptions.Add($"{ConvertToSuperScriptIfNeeded("Total Down: ")} {FormatData(TotalDownloadedMB)}");

            if (ViewModel.Instance.NetworkStats_ShowTotalUp)
                networkStatsDescriptions.Add($"{ConvertToSuperScriptIfNeeded("Total Up: ")} {FormatData(TotalUploadedMB)}");

            if (ViewModel.Instance.NetworkStats_ShowNetworkUtilization)
                networkStatsDescriptions.Add($"{ConvertToSuperScriptIfNeeded("Network utilization: ")} {NetworkUtilization:N2} %");

            if (networkStatsDescriptions.Count == 0)
            {
                return "";
            }

            foreach (var description in networkStatsDescriptions)
            {
                if (string.IsNullOrWhiteSpace(description))
                {
                    continue;
                }

                if (currentLine.Length + description.Length > maxLineWidth || (currentLine.Length == 0 && description.Length <= maxLineWidth))
                {
                    if (currentLine.Length > 0)
                    {
                        lines.Add(currentLine.TrimEnd());
                        currentLine = "";
                    }

                    if (description.Length <= maxLineWidth)
                    {
                        lines.Add(description);
                        continue;
                    }
                }

                if (currentLine.Length > 0)
                {
                    currentLine += separator;
                }

                currentLine += description;
            }

            if (currentLine.Length > 0)
            {
                lines.Add(currentLine.TrimEnd());
            }

            return string.Join("\v", lines);
        }

        private string FormatSpeed(double speedMbps)
        {
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
            if (dataMB < 1)
                return $"{dataMB * 1000:N2} {ConvertToSuperScriptIfNeeded("KB")}";
            else if (dataMB >= 1000)
                return dataMB >= 1_000_000 ? $"{dataMB / 1e6:N2} TB" : $"{dataMB / 1000:N2} {ConvertToSuperScriptIfNeeded("GB")}";
            else
                return $"{dataMB:N2} {ConvertToSuperScriptIfNeeded("MB")}";
        }

        // Implement property getters and setters with OnPropertyChanged notifications
        public double CurrentDownloadSpeedMbps
        {
            get => _currentDownloadSpeedMbps;
            set => SetProperty(ref _currentDownloadSpeedMbps, value);
        }

        public double CurrentUploadSpeedMbps
        {
            get => _currentUploadSpeedMbps;
            set => SetProperty(ref _currentUploadSpeedMbps, value);
        }

        public double MaxDownloadSpeedMbps
        {
            get => _maxDownloadSpeedMbps;
            set => SetProperty(ref _maxDownloadSpeedMbps, value);
        }

        public double MaxUploadSpeedMbps
        {
            get => _maxUploadSpeedMbps;
            set => SetProperty(ref _maxUploadSpeedMbps, value);
        }

        public double NetworkUtilization
        {
            get => _networkUtilization;
            set => SetProperty(ref _networkUtilization, value);
        }

        public double TotalDownloadedMB
        {
            get => _totalDownloadedMB;
            set => SetProperty(ref _totalDownloadedMB, value);
        }

        public double TotalUploadedMB
        {
            get => _totalUploadedMB;
            set => SetProperty(ref _totalUploadedMB, value);
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
                return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (!_dispatcher.CheckAccess())
            {
                _dispatcher.BeginInvoke(() =>
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                });
            }
            else
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public void Dispose()
        {
            StopModule();
            ViewModel.Instance.PropertyChanged -= PropertyChangedHandler;
        }
    }
}
