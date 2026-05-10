using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.UI.Tray;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;
using Windows.Media.Control;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace vrcosc_magicchatbox.Services;

internal enum TrayIntegration
{
    Status,
    MediaLink,
    Spotify,
    Twitch,
    Discord,
    Soundpad,
    WindowActivity,
    CurrentTime,
    Weather,
    HeartRate,
    ComponentStats,
    TrackerBattery,
    VrcRadar,
    NetworkStatistics
}

internal readonly record struct TrayIntegrationRouteState(
    bool Enabled,
    bool ActiveForCurrentMode,
    bool VrEnabled,
    bool DesktopEnabled);

public sealed class TrayIconService : ITrayIconService
{
    private readonly IUiDispatcher _ui;
    private readonly MediaLinkDisplayState _mediaLink;
    private readonly SpotifyDisplayState _spotify;
    private readonly Lazy<IModuleHost> _modules;
    private readonly IAppState _appState;
    private readonly ISettingsProvider<IntegrationSettings> _integrationSettingsProvider;
    private readonly ISettingsProvider<WeatherSettings> _weatherSettingsProvider;
    private readonly IntegrationSettings _integrationSettings;
    private readonly WeatherSettings _weatherSettings;
    private readonly TtsSettings _ttsSettings;
    private Forms.NotifyIcon? _notifyIcon;
    private Drawing.Icon? _icon;
    private TrayMenuWindow? _menuWindow;
    private MainWindow? _mainWindow;
    private ToastAction? _pendingNotificationAction;
    private bool _showMainWindowForPendingNotification = true;
    private int _mediaActionInProgress;
    private bool _disposed;

    public TrayIconService(
        IUiDispatcher ui,
        MediaLinkDisplayState mediaLink,
        SpotifyDisplayState spotify,
        Lazy<IModuleHost> modules,
        IAppState appState,
        ISettingsProvider<IntegrationSettings> integrationSettingsProvider,
        ISettingsProvider<WeatherSettings> weatherSettingsProvider,
        ISettingsProvider<TtsSettings> ttsSettingsProvider)
    {
        _ui = ui;
        _mediaLink = mediaLink;
        _spotify = spotify;
        _modules = modules;
        _appState = appState;
        _integrationSettingsProvider = integrationSettingsProvider;
        _weatherSettingsProvider = weatherSettingsProvider;
        _integrationSettings = integrationSettingsProvider.Value;
        _weatherSettings = weatherSettingsProvider.Value;
        _ttsSettings = ttsSettingsProvider.Value;
    }

    public bool IsInitialized => _notifyIcon is not null && !_disposed;

    public void Initialize(MainWindow mainWindow)
    {
        if (!_ui.CheckAccess())
        {
            _ui.BeginInvoke(() => Initialize(mainWindow));
            return;
        }

        _mainWindow = mainWindow;
        if (_notifyIcon is not null)
            return;

        _icon = LoadTrayIcon();
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _icon,
            Text = "MagicChatbox",
            Visible = true
        };

