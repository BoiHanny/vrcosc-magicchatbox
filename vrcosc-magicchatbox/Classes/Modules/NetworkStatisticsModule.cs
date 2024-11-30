using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly object _initLock = new object();
        private bool _isInitializing;

        public bool IsInitialized { get; private set; }
        private double _interval = 1000;
        public double Interval
        {
            get => _interval;
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(Interval), "Interval must be greater than zero.");
                _interval = value;
                if (_isMonitoring)
                {
                    _updateTimer.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(_interval));
                }
            }
        }

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

        // CancellationTokenSource for asynchronous initialization
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public NetworkStatisticsModule(double interval = 1000)
        {
            Interval = interval;
            _dispatcher = Application.Current.Dispatcher;
            ViewModel.Instance.PropertyChanged += PropertyChangedHandler;

            InitializeNetworkStatsAsync().ConfigureAwait(false);
        }

        private void PropertyChangedHandler(object sender, PropertyChangedEventArgs e)
        {
            if (IsRelevantPropertyChange(e.PropertyName))
            {
                if (ShouldStartMonitoring())
                {
                    InitializeNetworkStatsAsync().ConfigureAwait(false);
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

        /// <summary>
        /// Asynchronously initializes network statistics.
        /// Ensures that initialization is thread-safe and does not block the UI thread.
        /// </summary>
        private async Task InitializeNetworkStatsAsync()
        {
            if (_isInitializing)
                return;

            lock (_initLock)
            {
                if (_isInitializing)
                    return;
                _isInitializing = true;
            }

            try
            {
                var networkInterface = await Task.Run(() => GetActiveNetworkInterfaceAsync(_cancellationTokenSource.Token));
                if (networkInterface != null)
                {
                    _activeNetworkInterface = networkInterface;

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

                    var stats = GetTotalBytes(_activeNetworkInterface);
                    _previousBytesReceived = stats.BytesReceived;
                    _previousBytesSent = stats.BytesSent;

                    IsInitialized = true;

                    if (!_isMonitoring)
                    {
                        StartModule();
                    }
                }
                else
                {
                    // Handle the case when no active network interface is found
                    Logging.WriteException(new Exception("No active network interface found"), MSGBox: false);
                    IsInitialized = false;
                }
            }
            catch (OperationCanceledException)
            {
                // Initialization was canceled; do nothing.
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
                IsInitialized = false;
            }
            finally
            {
                lock (_initLock)
                {
                    _isInitializing = false;
                }
            }
        }

        /// <summary>
        /// Asynchronously determines the active network interface.
        /// Includes both IPv4 and IPv6 statistics.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The selected active NetworkInterface or null if none found.</returns>
        private NetworkInterface GetActiveNetworkInterfaceAsync(CancellationToken cancellationToken)
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni =>
                    ni.OperationalStatus == OperationalStatus.Up &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                    ni.GetIPProperties().UnicastAddresses.Any()).ToList();

            if (networkInterfaces.Count == 0)
                return null;

            // If there's only one active network interface, return it directly
            if (networkInterfaces.Count == 1)
            {
                return networkInterfaces.First();
            }

            // Prioritize network interfaces based on type (e.g., Ethernet over Wireless)
            var prioritizedInterfaces = networkInterfaces.OrderByDescending(ni => GetInterfacePriority(ni)).ToList();

            // Measure initial bytes sent/received for all interfaces
            var interfaceStats = prioritizedInterfaces.Select(ni => new InterfaceStats
            {
                NetworkInterface = ni,
                InitialBytesReceived = GetTotalBytes(ni).BytesReceived,
                InitialBytesSent = GetTotalBytes(ni).BytesSent
            }).ToList();

            // Wait for a short interval asynchronously
            Task.Delay(500, cancellationToken).Wait(cancellationToken);

            // Measure bytes sent/received after the interval
            foreach (var stat in interfaceStats)
            {
                var currentStats = GetTotalBytes(stat.NetworkInterface);
                stat.BytesReceivedDiff = currentStats.BytesReceived - stat.InitialBytesReceived;
                stat.BytesSentDiff = currentStats.BytesSent - stat.InitialBytesSent;
                stat.TotalBytesDiff = stat.BytesReceivedDiff + stat.BytesSentDiff;
            }

            // Select the network interface with the highest total bytes difference
            var mostActiveInterface = interfaceStats
                .OrderByDescending(s => s.TotalBytesDiff)
                .FirstOrDefault(s => s.TotalBytesDiff > 0);

            return mostActiveInterface?.NetworkInterface ?? prioritizedInterfaces.FirstOrDefault();
        }

        /// <summary>
        /// Assigns a priority to network interfaces based on their type.
        /// Higher priority for Ethernet, then Wireless, then others.
        /// </summary>
        /// <param name="ni">NetworkInterface.</param>
        /// <returns>Integer priority.</returns>
        private int GetInterfacePriority(NetworkInterface ni)
        {
            return ni.NetworkInterfaceType switch
            {
                NetworkInterfaceType.Ethernet => 3,
                NetworkInterfaceType.Wireless80211 => 2,
                _ => 1,
            };
        }

        /// <summary>
        /// Retrieves total bytes sent and received, including both IPv4 and IPv6.
        /// </summary>
        /// <param name="ni">NetworkInterface.</param>
        /// <returns>TotalBytes struct containing BytesReceived and BytesSent.</returns>
        private TotalBytes GetTotalBytes(NetworkInterface ni)
        {
            var ipv4Stats = ni.GetIPv4Statistics();
            var ipv6Stats = ni.GetIPStatistics();
            return new TotalBytes
            {
                BytesReceived = ipv4Stats.BytesReceived + ipv6Stats.BytesReceived,
                BytesSent = ipv4Stats.BytesSent + ipv6Stats.BytesSent
            };
        }

        private struct TotalBytes
        {
            public long BytesReceived;
            public long BytesSent;
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
            if (_isMonitoring || !IsInitialized)
                return;

            _updateTimer = new Timer(OnTimedEvent, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(Interval));
            _isMonitoring = true;
        }

        public void StopModule()
        {
            if (!_isMonitoring)
                return;

            _updateTimer?.Change(Timeout.Infinite, Timeout.Infinite);
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
                    // Attempt to re-initialize if the active interface is null
                    InitializeNetworkStatsAsync().ConfigureAwait(false);
                    if (_activeNetworkInterface == null)
                        return;
                }

                // Update UseInterfaceMaxSpeed based on ViewModel
                if (UseInterfaceMaxSpeed != ViewModel.Instance.NetworkStats_UseInterfaceMaxSpeed)
                {
                    UseInterfaceMaxSpeed = ViewModel.Instance.NetworkStats_UseInterfaceMaxSpeed;
                    MaxDownloadSpeedMbps = 0;
                    MaxUploadSpeedMbps = 0;
                }

                var stats = GetTotalBytes(_activeNetworkInterface);

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
                var totalDownloaded = TotalDownloadedMB + (bytesReceivedDiff / 1e6);
                var totalUploaded = TotalUploadedMB + (bytesSentDiff / 1e6);

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
                networkStatsDescriptions.Add($"{ConvertToSuperScriptIfNeeded("Network Utilization: ")} {NetworkUtilization:N2} %");

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
            else if (dataMB >= 1_000_000)
                return $"{dataMB / 1e6:N2} {ConvertToSuperScriptIfNeeded("TB")}";
            else if (dataMB >= 1000)
                return $"{dataMB / 1000:N2} {ConvertToSuperScriptIfNeeded("GB")}";
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
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            ViewModel.Instance.PropertyChanged -= PropertyChangedHandler;
        }
    }
}
