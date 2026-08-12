using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.Services.Vr;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Classes.Modules.Vr;

public partial class VrPerformanceModule : ObservableObject, IModule
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(1);

    private readonly ISettingsProvider<VrPerformanceSettings> _settingsProvider;
    private readonly IOpenVrSessionService _session;
    private readonly VrPerformanceSampler _sampler;
    private readonly IAppState _appState;
    private readonly IntegrationDisplayState _display;
    private readonly IPrivacyConsentService _consent;
    private readonly VrPerformanceDegradedTracker _degraded = new();
    private readonly object _lock = new();

    private IDisposable? _lease;
    private Timer? _timer;
    private bool _disposed;

    public VrPerformanceSettings Settings => _settingsProvider.Value;

    [ObservableProperty] private VrPerformanceSnapshot? _snapshot;
    [ObservableProperty] private string _combined = string.Empty;
    [ObservableProperty] private string _statusMessage = "Not started";
    [ObservableProperty] private bool _isDegraded;

    public string Name => "VrPerformance";
    public bool IsEnabled { get; set; } = true;
    public bool IsRunning { get; private set; }

    public VrPerformanceModule(
        ISettingsProvider<VrPerformanceSettings> settingsProvider,
        IOpenVrSessionService session,
        IAppState appState,
        IntegrationDisplayState display,
        IPrivacyConsentService consent)
    {
        _settingsProvider = settingsProvider;
        _session = session;
        _appState = appState;
        _display = display;
        _consent = consent;
        _sampler = new VrPerformanceSampler(session);
    }

    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task StartAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_disposed || IsRunning)
                return Task.CompletedTask;

            _lease = _session.AcquireLease(PrivacyHook.VrPerformance, Name);
            _timer = new Timer(_ => Tick(), null, TimeSpan.Zero, SampleInterval);
            IsRunning = true;
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
            _lease?.Dispose();
            _lease = null;
            IsRunning = false;
        }

        _sampler.Reset();
        _degraded.Reset();
        ClearOutput();
        return Task.CompletedTask;
    }

    private void Tick()
    {
        try
        {
            if (!_consent.IsApproved(PrivacyHook.VrPerformance))
            {
                SetStatus("Waiting for VR Performance permission");
                ClearOutput();
                return;
            }

            if (!_appState.IsVRRunning)
            {
                SetStatus("VR is not running");
                ClearOutput();
                return;
            }

            var snapshot = _sampler.Sample();
            Snapshot = snapshot;

            if (snapshot == null)
            {
                SetStatus(_session.IsAttached
                    ? "Collecting first sample..."
                    : _session.DescribeStatus());
                ClearOutput();
                return;
            }

            IsDegraded = _degraded.Update(snapshot, Settings, DateTime.UtcNow);
            SetStatus(_session.DescribeStatus());

            string text = VrPerformanceFormatter.Build(snapshot, Settings, IsDegraded);
            Combined = text;
            _display.VrPerformanceCombined = text;
            _display.VrPerformanceRunning = true;
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"VR performance tick failed: {ex.Message}");
            ClearOutput();
        }
    }

    private void SetStatus(string message)
    {
        StatusMessage = message;
        _display.VrPerformanceStatus = message;
    }

    private void ClearOutput()
    {
        Combined = string.Empty;
        _display.VrPerformanceCombined = string.Empty;
        _display.VrPerformanceRunning = false;
    }

    public void PropertyChangedHandler(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IntegrationSettings.IntgrVrPerformance))
            return;

        if (sender is not IntegrationSettings settings)
            return;

        if (settings.IntgrVrPerformance)
            _ = StartAsync();
        else
            _ = StopAsync();
    }

    public string DescribeStatus() => _session.DescribeStatus();

    public void SaveSettings() => _settingsProvider.Save();

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;
            _disposed = true;
        }

        StopAsync().GetAwaiter().GetResult();
    }
}
