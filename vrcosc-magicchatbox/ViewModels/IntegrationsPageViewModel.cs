using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Osc;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.ViewModels;

public partial class IntegrationsPageViewModel : ObservableObject
{
    private readonly ChatStatusDisplayState _chatStatus;
    private readonly Lazy<IModuleHost> _moduleHost;
    private readonly Lazy<OSCController> _osc;
    private readonly ISettingsProvider<IntegrationSettings> _integrationSettingsProvider;
    private readonly ISettingsProvider<SpotifySettings> _spotifySettingsProvider;
    private readonly IMenuNavigationService _menuNav;
    private readonly INavigationService _nav;
    private readonly IPrivacyConsentService _consent;
    private readonly IToastService _toast;
    public ISettingsProvider<IntegrationSettings> IntegrationSettingsProvider => _integrationSettingsProvider;
    private IMediaLinkService? _mediaLinkSvc;
    private IMediaLinkService MediaLink => _mediaLinkSvc ??= App.ApplicationMediaController;
    private ModuleFaultTracker? _faultTracker;
    private ModuleFaultTracker FaultTracker => _faultTracker ??= App.Services.GetRequiredService<ModuleFaultTracker>();

    public IntegrationDisplayState IntegrationDisplay { get; }
    public IntegrationSettings IntegrationSettings { get; }
    public IModuleHost Modules => _moduleHost.Value;
    public MediaLinkDisplayState MediaLinkDisplay { get; }
    public SpotifyDisplayState SpotifyDisplay { get; }
    public MediaLinkSettings MediaLinkSettings { get; }
    public SpotifySettings SpotifySettings { get; }
    public WeatherSettings WeatherSettings { get; }
    public TrackerDisplayState Tracker { get; }
    public IAppState AppState { get; }

    private readonly Lazy<ComponentStatsViewModel> _componentStats;
    public ComponentStatsViewModel ComponentStats => _componentStats.Value;

    private TrackerBatteryModule? TrackerBatteryModule => _moduleHost.Value.TrackerBattery;
    private SoundpadModule? Soundpad => _moduleHost.Value.Soundpad;
    private SpotifyModule? Spotify => _moduleHost.Value.Spotify;

    private readonly Lazy<ScanLoopService> _scanLoop;
    private readonly Lazy<IStatePersistenceCoordinator> _persistence;

