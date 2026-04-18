using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.ViewModels;

/// <summary>
/// ViewModel for the Integrations page. Owns commands for tracker scan,
/// manual OSC build, media controls, soundpad controls, and admin relaunch.
/// Used as DataContext for IntegrationsPage.xaml.
/// </summary>
public partial class IntegrationsPageViewModel : ObservableObject
{
    private readonly ChatStatusDisplayState _chatStatus;
    private readonly Lazy<IModuleHost> _moduleHost;
    private readonly Lazy<OSCController> _osc;
    private readonly ISettingsProvider<IntegrationSettings> _integrationSettingsProvider;
    private readonly IMenuNavigationService _menuNav;
    private readonly IPrivacyConsentService _consent;
    private readonly IToastService _toast;
    public ISettingsProvider<IntegrationSettings> IntegrationSettingsProvider => _integrationSettingsProvider;
    private IMediaLinkService? _mediaLinkSvc;
    private IMediaLinkService MediaLink => _mediaLinkSvc ??= App.ApplicationMediaController;

    public IntegrationDisplayState IntegrationDisplay { get; }
    public IntegrationSettings IntegrationSettings { get; }
    public IModuleHost Modules => _moduleHost.Value;
    public MediaLinkDisplayState MediaLinkDisplay { get; }
    public MediaLinkSettings MediaLinkSettings { get; }
    public WeatherSettings WeatherSettings { get; }
    public TrackerDisplayState Tracker { get; }
    public IAppState AppState { get; }

    // Module references — lazily resolved to avoid circular deps
    private readonly Lazy<ComponentStatsViewModel> _componentStats;
    public ComponentStatsViewModel ComponentStats => _componentStats.Value;

    private TrackerBatteryModule? TrackerBatteryModule => _moduleHost.Value.TrackerBattery;
    private SoundpadModule? Soundpad => _moduleHost.Value.Soundpad;

    private readonly Lazy<ScanLoopService> _scanLoop;
    private readonly Lazy<IStatePersistenceCoordinator> _persistence;

    /// <summary>
    /// Initializes the integrations page ViewModel with module host, persistence, media, and
    /// OSC services needed to coordinate all integration panels.
    /// </summary>
    public IntegrationsPageViewModel(
        ChatStatusDisplayState chatStatus,
        Lazy<IModuleHost> moduleHost,
        Lazy<OSCController> osc,
        ISettingsProvider<IntegrationSettings> integrationSettingsProvider,
        ISettingsProvider<MediaLinkSettings> mediaLinkSettingsProvider,
        ISettingsProvider<WeatherSettings> weatherSettingsProvider,
        Lazy<ComponentStatsViewModel> componentStats,
        Lazy<ScanLoopService> scanLoop,
        Lazy<IStatePersistenceCoordinator> persistence,
        IntegrationDisplayState integrationDisplay,
        MediaLinkDisplayState mediaLinkDisplay,
        TrackerDisplayState tracker,
        IAppState appState,
        IMenuNavigationService menuNav,
        IPrivacyConsentService consent,
        IToastService toast)
    {
        _chatStatus = chatStatus;
        _moduleHost = moduleHost;
        _osc = osc;
        _integrationSettingsProvider = integrationSettingsProvider;
        _componentStats = componentStats;
        _scanLoop = scanLoop;
        _persistence = persistence;
        IntegrationDisplay = integrationDisplay;
        IntegrationSettings = integrationSettingsProvider.Value;
        MediaLinkDisplay = mediaLinkDisplay;
        MediaLinkSettings = mediaLinkSettingsProvider.Value;
        WeatherSettings = weatherSettingsProvider.Value;
        Tracker = tracker;
        AppState = appState;
        _menuNav = menuNav;
        _consent = consent;
        _toast = toast;

        // Guard map: property name → (required hook, value getter, revert action).
        // Note: ComponentStats is intentionally excluded — it has a basic-mode fallback
        // (CPU%/RAM) that runs without the kernel driver, even when HardwareMonitor is denied.
        _guardMap = new Dictionary<string, (PrivacyHook Hook, Func<bool> GetValue, Action Revert)>
        {
            { nameof(IntegrationSettings.IntgrScanWindowActivity), (PrivacyHook.WindowActivity,   () => IntegrationSettings.IntgrScanWindowActivity, () => IntegrationSettings.IntgrScanWindowActivity = false) },
            { nameof(IntegrationSettings.IntgrScanMediaLink),      (PrivacyHook.MediaSession,     () => IntegrationSettings.IntgrScanMediaLink,       () => IntegrationSettings.IntgrScanMediaLink = false) },
            { nameof(IntegrationSettings.IntgrTwitch),             (PrivacyHook.InternetAccess,   () => IntegrationSettings.IntgrTwitch,              () => IntegrationSettings.IntgrTwitch = false) },
            { nameof(IntegrationSettings.IntgrHeartRate),          (PrivacyHook.InternetAccess,   () => IntegrationSettings.IntgrHeartRate,           () => IntegrationSettings.IntgrHeartRate = false) },
            { nameof(IntegrationSettings.IntgrTrackerBattery),     (PrivacyHook.VrTrackerBattery, () => IntegrationSettings.IntgrTrackerBattery,      () => IntegrationSettings.IntgrTrackerBattery = false) },
            { nameof(IntegrationSettings.IntgrNetworkStatistics),  (PrivacyHook.NetworkStats,     () => IntegrationSettings.IntgrNetworkStatistics,   () => IntegrationSettings.IntgrNetworkStatistics = false) },
            { nameof(IntegrationSettings.IntgrSoundpad),           (PrivacyHook.SoundpadBridge,   () => IntegrationSettings.IntgrSoundpad,            () => IntegrationSettings.IntgrSoundpad = false) },
        };

        IntegrationSettings.PropertyChanged += OnIntegrationSettingChanged;
    }

