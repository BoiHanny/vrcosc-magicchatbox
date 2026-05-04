using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Core.Toast;
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
    private readonly ISettingsProvider<SpotifySettings> _spotifySettingsProvider;
    private readonly IMenuNavigationService _menuNav;
    private readonly INavigationService _nav;
    private readonly IPrivacyConsentService _consent;
    private readonly IToastService _toast;
    public ISettingsProvider<IntegrationSettings> IntegrationSettingsProvider => _integrationSettingsProvider;
    private IMediaLinkService? _mediaLinkSvc;
    private IMediaLinkService MediaLink => _mediaLinkSvc ??= App.ApplicationMediaController;

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
        ISettingsProvider<SpotifySettings> spotifySettingsProvider,
        ISettingsProvider<WeatherSettings> weatherSettingsProvider,
        Lazy<ComponentStatsViewModel> componentStats,
        Lazy<ScanLoopService> scanLoop,
        Lazy<IStatePersistenceCoordinator> persistence,
        IntegrationDisplayState integrationDisplay,
        MediaLinkDisplayState mediaLinkDisplay,
        SpotifyDisplayState spotifyDisplay,
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
        MediaLinkSettings = mediaLinkSettingsProvider.Value;
        SpotifySettings = spotifySettingsProvider.Value;
        WeatherSettings = weatherSettingsProvider.Value;
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

        // Guard map: property name → (required hook, value getter, revert action).
        // Note: ComponentStats is intentionally excluded — it has a basic-mode fallback
        // (CPU%/RAM) that runs without the kernel driver, even when HardwareMonitor is denied.
        _guardMap = new Dictionary<string, (PrivacyHook Hook, Func<bool> GetValue, Action Revert)>
        {
            { nameof(IntegrationSettings.IntgrScanWindowActivity), (PrivacyHook.WindowActivity,   () => IntegrationSettings.IntgrScanWindowActivity, () => IntegrationSettings.IntgrScanWindowActivity = false) },
            { nameof(IntegrationSettings.IntgrScanMediaLink),      (PrivacyHook.MediaSession,     () => IntegrationSettings.IntgrScanMediaLink,       () => IntegrationSettings.IntgrScanMediaLink = false) },
            { nameof(IntegrationSettings.IntgrSpotify),            (PrivacyHook.InternetAccess,   () => IntegrationSettings.IntgrSpotify,            () => IntegrationSettings.IntgrSpotify = false) },
            { nameof(IntegrationSettings.IntgrTwitch),             (PrivacyHook.InternetAccess,   () => IntegrationSettings.IntgrTwitch,              () => IntegrationSettings.IntgrTwitch = false) },
            { nameof(IntegrationSettings.IntgrHeartRate),          (PrivacyHook.InternetAccess,   () => IntegrationSettings.IntgrHeartRate,           () => IntegrationSettings.IntgrHeartRate = false) },
            { nameof(IntegrationSettings.IntgrTrackerBattery),     (PrivacyHook.VrTrackerBattery, () => IntegrationSettings.IntgrTrackerBattery,      () => IntegrationSettings.IntgrTrackerBattery = false) },
            { nameof(IntegrationSettings.IntgrNetworkStatistics),  (PrivacyHook.NetworkStats,     () => IntegrationSettings.IntgrNetworkStatistics,   () => IntegrationSettings.IntgrNetworkStatistics = false) },
            { nameof(IntegrationSettings.IntgrSoundpad),           (PrivacyHook.SoundpadBridge,   () => IntegrationSettings.IntgrSoundpad,            () => IntegrationSettings.IntgrSoundpad = false) },
            { nameof(IntegrationSettings.IntgrVrcRadar),           (PrivacyHook.VrcLogReader,     () => IntegrationSettings.IntgrVrcRadar,            () => IntegrationSettings.IntgrVrcRadar = false) },
        };

        IntegrationSettings.PropertyChanged += OnIntegrationSettingChanged;
    }

    // Instance guard map built in constructor so closures capture the correct IntegrationSettings instance.
    private readonly Dictionary<string, (PrivacyHook Hook, Func<bool> GetValue, Action Revert)> _guardMap;

    public bool IsVRRunning => AppState.IsVRRunning;

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
            guard.Revert(); // flip the toggle back to false (safe: SetProperty is no-op when value unchanged)
            var (name, icon) = PrivacyHookInfo.Get(guard.Hook);
            _toast.Show(
                "🔒 Permission Required",
                $"{icon} {name} access is needed. Enable it in Privacy & Permissions.",
                ToastType.Warning,
                new ToastAction("Open Privacy & Permissions", () => { _menuNav.NavigateToPrivacy(); return Task.CompletedTask; }),
                durationMs: 6000,
                key: $"consent-{guard.Hook}");
            return;
        }

        if (e.PropertyName is nameof(IntegrationSettings.IntgrSpotify) or nameof(IntegrationSettings.IntgrScanMediaLink))
            HandleSpotifyMediaLinkCoexistence();
    }

    private void OnAppStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IAppState.IsVRRunning) || e.PropertyName == nameof(ViewModel.IsVRRunning))
            OnPropertyChanged(nameof(IsVRRunning));
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

        // Default to PreferSpotify for reliability and notify user via toast
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

    public string ComponentStatsAccessWarningText =>
        !_consent.IsApproved(PrivacyHook.HardwareMonitor)
            ? "Enable Hardware Monitor permission"
            : IsProcessElevated()
                ? "Some stats aren't available on this system"
                : "Some stats may need admin rights";

    public bool CanResolveComponentStatsAccessIssue =>
        !_consent.IsApproved(PrivacyHook.HardwareMonitor) || !IsProcessElevated();

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
        if (!_consent.IsApproved(PrivacyHook.HardwareMonitor))
        {
            _menuNav.NavigateToPrivacy();
            _toast.Show(
                "🔒 Permission Required",
                "Enable Hardware Monitor in Privacy & Permissions first.",
                ToastType.Warning,
                durationMs: 5000,
                key: "hw-monitor-consent-required");
            return;
        }

        if (IsProcessElevated())
        {
            _toast.Show(
                "🖥️ Hardware Monitor",
                "MagicChatbox is already running as administrator. Missing temp/power stats are likely unsupported or blocked on this system.",
                ToastType.Info,
                durationMs: 6000,
                key: "hw-monitor-already-elevated");
            return;
        }

        _persistence.Value.PersistAllState();
        try
        {
            string processPath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(processPath))
                throw new InvalidOperationException("Unable to determine the current executable path for admin relaunch.");

            var proc = new ProcessStartInfo
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(processPath) ?? Environment.CurrentDirectory,
                FileName = processPath,
                Arguments = BuildCurrentArgumentString(),
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

    private static bool IsProcessElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static string BuildCurrentArgumentString()
    {
        return string.Join(" ",
            Environment.GetCommandLineArgs()
                .Skip(1)
                .Select(QuoteCommandLineArgument));
    }

    private static string QuoteCommandLineArgument(string argument)
    {
        if (string.IsNullOrEmpty(argument))
            return "\"\"";

        return argument.Contains(' ') || argument.Contains('"')
            ? $"\"{argument.Replace("\"", "\\\"")}\""
            : argument;
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