    public IntegrationsPageViewModel(
        ChatStatusDisplayState chatStatus,
        Lazy<IModuleHost> moduleHost,
        Lazy<OSCController> osc,
        ISettingsProvider<IntegrationSettings> integrationSettingsProvider,
        ISettingsProvider<MediaLinkSettings> mediaLinkSettingsProvider,
        ISettingsProvider<SpotifySettings> spotifySettingsProvider,
        ISettingsProvider<WeatherSettings> weatherSettingsProvider,
        ISettingsProvider<Classes.Modules.Vr.VrPerformanceSettings> vrPerformanceSettingsProvider,
        Lazy<ComponentStatsViewModel> componentStats,
        Lazy<ScanLoopService> scanLoop,
        Lazy<IStatePersistenceCoordinator> persistence,
        IntegrationDisplayState integrationDisplay,
        MediaLinkDisplayState mediaLinkDisplay,
        SpotifyDisplayState spotifyDisplay,
        LyricsDisplayState lyricsDisplay,
        TrackerDisplayState tracker,
        IAppState appState,
        IMenuNavigationService menuNav,
        INavigationService nav,
        IPrivacyConsentService consent,
        IToastService toast)
    {
        _chatStatus = chatStatus;
        _moduleHost = moduleHost;
        _osc = osc;
        _integrationSettingsProvider = integrationSettingsProvider;
        _spotifySettingsProvider = spotifySettingsProvider;
        _componentStats = componentStats;
        _scanLoop = scanLoop;
        _persistence = persistence;
        IntegrationDisplay = integrationDisplay;
        IntegrationSettings = integrationSettingsProvider.Value;
        MediaLinkDisplay = mediaLinkDisplay;
        SpotifyDisplay = spotifyDisplay;
        LyricsDisplay = lyricsDisplay;
        MediaLinkSettings = mediaLinkSettingsProvider.Value;
        SpotifySettings = spotifySettingsProvider.Value;
        WeatherSettings = weatherSettingsProvider.Value;
        VrPerformanceSettings = vrPerformanceSettingsProvider.Value;
        Tracker = tracker;
        AppState = appState;
        _menuNav = menuNav;
        _nav = nav;
        _consent = consent;
        _toast = toast;
        AppState.PropertyChanged += OnAppStatePropertyChanged;
        IntegrationDisplay.PropertyChanged += OnIntegrationDisplayPropertyChanged;
        SpotifyDisplay.PropertyChanged += OnSpotifyDisplayChanged;
        SpotifySettings.PropertyChanged += OnSpotifySettingsChanged;
        _consent.ConsentChanged += (_, e) =>
        {
            if (e.Hook == PrivacyHook.HardwareMonitor)
            {
                OnPropertyChanged(nameof(ComponentStatsAccessWarningText));
                OnPropertyChanged(nameof(CanResolveComponentStatsAccessIssue));
            }
        };

        _guardMap = new Dictionary<string, (PrivacyHook Hook, Func<bool> GetValue, Action Revert)>
        {
            { nameof(IntegrationSettings.IntgrComponentStats),      (PrivacyHook.HardwareMonitor, () => IntegrationSettings.IntgrComponentStats,      () => IntegrationSettings.IntgrComponentStats = false) },
            { nameof(IntegrationSettings.IntgrScanWindowActivity), (PrivacyHook.WindowActivity,   () => IntegrationSettings.IntgrScanWindowActivity, () => IntegrationSettings.IntgrScanWindowActivity = false) },
            { nameof(IntegrationSettings.IntgrScanMediaLink),      (PrivacyHook.MediaSession,     () => IntegrationSettings.IntgrScanMediaLink,       () => IntegrationSettings.IntgrScanMediaLink = false) },
            { nameof(IntegrationSettings.IntgrSpotify),            (PrivacyHook.InternetAccess,   () => IntegrationSettings.IntgrSpotify,            () => IntegrationSettings.IntgrSpotify = false) },
            { nameof(IntegrationSettings.IntgrTwitch),             (PrivacyHook.InternetAccess,   () => IntegrationSettings.IntgrTwitch,              () => IntegrationSettings.IntgrTwitch = false) },
            { nameof(IntegrationSettings.IntgrTikTokLive),         (PrivacyHook.InternetAccess,   () => IntegrationSettings.IntgrTikTokLive,          () => IntegrationSettings.IntgrTikTokLive = false) },
            { nameof(IntegrationSettings.IntgrHeartRate),          (PrivacyHook.InternetAccess,   () => IntegrationSettings.IntgrHeartRate,           () => IntegrationSettings.IntgrHeartRate = false) },
            { nameof(IntegrationSettings.IntgrTrackerBattery),     (PrivacyHook.VrTrackerBattery, () => IntegrationSettings.IntgrTrackerBattery,      () => IntegrationSettings.IntgrTrackerBattery = false) },
            { nameof(IntegrationSettings.IntgrNetworkStatistics),  (PrivacyHook.NetworkStats,     () => IntegrationSettings.IntgrNetworkStatistics,   () => IntegrationSettings.IntgrNetworkStatistics = false) },
            { nameof(IntegrationSettings.IntgrSoundpad),           (PrivacyHook.SoundpadBridge,   () => IntegrationSettings.IntgrSoundpad,            () => IntegrationSettings.IntgrSoundpad = false) },
            { nameof(IntegrationSettings.IntgrVrcRadar),           (PrivacyHook.VrcLogReader,     () => IntegrationSettings.IntgrVrcRadar,            () => IntegrationSettings.IntgrVrcRadar = false) },
            { nameof(IntegrationSettings.IntgrVrPerformance),      (PrivacyHook.VrPerformance,    () => IntegrationSettings.IntgrVrPerformance,       () => IntegrationSettings.IntgrVrPerformance = false) },
            { nameof(IntegrationSettings.IntgrLyrics),             (PrivacyHook.InternetAccess,   () => IntegrationSettings.IntgrLyrics,              () => IntegrationSettings.IntgrLyrics = false) },
        };

        _faultResetMap = new Dictionary<string, (string SortKey, Func<bool> GetValue)>
        {
            { nameof(IntegrationSettings.IntgrStatus),             ("Status",         () => IntegrationSettings.IntgrStatus) },
            { nameof(IntegrationSettings.IntgrScanWindowActivity), ("Window",         () => IntegrationSettings.IntgrScanWindowActivity) },
            { nameof(IntegrationSettings.IntgrScanWindowTime),     ("Time",           () => IntegrationSettings.IntgrScanWindowTime) },
            { nameof(IntegrationSettings.IntgrTwitch),             ("Twitch",         () => IntegrationSettings.IntgrTwitch) },
            { nameof(IntegrationSettings.IntgrTikTokLive),         ("TikTokLive",     () => IntegrationSettings.IntgrTikTokLive) },
            { nameof(IntegrationSettings.IntgrDiscord),            ("Discord",        () => IntegrationSettings.IntgrDiscord) },
            { nameof(IntegrationSettings.IntgrSpotify),            ("Spotify",        () => IntegrationSettings.IntgrSpotify) },
            { nameof(IntegrationSettings.IntgrVrcRadar),           ("VrcRadar",       () => IntegrationSettings.IntgrVrcRadar) },
            { nameof(IntegrationSettings.IntgrHeartRate),          ("HeartRate",      () => IntegrationSettings.IntgrHeartRate) },
            { nameof(IntegrationSettings.IntgrComponentStats),     ("Component",      () => IntegrationSettings.IntgrComponentStats) },
            { nameof(IntegrationSettings.IntgrTrackerBattery),     ("TrackerBattery", () => IntegrationSettings.IntgrTrackerBattery) },
            { nameof(IntegrationSettings.IntgrVrPerformance),      ("VrPerformance",  () => IntegrationSettings.IntgrVrPerformance) },
            { nameof(IntegrationSettings.IntgrLyrics),             ("Lyrics",         () => IntegrationSettings.IntgrLyrics) },
            { nameof(IntegrationSettings.IntgrNetworkStatistics),  ("Network",        () => IntegrationSettings.IntgrNetworkStatistics) },
            { nameof(IntegrationSettings.IntgrWeather_VR),         ("Weather",        () => IntegrationSettings.IntgrWeather_VR) },
            { nameof(IntegrationSettings.IntgrWeather_DESKTOP),    ("Weather",        () => IntegrationSettings.IntgrWeather_DESKTOP) },
            { nameof(IntegrationSettings.IntgrScanMediaLink),      ("MediaLink",      () => IntegrationSettings.IntgrScanMediaLink) },
            { nameof(IntegrationSettings.IntgrSoundpad),           ("Soundpad",       () => IntegrationSettings.IntgrSoundpad) },
        };

        IntegrationSettings.PropertyChanged += OnIntegrationSettingChanged;
    }

