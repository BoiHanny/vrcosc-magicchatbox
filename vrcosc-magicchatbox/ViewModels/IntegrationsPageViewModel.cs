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
        IPrivacyConsentService consent)
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

        IntegrationSettings.PropertyChanged += OnIntegrationSettingChanged;
    }

    private static readonly Dictionary<string, PrivacyHook> _integrationHookMap = new()
    {
        { nameof(IntegrationSettings.IntgrTwitch), PrivacyHook.InternetAccess },
        { nameof(IntegrationSettings.IntgrHeartRate), PrivacyHook.InternetAccess },
        { nameof(IntegrationSettings.IntgrTrackerBattery), PrivacyHook.VrTrackerBattery },
        { nameof(IntegrationSettings.IntgrNetworkStatistics), PrivacyHook.NetworkStats },
        { nameof(IntegrationSettings.IntgrSoundpad), PrivacyHook.SoundpadBridge },
    };

    private void OnIntegrationSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == null) return;
        if (!_integrationHookMap.TryGetValue(e.PropertyName, out var hook)) return;
        if (!_consent.IsApproved(hook))
            _menuNav.NavigateToPrivacy();
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
