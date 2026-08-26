using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Utilities;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Osc.Text;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.Services;

namespace vrcosc_magicchatbox.Classes.Modules;

public class NetworkStatisticsModule : INotifyPropertyChanged, IModule
{
    private readonly IAppState _appState;

    private NetworkInterface? _activeNetworkInterface;

    private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

    private double _currentDownloadSpeedMbps;
    private double _currentUploadSpeedMbps;
    private readonly IUiDispatcher _dispatcher;
    private readonly object _initLock = new object();
    private double _interval = 1000;
    private bool _isInitializing;
    private bool _isMonitoring;
    private double _maxDownloadSpeedMbps;
    private double _maxUploadSpeedMbps;
    private double _networkUtilization;
    private long _previousBytesReceived;
    private long _previousBytesSent;
    private double _totalDownloadedMB;
    private double _totalUploadedMB;
    private Timer? _updateTimer;

    private readonly ISettingsProvider<NetworkStatsSettings> _settingsProvider;
    public NetworkStatsSettings Settings => _settingsProvider.Value;
    public void SaveSettings() => _settingsProvider.Save();

    public string Name => "NetworkStatistics";
    public bool IsEnabled { get; set; } = true;
    public bool IsRunning => _isMonitoring;
    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task StartAsync(CancellationToken ct = default) { StartModule(); return Task.CompletedTask; }
    public Task StopAsync(CancellationToken ct = default) { StopModule(); return Task.CompletedTask; }

    private readonly IntegrationSettings _integrationSettings;
    private readonly IToastService? _toast;
    private readonly IPrivacyConsentService _consentService;
    private bool _networkErrorShown;

    public NetworkStatisticsModule(
        IAppState appState,
        ISettingsProvider<NetworkStatsSettings> settingsProvider,
        ISettingsProvider<IntegrationSettings> integrationSettingsProvider,
        IUiDispatcher dispatcher,
        IPrivacyConsentService consentService,
        double interval = 1000,
        IToastService? toast = null)
    {
        _appState = appState;
        _settingsProvider = settingsProvider;
        _integrationSettings = integrationSettingsProvider.Value;
        Interval = interval;
        _dispatcher = dispatcher;
        _consentService = consentService;
        _toast = toast;
        _appState.PropertyChanged += PropertyChangedHandler;
        _integrationSettings.PropertyChanged += PropertyChangedHandler;

        _consentService.ConsentChanged += OnConsentChanged;

        if (_consentService.IsApproved(PrivacyHook.NetworkStats) && ShouldStartMonitoring())
            BeginInitializeNetworkStats();
    }