    private readonly Dictionary<string, (PrivacyHook Hook, Func<bool> GetValue, Action Revert)> _guardMap;
    private readonly Dictionary<string, (string SortKey, Func<bool> GetValue)> _faultResetMap;

    public bool IsVRRunning => AppState.IsVRRunning;

    public Classes.Modules.Vr.VrPerformanceSettings VrPerformanceSettings { get; }

    public LyricsDisplayState LyricsDisplay { get; }

    public IReadOnlyList<Classes.Modules.Vr.VrPerformanceDisplayMode> VrPerformanceDisplayModes { get; } =
        (Classes.Modules.Vr.VrPerformanceDisplayMode[])Enum.GetValues(typeof(Classes.Modules.Vr.VrPerformanceDisplayMode));

    public string TrackerBattery_LastScanDisplay => IntegrationDisplay.TrackerBatteryLastScanDisplay;

    public double NetworkStats_Opacity => ParseOpacity(IntegrationDisplay.NetworkStatsOpacity);

    public string SpotifyWidgetTitle => ResolveSpotifyWidgetText(
        SpotifyDisplay.Title,
        SpotifySettings.AllowTrackTitleInOutput,
        SpotifyDisplay.HasPlayback ? "Unknown track" : "Nothing playing");

    public string SpotifyWidgetArtist => ResolveSpotifyWidgetText(
        SpotifyDisplay.Artist,
        SpotifySettings.AllowArtistInOutput,
        SpotifyDisplay.IsConnected ? SpotifyDisplay.StatusText : "Connect Spotify to start");

    public string SpotifyWidgetAlbum => ResolveSpotifyWidgetText(
        SpotifyDisplay.Album,
        SpotifySettings.AllowAlbumInOutput,
        string.Empty);