        _notifyIcon.MouseUp += NotifyIcon_MouseUp;
        _notifyIcon.DoubleClick += NotifyIcon_DoubleClick;
        _notifyIcon.BalloonTipClicked += NotifyIcon_BalloonTipClicked;
    }

    public void Notify(string text, ToastAction? action = null, bool showMainWindowOnClick = true)
    {
        if (!_ui.CheckAccess())
        {
            _ui.BeginInvoke(() => Notify(text, action, showMainWindowOnClick));
            return;
        }

        if (_notifyIcon is null || string.IsNullOrWhiteSpace(text))
            return;

        _pendingNotificationAction = action;
        _showMainWindowForPendingNotification = showMainWindowOnClick;
        (string title, string body) = BuildNotificationContent(text);
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = body;
        _notifyIcon.BalloonTipIcon = Forms.ToolTipIcon.None;
        _notifyIcon.ShowBalloonTip(10000);
    }

    public void OpenContextMenu()
    {
        if (!_ui.CheckAccess())
        {
            _ui.BeginInvoke(OpenContextMenu);
            return;
        }

        ShowMenu();
    }

    internal MainWindow? ShowMainWindow()
    {
        MainWindow? mainWindow = _mainWindow ?? App.mainWindow;
        if (mainWindow is null)
            return null;

        mainWindow.Show();
        mainWindow.WindowState = WindowState.Normal;
        mainWindow.Activate();
        mainWindow.Focus();
        return mainWindow;
    }

    internal void ShowPage(int menuIndex)
    {
        MainWindow? mainWindow = ShowMainWindow();
        if (mainWindow is not null)
            mainWindow.VM.SelectedMenuIndex = menuIndex;

        HideMenu();
    }

    internal void ToggleMasterSwitch()
    {
        MainWindow? mainWindow = _mainWindow ?? App.mainWindow;
        if (mainWindow is null)
            return;

        mainWindow.VM.MasterSwitch = !mainWindow.VM.MasterSwitch;
        mainWindow.VM.MasterSwitchToggledCommand.Execute(true);
        RefreshMenu();
    }

    internal void ToggleAfk()
    {
        MainWindow? mainWindow = _mainWindow ?? App.mainWindow;
        if (mainWindow is null)
            return;

        mainWindow.VM.Modules.Afk.Settings.OverrideAfk = !mainWindow.VM.Modules.Afk.Settings.OverrideAfk;
        RefreshMenu();
    }

    internal void ToggleVoiceChat()
    {
        MainWindow? mainWindow = _mainWindow ?? App.mainWindow;
        if (mainWindow is null)
            return;

        mainWindow.VM.ToggleVoiceCommand.Execute(true);
        RefreshMenu();
    }

    internal string GetVoiceTrayText()
        => _ttsSettings.ToggleVoiceWithV ? "Toggle voice (Alt+V)" : "Toggle voice";

    internal bool TrySendTrayChat(string message)
    {
        string chat = message.Trim();
        if (string.IsNullOrWhiteSpace(chat))
            return false;

        MainWindow? mainWindow = _mainWindow ?? App.mainWindow;
        if (mainWindow is null)
            return false;

        ChattingPageViewModel chatViewModel = mainWindow.VM.Chatting;
        if (chat.Length > Core.Constants.MaxChatMessageLength)
        {
            int overmax = chat.Length - Core.Constants.MaxChatMessageLength;
            chatViewModel.ChatStatus.ChatFeedbackTxt = $"Message is {overmax} characters too long.";
            return false;
        }

        if (!mainWindow.VM.MasterSwitch)
        {
            chatViewModel.ChatStatus.ChatFeedbackTxt = "Sent to VRChat is off";
            return false;
        }

        return chatViewModel.TrySendChatText(chat, preserveCurrentInput: true);
    }

    internal MediaSessionInfo? GetWindowsMediaSession()
    {
        var sessions = _mediaLink.MediaSessions?.ToArray() ?? Array.Empty<MediaSessionInfo>();
        if (sessions.Length == 0)
            return null;

        bool spotifyWidgetVisible = HasSpotifyWidget();
        var candidates = spotifyWidgetVisible
            ? sessions.Where(s => !IsSpotifySession(s)).ToArray()
            : sessions;

        return candidates.FirstOrDefault(s => s.IsActive && IsControllablePlaybackState(s.PlaybackStatus))
            ?? candidates.FirstOrDefault(s => s.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            ?? candidates.FirstOrDefault(s => s.IsActive)
            ?? candidates.FirstOrDefault(s => IsControllablePlaybackState(s.PlaybackStatus));
    }

    internal bool HasSpotifyWidget()
        => _spotify.IsConnected && _spotify.HasPlayback;

    internal SpotifyDisplayState SpotifyDisplay => _spotify;

    internal StatusGroup[] GetStatusGroups()
    {
        MainWindow? mainWindow = _mainWindow ?? App.mainWindow;
        return mainWindow?.VM.Status.ChatStatus.GroupList.ToArray() ?? Array.Empty<StatusGroup>();
    }

    internal string SelectedStatusGroupId
    {
        get
        {
            MainWindow? mainWindow = _mainWindow ?? App.mainWindow;
            return mainWindow?.VM.Status.SelectedGroup?.GroupId ?? string.Empty;
        }
    }

    internal bool IsCycleStatusEnabled
    {
        get
        {
            MainWindow? mainWindow = _mainWindow ?? App.mainWindow;
            return mainWindow?.VM.AppSettingsInstance.CycleStatus ?? false;
        }
    }

    internal bool IsCycleOverrideCurrentGroupEnabled
    {
        get
        {
            MainWindow? mainWindow = _mainWindow ?? App.mainWindow;
            return mainWindow?.VM.AppSettingsInstance.CycleOverrideCurrentGroup ?? false;
        }
    }

    internal void SelectStatusGroup(string? groupId)
    {
        MainWindow? mainWindow = _mainWindow ?? App.mainWindow;
        if (mainWindow is null)
            return;

        mainWindow.VM.Status.SelectedGroup = string.IsNullOrEmpty(groupId)
            ? null
            : mainWindow.VM.Status.ChatStatus.GroupList.FirstOrDefault(g => g.GroupId == groupId);
        RefreshMenu();
    }

    internal void ToggleCycleStatus()
    {
        MainWindow? mainWindow = _mainWindow ?? App.mainWindow;
        if (mainWindow is null)
            return;

        mainWindow.VM.AppSettingsInstance.CycleStatus = !mainWindow.VM.AppSettingsInstance.CycleStatus;
        RefreshMenu();
    }

    internal void ToggleCycleOverrideCurrentGroup()
    {
        MainWindow? mainWindow = _mainWindow ?? App.mainWindow;
        if (mainWindow is null)
            return;

        var settings = mainWindow.VM.AppSettingsInstance;
        settings.CycleOverrideCurrentGroup = !settings.CycleOverrideCurrentGroup;
        settings.CycleOverrideGroupId = settings.CycleOverrideCurrentGroup
            ? mainWindow.VM.Status.SelectedGroup?.GroupId ?? string.Empty
            : string.Empty;
        RefreshMenu();
    }

    internal bool IsIntegrationEnabled(TrayIntegration integration)
        => integration switch
        {
            TrayIntegration.Status => _integrationSettings.IntgrStatus,
            TrayIntegration.MediaLink => _integrationSettings.IntgrScanMediaLink,
            TrayIntegration.Spotify => _integrationSettings.IntgrSpotify,
            TrayIntegration.Twitch => _integrationSettings.IntgrTwitch,
            TrayIntegration.Discord => _integrationSettings.IntgrDiscord,
            TrayIntegration.Soundpad => _integrationSettings.IntgrSoundpad,
            TrayIntegration.WindowActivity => _integrationSettings.IntgrScanWindowActivity,
            TrayIntegration.CurrentTime => _integrationSettings.IntgrScanWindowTime,
            TrayIntegration.Weather => _weatherSettings.ShowWeatherInTime,
            TrayIntegration.HeartRate => _integrationSettings.IntgrHeartRate,
            TrayIntegration.ComponentStats => _integrationSettings.IntgrComponentStats,
            TrayIntegration.TrackerBattery => _integrationSettings.IntgrTrackerBattery,
            TrayIntegration.VrcRadar => _integrationSettings.IntgrVrcRadar,
            TrayIntegration.NetworkStatistics => _integrationSettings.IntgrNetworkStatistics,
            _ => throw new ArgumentOutOfRangeException(nameof(integration), integration, null)
        };

    internal TrayIntegrationRouteState GetIntegrationRouteState(TrayIntegration integration)
    {
        bool enabled = IsIntegrationEnabled(integration);
        (bool vrEnabled, bool desktopEnabled) = GetIntegrationModeFlags(integration);
        bool routeEnabled = _appState.IsVRRunning ? vrEnabled : desktopEnabled;
        return new TrayIntegrationRouteState(enabled, enabled && routeEnabled, vrEnabled, desktopEnabled);
    }

    private (bool VrEnabled, bool DesktopEnabled) GetIntegrationModeFlags(TrayIntegration integration)
        => integration switch
        {
            TrayIntegration.Status => (_integrationSettings.IntgrStatus_VR, _integrationSettings.IntgrStatus_DESKTOP),
            TrayIntegration.MediaLink => (_integrationSettings.IntgrMediaLink_VR, _integrationSettings.IntgrMediaLink_DESKTOP),
            TrayIntegration.Spotify => (_integrationSettings.IntgrSpotify_VR, _integrationSettings.IntgrSpotify_DESKTOP),
            TrayIntegration.Twitch => (_integrationSettings.IntgrTwitch_VR, _integrationSettings.IntgrTwitch_DESKTOP),
            TrayIntegration.Discord => (_integrationSettings.IntgrDiscord_VR, _integrationSettings.IntgrDiscord_DESKTOP),
            TrayIntegration.Soundpad => (_integrationSettings.IntgrSoundpad_VR, _integrationSettings.IntgrSoundpad_DESKTOP),
            TrayIntegration.WindowActivity => (_integrationSettings.IntgrWindowActivity_VR, _integrationSettings.IntgrWindowActivity_DESKTOP),
            TrayIntegration.CurrentTime => (_integrationSettings.IntgrCurrentTime_VR, _integrationSettings.IntgrCurrentTime_DESKTOP),
            TrayIntegration.Weather => (_integrationSettings.IntgrWeather_VR, _integrationSettings.IntgrWeather_DESKTOP),
            TrayIntegration.HeartRate => (_integrationSettings.IntgrHeartRate_VR, _integrationSettings.IntgrHeartRate_DESKTOP),
            TrayIntegration.ComponentStats => (_integrationSettings.IntgrComponentStats_VR, _integrationSettings.IntgrComponentStats_DESKTOP),
            TrayIntegration.VrcRadar => (_integrationSettings.IntgrVrcRadar_VR, _integrationSettings.IntgrVrcRadar_DESKTOP),
            TrayIntegration.NetworkStatistics => (_integrationSettings.IntgrNetworkStatistics_VR, _integrationSettings.IntgrNetworkStatistics_DESKTOP),
            TrayIntegration.TrackerBattery => (true, true),
            _ => throw new ArgumentOutOfRangeException(nameof(integration), integration, null)
        };

    internal void ToggleIntegration(TrayIntegration integration)
    {
        bool enabled = !IsIntegrationEnabled(integration);
        switch (integration)
        {
            case TrayIntegration.Status:
                _integrationSettings.IntgrStatus = enabled;
                _integrationSettingsProvider.Save();
                break;
            case TrayIntegration.MediaLink:
                _integrationSettings.IntgrScanMediaLink = enabled;
                _integrationSettingsProvider.Save();
                break;
            case TrayIntegration.Spotify:
                _integrationSettings.IntgrSpotify = enabled;
                _integrationSettingsProvider.Save();
                break;
            case TrayIntegration.Twitch:
                _integrationSettings.IntgrTwitch = enabled;
                _integrationSettingsProvider.Save();
                break;
            case TrayIntegration.Discord:
                _integrationSettings.IntgrDiscord = enabled;
                _integrationSettingsProvider.Save();
                break;
            case TrayIntegration.Soundpad:
                _integrationSettings.IntgrSoundpad = enabled;
                _integrationSettingsProvider.Save();
                break;
            case TrayIntegration.WindowActivity:
                _integrationSettings.IntgrScanWindowActivity = enabled;
                _integrationSettingsProvider.Save();
                break;
            case TrayIntegration.CurrentTime:
                _integrationSettings.IntgrScanWindowTime = enabled;
                _integrationSettingsProvider.Save();
                break;
            case TrayIntegration.Weather:
                _weatherSettings.ShowWeatherInTime = enabled;
                _weatherSettingsProvider.Save();
                break;
            case TrayIntegration.HeartRate:
                _integrationSettings.IntgrHeartRate = enabled;
                _integrationSettingsProvider.Save();
                break;
            case TrayIntegration.ComponentStats:
                _integrationSettings.IntgrComponentStats = enabled;
                _integrationSettingsProvider.Save();
                break;
            case TrayIntegration.TrackerBattery:
                _integrationSettings.IntgrTrackerBattery = enabled;
                _integrationSettingsProvider.Save();
                break;
            case TrayIntegration.VrcRadar:
                _integrationSettings.IntgrVrcRadar = enabled;
                _integrationSettingsProvider.Save();
                break;
            case TrayIntegration.NetworkStatistics:
                _integrationSettings.IntgrNetworkStatistics = enabled;
                _integrationSettingsProvider.Save();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(integration), integration, null);
        }

        RefreshMenu();
    }

    internal void PreviousWindowsMedia()
    {
        RunMediaAction("Tray Windows media previous", async () =>
        {
            MediaSessionInfo? session = GetWindowsMediaSession();
            if (session is null)
                return;

            App.ApplicationMediaController.SelectMediaSession(session);
            await App.ApplicationMediaController.MediaManager_PreviousAsync(session);
        });
    }

    internal void ToggleWindowsMediaPlayback()
    {
        RunMediaAction("Tray Windows media play/pause", () =>
        {
            MediaSessionInfo? session = GetWindowsMediaSession();
            if (session is null)
                return Task.CompletedTask;

            App.ApplicationMediaController.SelectMediaSession(session);
            return App.ApplicationMediaController.MediaManager_PlayPauseAsync(session);
        });
    }

    internal void NextWindowsMedia()
    {
        RunMediaAction("Tray Windows media next", () =>
        {
            MediaSessionInfo? session = GetWindowsMediaSession();
            if (session is null)
                return Task.CompletedTask;

            App.ApplicationMediaController.SelectMediaSession(session);
            return App.ApplicationMediaController.MediaManager_NextAsync(session);
        });
    }

    internal void SeekWindowsMedia(double progressPercent)
    {
        double clampedPercent = Math.Clamp(progressPercent, 0d, 100d);
        RunMediaAction("Tray Windows media seek", () =>
        {
            MediaSessionInfo? session = GetWindowsMediaSession();
            if (session is null || session.FullTime <= TimeSpan.Zero || session.IsLiveTime)
                return Task.CompletedTask;

            App.ApplicationMediaController.SelectMediaSession(session);
            return App.ApplicationMediaController.MediaManager_SeekTo(session, clampedPercent);
        });
    }

    internal void PreviousSpotify()
        => RunMediaAction("Tray Spotify previous", () => _modules.Value.Spotify?.PreviousAsync() ?? Task.CompletedTask);

    internal void ToggleSpotifyPlayback()
        => RunMediaAction("Tray Spotify play/pause", () => _modules.Value.Spotify?.TogglePlayPauseAsync() ?? Task.CompletedTask);

    internal void NextSpotify()
        => RunMediaAction("Tray Spotify next", () => _modules.Value.Spotify?.NextAsync() ?? Task.CompletedTask);

    internal void ToggleSpotifyLike()
        => RunMediaAction("Tray Spotify like", () => _modules.Value.Spotify?.ToggleLikeAsync() ?? Task.CompletedTask);

    internal void ExitApplication()
    {
        HideMenu();

        MainWindow? mainWindow = _mainWindow ?? App.mainWindow;
        if (mainWindow is not null)
        {
            mainWindow._isTrayClosing = true;
            mainWindow.Close();
            return;
        }

        Application.Current.Shutdown();
    }

    internal void HideMenu()
    {
        _menuWindow?.HideAndReset();
    }

    internal void RefreshMenu()
    {
        MainWindow? mainWindow = _mainWindow ?? App.mainWindow;
        if (mainWindow is not null)
            _menuWindow?.RefreshFrom(mainWindow);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        HideMenu();
        if (_menuWindow is not null)
        {
            _menuWindow.Close();
            _menuWindow = null;
        }

        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.MouseUp -= NotifyIcon_MouseUp;
            _notifyIcon.DoubleClick -= NotifyIcon_DoubleClick;
            _notifyIcon.BalloonTipClicked -= NotifyIcon_BalloonTipClicked;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        _icon?.Dispose();
        _icon = null;
    }

    private void NotifyIcon_MouseUp(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Right)
            _ui.BeginInvoke(ShowMenu);
    }

    private void NotifyIcon_DoubleClick(object? sender, EventArgs e)
    {
        _ui.BeginInvoke(() =>
        {
            ShowMainWindow();
            HideMenu();
        });
    }

    private void NotifyIcon_BalloonTipClicked(object? sender, EventArgs e)
    {
        _ui.BeginInvoke(() => _ = ActivateNotificationAsync());
    }

    private void ShowMenu()
    {
        if (_disposed)
            return;

        _menuWindow ??= new TrayMenuWindow(this);
        RefreshMenu();
        _menuWindow.ShowNearCursor();
    }

    private static bool IsControllablePlaybackState(GlobalSystemMediaTransportControlsSessionPlaybackStatus status)
        => status is GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing
            or GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused;

    private static bool IsSpotifySession(MediaSessionInfo session)
    {
        string friendlyName = session.FriendlyAppName ?? string.Empty;
        string sessionId = session.Session?.Id ?? string.Empty;
        return friendlyName.Contains("spotify", StringComparison.OrdinalIgnoreCase) ||
               sessionId.Contains("spotify", StringComparison.OrdinalIgnoreCase);
    }

    private void RunMediaAction(string context, Func<Task> action)
    {
        if (Interlocked.Exchange(ref _mediaActionInProgress, 1) == 1)
            return;

        try
        {
            _ = RunMediaActionAsync(context, action);
        }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref _mediaActionInProgress, 0);
            Logging.WriteInfo($"{context} failed before starting: {ex.Message}");
            Logging.WriteException(ex, MSGBox: false);
        }
    }

    private async Task RunMediaActionAsync(string context, Func<Task> action)
    {
        try
        {
            await action();
            await Task.Delay(250);
            _ui.BeginInvoke(RefreshMenu);
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"{context} failed: {ex.Message}");
            Logging.WriteException(ex, MSGBox: false);
        }
        finally
        {
            Interlocked.Exchange(ref _mediaActionInProgress, 0);
        }
    }

    private async Task ActivateNotificationAsync()
    {
        ToastAction? action = _pendingNotificationAction;
        bool shouldShowMainWindow = _showMainWindowForPendingNotification;
        _pendingNotificationAction = null;
        _showMainWindowForPendingNotification = true;

        if (shouldShowMainWindow)
            ShowMainWindow();

        if (action is null)
            return;

        try
        {
            await action.Execute();
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
    }

    private static (string Title, string Body) BuildNotificationContent(string text)
    {
        string normalized = text.Trim();
        string[] lines = normalized
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', 2, StringSplitOptions.TrimEntries);

        if (lines.Length == 1)
            return (string.Empty, lines[0]);

        string title = lines[0].Equals("MagicChatbox", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : lines[0];

        return (title, lines[1]);
    }

    private static Drawing.Icon LoadTrayIcon()
    {
        string? processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
        {
            Drawing.Icon? extractedIcon = Drawing.Icon.ExtractAssociatedIcon(processPath);
            if (extractedIcon is not null)
            {
                Drawing.Icon clone = (Drawing.Icon)extractedIcon.Clone();
                extractedIcon.Dispose();
                return clone;
            }
        }

        return (Drawing.Icon)Drawing.SystemIcons.Application.Clone();
    }
}