    private void OnConsentChanged(object? sender, ConsentChangedEventArgs e)
    {
        if (e.Hook != PrivacyHook.NetworkStats)
            return;

        if (e.NewState == ConsentState.Denied)
        {
            StopModule();
            IsInitialized = false;
            _activeNetworkInterface = null;
            _dispatcher.BeginInvoke(() =>
            {
                CurrentDownloadSpeedMbps = 0;
                CurrentUploadSpeedMbps = 0;
                NetworkUtilization = 0;
            });
            _toast?.Show("🔒 Network Stats", "Network monitoring paused — privacy consent revoked.", ToastType.Privacy, key: "network-privacy-denied");
        }
        else if (e.NewState == ConsentState.Approved && !IsInitialized)
        {
            BeginInitializeNetworkStats();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private string Measure(double amount, string unit)
        => new SegmentWriter()
            .Field(OscText.Value(amount.ToString("N2", CultureInfo.CurrentCulture)), Unit(unit))
            .Text;

    private OscText Unit(string unit)
        => Settings.StyledCharacters ? OscText.Unit(unit) : OscText.Raw(unit);

    private OscText Label(string label)
        => Settings.StyledCharacters ? OscText.Label(label) : OscText.Raw(label);

    private string FormatData(double dataMB)
    {
        if (dataMB < 1)
            return Measure(dataMB * 1000, "KB");
        if (dataMB >= 1_000_000)
            return Measure(dataMB / 1e6, "TB");
        if (dataMB >= 1000)
            return Measure(dataMB / 1000, "GB");

        return Measure(dataMB, "MB");
    }

    private string FormatSpeed(double speedMbps)
    {
        if (speedMbps < 1)
            return Measure(speedMbps * 1000, "Kbps");
        if (speedMbps >= 1000)
            return Measure(speedMbps / 1000, "Gbps");

        return Measure(speedMbps, "Mbps");
    }

    private Task<NetworkInterface?> GetActiveNetworkInterfaceAsync(CancellationToken cancellationToken)
    {
        var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni =>
                ni.OperationalStatus == OperationalStatus.Up &&
                ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                ni.GetIPProperties().UnicastAddresses.Any())
            .OrderByDescending(ni => GetInterfacePriority(ni))
            .ToList();

        return Task.FromResult(networkInterfaces.FirstOrDefault());
    }

    private int GetInterfacePriority(NetworkInterface ni)
    {
        return ni.NetworkInterfaceType switch
        {
            NetworkInterfaceType.Ethernet => 3,
            NetworkInterfaceType.Wireless80211 => 2,
            _ => 1,
        };
    }

    private TotalBytes GetTotalBytes(NetworkInterface ni)
    {
        var ipv4Stats = ni.GetIPv4Statistics();
        return new TotalBytes
        {
            BytesReceived = ipv4Stats.BytesReceived,
            BytesSent = ipv4Stats.BytesSent
        };
    }

    private void BeginInitializeNetworkStats()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await InitializeNetworkStatsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"Network statistics initialisation failed: {ex.Message}");
            }
        });
    }

    private async Task InitializeNetworkStatsAsync()
    {
        if (!_consentService.IsApproved(PrivacyHook.NetworkStats))
            return;

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
            var networkInterface = await GetActiveNetworkInterfaceAsync(_cancellationTokenSource.Token);
            if (networkInterface != null)
            {
                _activeNetworkInterface = networkInterface;

                if (UseInterfaceMaxSpeed)
                {
                    var speedInMbps = networkInterface.Speed / 1e6;
                    MaxDownloadSpeedMbps = speedInMbps;
                    MaxUploadSpeedMbps = speedInMbps;
                }
                else
                {
                    MaxDownloadSpeedMbps = 0;
                    MaxUploadSpeedMbps = 0;
                }

                var stats = GetTotalBytes(networkInterface);
                _previousBytesReceived = stats.BytesReceived;
                _previousBytesSent = stats.BytesSent;

                IsInitialized = true;

                if (!_isMonitoring && ShouldStartMonitoring())
                {
                    StartModule();
                }
            }
            else
            {
                Logging.WriteException(new Exception("No active network interface found"), MSGBox: false);
                IsInitialized = false;
                if (!_networkErrorShown)
                {
                    _networkErrorShown = true;
                    _toast?.Show("🌐 Network Stats", "No active network interface found. Network monitoring unavailable.", ToastType.Warning, key: "network-no-interface");
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            IsInitialized = false;
            if (!_networkErrorShown)
            {
                _networkErrorShown = true;
                _toast?.Show("🌐 Network Stats", "Network monitoring failed to initialize.", ToastType.Warning, key: "network-init-error");
            }
        }
        finally
        {
            lock (_initLock)
            {
                _isInitializing = false;
            }
        }
    }

    private bool IsRelevantPropertyChange(string? propertyName)
    {
        return propertyName == nameof(_integrationSettings.IntgrNetworkStatistics) ||
               propertyName == nameof(_appState.IsVRRunning) ||
               propertyName == nameof(_integrationSettings.IntgrNetworkStatistics_VR) ||
               propertyName == nameof(_integrationSettings.IntgrNetworkStatistics_DESKTOP);
    }

    private void OnTimedEvent(object? state)
    {
        try
        {
            if (!_consentService.IsApproved(PrivacyHook.NetworkStats))
                return;

            if (!ShouldStartMonitoring())
            {
                StopModule();
                return;
            }

            if (_activeNetworkInterface == null)
            {
                BeginInitializeNetworkStats();
                if (_activeNetworkInterface == null)
                    return;
            }

            var activeNetworkInterface = _activeNetworkInterface;

            if (UseInterfaceMaxSpeed != Settings.UseInterfaceMaxSpeed)
            {
                UseInterfaceMaxSpeed = Settings.UseInterfaceMaxSpeed;
                MaxDownloadSpeedMbps = 0;
                MaxUploadSpeedMbps = 0;
            }

            var stats = GetTotalBytes(activeNetworkInterface);

            var bytesReceivedDiff = stats.BytesReceived - _previousBytesReceived;
            var bytesSentDiff = stats.BytesSent - _previousBytesSent;

            _previousBytesReceived = stats.BytesReceived;
            _previousBytesSent = stats.BytesSent;

            var intervalInSeconds = Interval / 1000;
            var downloadSpeed = (bytesReceivedDiff * 8) / 1e6 / intervalInSeconds;
            var uploadSpeed = (bytesSentDiff * 8) / 1e6 / intervalInSeconds;

            var totalDownloaded = TotalDownloadedMB + (bytesReceivedDiff / 1e6);
            var totalUploaded = TotalUploadedMB + (bytesSentDiff / 1e6);

            if (!UseInterfaceMaxSpeed)
            {
                if (downloadSpeed > MaxDownloadSpeedMbps)
                    MaxDownloadSpeedMbps = downloadSpeed;

                if (uploadSpeed > MaxUploadSpeedMbps)
                    MaxUploadSpeedMbps = uploadSpeed;
            }

            var maxDownloadSpeed = UseInterfaceMaxSpeed
                ? activeNetworkInterface.Speed / 1e6
                : MaxDownloadSpeedMbps;

            var maxUploadSpeed = UseInterfaceMaxSpeed
                ? activeNetworkInterface.Speed / 1e6
                : MaxUploadSpeedMbps;

            var utilization = maxDownloadSpeed > 0
                ? Math.Min(100, (downloadSpeed / maxDownloadSpeed) * 100)
                : 0;

            _dispatcher.InvokeAsync(() =>
            {
                CurrentDownloadSpeedMbps = downloadSpeed;
                CurrentUploadSpeedMbps = uploadSpeed;
                TotalDownloadedMB = totalDownloaded;
                TotalUploadedMB = totalUploaded;
                NetworkUtilization = utilization;
                MaxDownloadSpeedMbps = maxDownloadSpeed;
                MaxUploadSpeedMbps = maxUploadSpeed;
            });
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
    }

    private void PropertyChangedHandler(object? sender, PropertyChangedEventArgs e)
    {
        if (IsRelevantPropertyChange(e.PropertyName))
        {
            if (ShouldStartMonitoring())
            {
                BeginInitializeNetworkStats();
            }
            else
            {
                StopModule();
            }
        }
    }

    private bool ShouldStartMonitoring()
    {
        return _integrationSettings.IntgrNetworkStatistics &&
               ((_appState.IsVRRunning && _integrationSettings.IntgrNetworkStatistics_VR) ||
                (!_appState.IsVRRunning && _integrationSettings.IntgrNetworkStatistics_DESKTOP));
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.InvokeAsync(() =>
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            });
        }
        else
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
            return false;
        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private bool _disposed;

    public void Dispose()
    {
        lock (_monitoringLock)
        {
            if (_disposed)
                return;
            _disposed = true;
        }

        StopModule();

        try
        {
            _cancellationTokenSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        _cancellationTokenSource.Dispose();
        _appState.PropertyChanged -= PropertyChangedHandler;
        _integrationSettings.PropertyChanged -= PropertyChangedHandler;
    }

    public string GenerateDescription()
    {
        const int maxLineWidth = 25;
        var separator = " | ";
        List<string> lines = new List<string>();
        string currentLine = "";

        var networkStatsDescriptions = new List<string>();

        void Add(bool show, string label, string value)
        {
            if (show)
                networkStatsDescriptions.Add(new SegmentWriter().Field(Label(label), OscText.Value(value)).Text);
        }

        Add(Settings.ShowCurrentDown, "Down", FormatSpeed(CurrentDownloadSpeedMbps));
        Add(Settings.ShowCurrentUp, "Up", FormatSpeed(CurrentUploadSpeedMbps));
        Add(Settings.ShowMaxDown, "Max Down", FormatSpeed(MaxDownloadSpeedMbps));
        Add(Settings.ShowMaxUp, "Max Up", FormatSpeed(MaxUploadSpeedMbps));
        Add(Settings.ShowTotalDown, "Total Down", FormatData(TotalDownloadedMB));
        Add(Settings.ShowTotalUp, "Total Up", FormatData(TotalUploadedMB));

        if (Settings.ShowNetworkUtilization)
        {
            networkStatsDescriptions.Add(new SegmentWriter()
                .Field(
                    Label("Network Utilization"),
                    OscText.Value(NetworkUtilization.ToString("N2", CultureInfo.CurrentCulture)),
                    Unit("%"))
                .Text);
        }

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

    private readonly object _monitoringLock = new();

    public void StartModule()
    {
        lock (_monitoringLock)
        {
            if (_isMonitoring || !IsInitialized)
                return;

            _updateTimer = new Timer(OnTimedEvent, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(Interval));
            _isMonitoring = true;
        }
    }

    public void StopModule()
    {
        lock (_monitoringLock)
        {
            if (!_isMonitoring)
                return;

            _updateTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _updateTimer?.Dispose();
            _updateTimer = null;
            _isMonitoring = false;
        }
    }

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
                // _updateTimer is always set while _isMonitoring is true (see StartModule/StopModule).
                _updateTimer!.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(_interval));
            }
        }
    }

    public bool IsInitialized { get; private set; }

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

    public bool UseInterfaceMaxSpeed { get; set; } = false;

    private struct TotalBytes
    {
        public long BytesReceived;
        public long BytesSent;
    }
}