    private void OnIntegrationSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == null) return;
        if (_guardMap.TryGetValue(e.PropertyName, out var guard) && guard.GetValue() && !_consent.IsApproved(guard.Hook))
        {
            guard.Revert();            var (name, icon) = PrivacyHookInfo.Get(guard.Hook);
            _toast.Show(
                "🔒 Permission Required",
                $"{icon} {name} access is needed. Enable it in Privacy & Permissions.",
                ToastType.Warning,
                new ToastAction("Open Privacy & Permissions", () => { _menuNav.NavigateToPrivacy(); return Task.CompletedTask; }),
                durationMs: 6000,
                key: $"consent-{guard.Hook}");
            return;
        }

        if (_faultResetMap.TryGetValue(e.PropertyName, out var faultReset) && faultReset.GetValue())
            FaultTracker.ResetFault(faultReset.SortKey);

        HandleModeVisibility(e.PropertyName);

        if (e.PropertyName is nameof(IntegrationSettings.IntgrSpotify) or nameof(IntegrationSettings.IntgrScanMediaLink))
            HandleSpotifyMediaLinkCoexistence();
    }

    private void HandleModeVisibility(string propertyName)
    {
        if (!propertyName.StartsWith("Intgr", StringComparison.Ordinal))
            return;

        if (IntegrationModeVisibility.TryDescribeHiddenMode(
                IntegrationSettings, propertyName, AppState.IsVRRunning, out var hidden))
        {
            string mode = AppState.IsVRRunning ? "VR" : "Desktop";
            _toast.Show(
                "👁️ Not shown in this mode",
                hidden.CanEnableInCurrentMode
                    ? $"{hidden.DisplayName} is on, but its {mode} switch is off — it won't appear in {mode} mode."
                    : $"{hidden.DisplayName} is on, but it can't run in {mode} mode.",
                ToastType.Warning,
                hidden.CanEnableInCurrentMode
                    ? new ToastAction($"Show in {mode} mode", () =>
                    {
                        if (IntegrationModeVisibility.TryEnableCurrentMode(
                                IntegrationSettings, propertyName, AppState.IsVRRunning, out _))
                            _integrationSettingsProvider.Save();

                        OnPropertyChanged(nameof(ModeVisibilityWarning));
                        OnPropertyChanged(nameof(HasModeVisibilityWarning));
                        return Task.CompletedTask;
                    })
                    : null,
                durationMs: 8000,
                key: $"mode-visibility-{propertyName}");
        }

        OnPropertyChanged(nameof(ModeVisibilityWarning));
        OnPropertyChanged(nameof(HasModeVisibilityWarning));
    }

    public string? ModeVisibilityWarning
        => IntegrationModeVisibility.BuildWarning(IntegrationSettings, AppState.IsVRRunning);

    public bool HasModeVisibilityWarning => ModeVisibilityWarning != null;

    private void OnAppStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IAppState.IsVRRunning) || e.PropertyName == nameof(ViewModel.IsVRRunning))
        {
            OnPropertyChanged(nameof(IsVRRunning));
            OnPropertyChanged(nameof(ModeVisibilityWarning));
            OnPropertyChanged(nameof(HasModeVisibilityWarning));
        }
    }

    private void OnIntegrationDisplayPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IntegrationDisplayState.TrackerBatteryLastScanDisplay):
                OnPropertyChanged(nameof(TrackerBattery_LastScanDisplay));
                break;
            case nameof(IntegrationDisplayState.NetworkStatsOpacity):
                OnPropertyChanged(nameof(NetworkStats_Opacity));
                break;
        }
    }

    private void OnSpotifyDisplayChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SpotifyDisplayState.Title) or
            nameof(SpotifyDisplayState.Artist) or
            nameof(SpotifyDisplayState.Album) or
            nameof(SpotifyDisplayState.HasPlayback) or
            nameof(SpotifyDisplayState.IsConnected) or
            nameof(SpotifyDisplayState.StatusText))
        {
            NotifySpotifyWidgetTextChanged();
        }
    }

    private void OnSpotifySettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SpotifySettings.PrivacyMode) or
            nameof(SpotifySettings.PrivacyHiddenText) or
            nameof(SpotifySettings.AllowTrackTitleInOutput) or
            nameof(SpotifySettings.AllowArtistInOutput) or
            nameof(SpotifySettings.AllowAlbumInOutput))
        {
            NotifySpotifyWidgetTextChanged();
        }
    }

    private void NotifySpotifyWidgetTextChanged()
    {
        OnPropertyChanged(nameof(SpotifyWidgetTitle));
        OnPropertyChanged(nameof(SpotifyWidgetArtist));
        OnPropertyChanged(nameof(SpotifyWidgetAlbum));
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
            _osc.Value.BuildOSC(allowExternalRefresh: false);
        _integrationSettingsProvider.Save();
    }

    [RelayCommand]
    private void ResolveComponentStatsAccess() => ExecuteResolveComponentStatsAccess();

    [RelayCommand]
    private async Task MediaPlayPause(MediaSessionInfo? m)
    { if (m != null) await MediaLink.MediaManager_PlayPauseAsync(m); }

    [RelayCommand]
    private async Task MediaNext(MediaSessionInfo? m)
    { if (m != null) await MediaLink.MediaManager_NextAsync(m); }

    [RelayCommand]
    private async Task MediaPrevious(MediaSessionInfo? m)
    { if (m != null) await MediaLink.MediaManager_PreviousAsync(m); }

    [RelayCommand]
    private void SelectMediaSession(MediaSessionInfo? m)
    {
        if (m == null)
            return;

        MediaLink.SelectMediaSession(m);
        if (!_chatStatus.ScanPause)
            _osc.Value.BuildOSC(allowExternalRefresh: false);
    }

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

    [RelayCommand]
    private async Task SpotifyPlayPause()
    {
        if (Spotify != null)
            await Spotify.TogglePlayPauseAsync();
    }

    [RelayCommand]
    private async Task SpotifyPrevious()
    {
        if (Spotify != null)
            await Spotify.PreviousAsync();
    }

    [RelayCommand]
    private async Task SpotifyNext()
    {
        if (Spotify != null)
            await Spotify.NextAsync();
    }

    [RelayCommand]
    private async Task SpotifyToggleLike()
    {
        if (Spotify != null)
            await Spotify.ToggleLikeAsync();
    }

    [RelayCommand]
    private async Task SpotifyToggleShuffle()
    {
        if (Spotify != null)
            await Spotify.ToggleShuffleAsync();
    }

    [RelayCommand]
    private async Task SpotifyCycleRepeat()
    {
        if (Spotify != null)
            await Spotify.CycleRepeatAsync();
    }

    [RelayCommand]
    private async Task SpotifyRefresh()
    {
        if (Spotify != null)
            await Spotify.TriggerManualRefreshAsync();
    }

    [RelayCommand]
    private void SpotifyOpenCurrentTrack()
    {
        if (SpotifyDisplay.CanOpenSpotify)
            _nav.OpenUrl(SpotifyDisplay.ExternalUrl);
    }

    public async Task SetSpotifyVolume(double value)
    {
        if (Spotify != null)
            await Spotify.SetVolumeAsync((int)Math.Clamp(value, 0, 100));
    }

    private void HandleSpotifyMediaLinkCoexistence()
    {
        if (!IntegrationSettings.IntgrSpotify ||
            !IntegrationSettings.IntgrScanMediaLink ||
            SpotifySettings.MediaLinkCoexistence != SpotifyMediaLinkCoexistence.Ask)
            return;

        SpotifySettings.MediaLinkCoexistence = SpotifyMediaLinkCoexistence.PreferSpotify;
        _spotifySettingsProvider.Save();

        _toast.Show(
            "🎵 Spotify + MediaLink",
            "Both are enabled — defaulting to dedicated Spotify output. Change this in Spotify options under 'MediaLink coexistence'.",
            ToastType.Info,
            new ToastAction("Open Spotify settings", () => { _menuNav.ActivateSetting("Settings_Spotify"); return Task.CompletedTask; }),
            durationMs: 8000,
            key: "spotify-medialink-coexist");
    }

    public string ComponentStatsAccessWarningText => "Enable Hardware Monitor permission";

    public bool CanResolveComponentStatsAccessIssue => !_consent.IsApproved(PrivacyHook.HardwareMonitor);

    private void ScanTrackerBatteryDevices()
    {
        if (TrackerBatteryModule != null)
        {
            TrackerBatteryModule.UpdateDevices();
            TrackerBatteryModule.BuildChatboxString();
        }
    }

    private void ExecuteResolveComponentStatsAccess()
    {
        if (_consent.IsApproved(PrivacyHook.HardwareMonitor))
            return;

        _menuNav.NavigateToPrivacy();
        _toast.Show(
            "🔒 Permission Required",
            "Enable Hardware Monitor in Privacy & Permissions to read CPU and GPU stats.",
            ToastType.Warning,
            durationMs: 5000,
            key: "hw-monitor-consent-required");
    }

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

    private static double ParseOpacity(string? value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double opacity)
            ? opacity
            : 1d;
    }

    private string ResolveSpotifyWidgetText(string value, bool allowed, string fallback)
    {
        if (!allowed || SpotifySettings.PrivacyMode)
            return SpotifySettings.PrivacyHiddenText;

        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