    // Instance guard map built in constructor so closures capture the correct IntegrationSettings instance.
    private readonly Dictionary<string, (PrivacyHook Hook, Func<bool> GetValue, Action Revert)> _guardMap;

    private void OnIntegrationSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == null) return;
        if (!_guardMap.TryGetValue(e.PropertyName, out var guard)) return;
        if (!guard.GetValue()) return; // only enforce when being turned ON
        if (_consent.IsApproved(guard.Hook)) return;

        guard.Revert(); // flip the toggle back to false (safe: SetProperty is no-op when value unchanged)
        var (name, icon) = PrivacyHookInfo.Get(guard.Hook);
        _toast.Show(
            "🔒 Permission Required",
            $"{icon} {name} access is needed. Enable it in Privacy & Permissions.",
            ToastType.Warning,
            new ToastAction("Open Privacy & Permissions", () => { _menuNav.NavigateToPrivacy(); return Task.CompletedTask; }),
            durationMs: 6000,
            key: $"consent-{guard.Hook}");
    }

    [RelayCommand]
    private void ActivateSetting(string settingName)
        => _menuNav.ActivateSetting(settingName);

    [RelayCommand]
    private void TrackerBatteryScan() => ScanTrackerBatteryDevices();

    [RelayCommand]
    private void ManualBuildOsc()
    {
        if (!_chatStatus.ScanPause)
            _osc.Value.BuildOSC();
        _integrationSettingsProvider.Save();
    }

    [RelayCommand]
    private void RestartAsAdmin() => ExecuteRestartAsAdmin();

    [RelayCommand]
    private void MediaPlayPause(MediaSessionInfo? m)
    { if (m != null) MediaLink.MediaManager_PlayPauseAsync(m); }

    [RelayCommand]
    private void MediaNext(MediaSessionInfo? m)
    { if (m != null) MediaLink.MediaManager_NextAsync(m); }

    [RelayCommand]
    private void MediaPrevious(MediaSessionInfo? m)
    { if (m != null) MediaLink.MediaManager_PreviousAsync(m); }

    [RelayCommand]
    private void SoundpadPlayPause() => Soundpad?.TogglePause();

    [RelayCommand]
    private void SoundpadPrevious() => Soundpad?.PlayPreviousSound();

    [RelayCommand]
    private void SoundpadNext() => Soundpad?.PlayNextSound();

    [RelayCommand]
    private void SoundpadStop() => Soundpad?.StopSound();

    [RelayCommand]
    private void SoundpadRandom() => Soundpad?.PlayRandomSound();

    private void ScanTrackerBatteryDevices()
    {
        if (TrackerBatteryModule != null)
        {
            TrackerBatteryModule.UpdateDevices();
            TrackerBatteryModule.BuildChatboxString();
        }
    }

    private void ExecuteRestartAsAdmin()
    {
        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        if (principal.IsInRole(WindowsBuiltInRole.Administrator))
            return;

        _persistence.Value.PersistAllState();
        try
        {
            var proc = new ProcessStartInfo
            {
                UseShellExecute = true,
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = Process.GetCurrentProcess().MainModule?.FileName,
                Verb = "runas"
            };
            Process.Start(proc);
            Thread.Sleep(1000);
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
    }

    /// <summary>
    /// Seeks a media session to a specific position based on progress bar click.
    /// </summary>
    public async Task SeekMedia(MediaSessionInfo? session, double progressFraction, double maximum)
    {
        if (session == null) return;
        try
        {
            double position = progressFraction * maximum;
            await MediaLink.MediaManager_SeekTo(session, position);
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"Media seek failed: {ex.Message}");
        }
    }
}
