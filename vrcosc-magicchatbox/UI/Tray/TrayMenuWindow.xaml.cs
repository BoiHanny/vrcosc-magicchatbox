using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Documents;
using System.Windows.Threading;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;
using Forms = System.Windows.Forms;
using Windows.Media.Control;

namespace vrcosc_magicchatbox.UI.Tray;

public partial class TrayMenuWindow : Window
{
    private const double ScreenMargin = 10;
    private static readonly Brush SelectedStatusGroupBackground = CreateBrush("#2631B7B4");
    private static readonly Brush StatusGroupBackground = CreateBrush("#102D315D");
    private static readonly Brush SelectedStatusGroupBorder = CreateBrush("#8031B7B4");
    private static readonly Brush StatusGroupBorder = CreateBrush("#3D7169B7");
    private readonly TrayIconService _trayIconService;
    private readonly DispatcherTimer _mediaRefreshTimer;
    private bool _isIntegrationPanelOpen;
    private bool _isStatusPanelOpen;

    public TrayMenuWindow(TrayIconService trayIconService)
    {
        InitializeComponent();
        _trayIconService = trayIconService;
        _mediaRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _mediaRefreshTimer.Tick += (_, _) =>
        {
            RefreshWindowsMedia();
            RefreshSpotify();
        };
    }

    public void RefreshFrom(MainWindow mainWindow)
    {
        MasterSwitch.IsChecked = mainWindow.VM.MasterSwitch;
        MasterSwitchStateText.Text = mainWindow.VM.MasterSwitch ? "On" : "Off";
        MasterSwitchStateText.Foreground = mainWindow.VM.MasterSwitch
            ? (Brush)FindResource("AccentTealBrush")
            : (Brush)FindResource("TextMutedBrush");

        AfkSwitch.IsChecked = mainWindow.VM.Modules.Afk.Settings.OverrideAfk;
        AfkSwitchStateText.Text = mainWindow.VM.Modules.Afk.Settings.OverrideAfk ? "On" : "Off";
        AfkSwitchStateText.Foreground = mainWindow.VM.Modules.Afk.Settings.OverrideAfk
            ? (Brush)FindResource("AccentTealBrush")
            : (Brush)FindResource("TextMutedBrush");

        VoiceChatText.Text = _trayIconService.GetVoiceTrayText();

        RefreshWindowsMedia();
        RefreshSpotify();
        RefreshIntegrationPanel();
        RefreshIntegrationToggles();
        RefreshStatusPanel();
        RefreshStatusControls();
        ApplyDetailPanelLayout();
    }

    public void ShowNearCursor()
    {
        CollapseDetailPanels();

        if (!IsVisible)
            Show();

        UpdateLayout();

        Point cursor = GetCursorPositionInDips();
        Rect workArea = GetCurrentScreenWorkAreaInDips();
        double width = ActualWidth > 0 ? ActualWidth : Width;
        double height = ActualHeight;

        Left = Clamp(cursor.X - width + 12, workArea.Left + ScreenMargin, workArea.Right - width - ScreenMargin);
        Top = Clamp(cursor.Y - height - 6, workArea.Top + ScreenMargin, workArea.Bottom - height - ScreenMargin);

        Activate();
        _mediaRefreshTimer.Start();
        Dispatcher.BeginInvoke(FocusTrayChatInput, DispatcherPriority.Input);
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        _trayIconService.ShowMainWindow();
        _trayIconService.HideMenu();
    }

    private void Integrations_Click(object sender, RoutedEventArgs e) => _trayIconService.ShowPage(0);

    private void Status_Click(object sender, RoutedEventArgs e) => _trayIconService.ShowPage(1);

    private void Chatting_Click(object sender, RoutedEventArgs e) => _trayIconService.ShowPage(2);

    private void Options_Click(object sender, RoutedEventArgs e) => _trayIconService.ShowPage(3);

    private void MasterSwitch_Click(object sender, RoutedEventArgs e) => _trayIconService.ToggleMasterSwitch();

    private void AfkSwitch_Click(object sender, RoutedEventArgs e) => _trayIconService.ToggleAfk();

    private void VoiceChat_Click(object sender, RoutedEventArgs e) => _trayIconService.ToggleVoiceChat();

    private void TrayChatSend_Click(object sender, RoutedEventArgs e) => SendTrayChatInput();

    private void TrayChatPaste_Click(object sender, RoutedEventArgs e) => PasteIntoTrayChat();

    private void TrayChatClear_Click(object sender, RoutedEventArgs e)
    {
        TrayChatText.Clear();
        FocusTrayChatInput();
    }

    private void TrayChatText_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => CollapseDetailPanels();

    private void CollapseDetailPanels_PreviewMouseDown(object sender, MouseButtonEventArgs e) => CollapseDetailPanels(clearKeyboardFocus: true);

    private void TrayChatText_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SendTrayChatInput();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            TrayChatText.Clear();
            e.Handled = true;
        }
    }

    private void TrayChatText_TextChanged(object sender, TextChangedEventArgs e)
    {
        int count = TrayChatText.Text?.Length ?? 0;
        TrayChatCount.Text = $"{count}/{Core.Constants.MaxChatMessageLength}";
        TrayChatCount.Foreground = count > Core.Constants.MaxChatMessageLength
            ? (Brush)FindResource("TextDangerRedBrush")
            : (Brush)FindResource("TextMutedBrush");
    }

    private void IntegrationPanel_Click(object sender, RoutedEventArgs e)
    {
        Keyboard.Focus(IntegrationPanelSwitch);
        bool shouldOpen = !_isIntegrationPanelOpen;
        _isIntegrationPanelOpen = shouldOpen;
        if (shouldOpen)
            _isStatusPanelOpen = false;

        RefreshStatusPanel();
        RefreshIntegrationPanel();
    }

    private void StatusPanel_Click(object sender, RoutedEventArgs e)
    {
        Keyboard.Focus(StatusPanelSwitch);
        bool shouldOpen = !_isStatusPanelOpen;
        _isStatusPanelOpen = shouldOpen;
        if (shouldOpen)
            _isIntegrationPanelOpen = false;

        RefreshIntegrationPanel();
        RefreshStatusPanel();
    }

    private void StatusCycle_Click(object sender, RoutedEventArgs e) => _trayIconService.ToggleCycleStatus();

    private void StatusCycleOverride_Click(object sender, RoutedEventArgs e) => _trayIconService.ToggleCycleOverrideCurrentGroup();

    private void StatusGroupButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
            _trayIconService.SelectStatusGroup(button.Tag as string);
    }

    private void StatusPage_Click(object sender, RoutedEventArgs e) => _trayIconService.ShowPage(1);

    private void MediaLinkIntegration_Click(object sender, RoutedEventArgs e) => _trayIconService.ToggleIntegration(TrayIntegration.MediaLink);

    private void StatusIntegration_Click(object sender, RoutedEventArgs e) => _trayIconService.ToggleIntegration(TrayIntegration.Status);

    private void SpotifyIntegration_Click(object sender, RoutedEventArgs e) => _trayIconService.ToggleIntegration(TrayIntegration.Spotify);

    private void TwitchIntegration_Click(object sender, RoutedEventArgs e) => _trayIconService.ToggleIntegration(TrayIntegration.Twitch);

    private void TikTokIntegration_Click(object sender, RoutedEventArgs e) => _trayIconService.ToggleIntegration(TrayIntegration.TikTok);

    private void DiscordIntegration_Click(object sender, RoutedEventArgs e) => _trayIconService.ToggleIntegration(TrayIntegration.Discord);

    private void SoundpadIntegration_Click(object sender, RoutedEventArgs e) => _trayIconService.ToggleIntegration(TrayIntegration.Soundpad);

    private void WindowActivityIntegration_Click(object sender, RoutedEventArgs e) => _trayIconService.ToggleIntegration(TrayIntegration.WindowActivity);

    private void CurrentTimeIntegration_Click(object sender, RoutedEventArgs e) => _trayIconService.ToggleIntegration(TrayIntegration.CurrentTime);

    private void WeatherIntegration_Click(object sender, RoutedEventArgs e) => _trayIconService.ToggleIntegration(TrayIntegration.Weather);

    private void HeartRateIntegration_Click(object sender, RoutedEventArgs e) => _trayIconService.ToggleIntegration(TrayIntegration.HeartRate);

    private void ComponentStatsIntegration_Click(object sender, RoutedEventArgs e) => _trayIconService.ToggleIntegration(TrayIntegration.ComponentStats);

    private void TrackerBatteryIntegration_Click(object sender, RoutedEventArgs e) => _trayIconService.ToggleIntegration(TrayIntegration.TrackerBattery);

    private void VrcRadarIntegration_Click(object sender, RoutedEventArgs e) => _trayIconService.ToggleIntegration(TrayIntegration.VrcRadar);

    private void NetworkIntegration_Click(object sender, RoutedEventArgs e) => _trayIconService.ToggleIntegration(TrayIntegration.NetworkStatistics);

    private void WindowsMediaPrevious_Click(object sender, RoutedEventArgs e) => _trayIconService.PreviousWindowsMedia();

    private void WindowsMediaPlayPause_Click(object sender, RoutedEventArgs e) => _trayIconService.ToggleWindowsMediaPlayback();

    private void WindowsMediaNext_Click(object sender, RoutedEventArgs e) => _trayIconService.NextWindowsMedia();

    private void SpotifyPrevious_Click(object sender, RoutedEventArgs e) => _trayIconService.PreviousSpotify();

    private void SpotifyPlayPause_Click(object sender, RoutedEventArgs e) => _trayIconService.ToggleSpotifyPlayback();

    private void SpotifyNext_Click(object sender, RoutedEventArgs e) => _trayIconService.NextSpotify();

    private void SpotifyLike_Click(object sender, RoutedEventArgs e) => _trayIconService.ToggleSpotifyLike();

    private void Exit_Click(object sender, RoutedEventArgs e) => _trayIconService.ExitApplication();

    private void WindowsMediaProgressTrack_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (WindowsMediaProgressBar.Visibility != Visibility.Visible)
            return;

        double width = WindowsMediaProgressBar.ActualWidth;
        if (width <= 0)
            return;

        Point position = e.GetPosition(WindowsMediaProgressBar);
        double progressPercent = Clamp(position.X / width * 100d, 0d, 100d);
        _trayIconService.SeekWindowsMedia(progressPercent);
        e.Handled = true;
    }

    private void Window_Deactivated(object sender, EventArgs e)
        => HideAndReset();

    public void HideAndReset()
    {
        _mediaRefreshTimer.Stop();
        CollapseDetailPanels();
        Hide();
    }

    private void SendTrayChatInput()
    {
        if (_trayIconService.TrySendTrayChat(TrayChatText.Text))
            TrayChatText.Clear();

        FocusTrayChatInput();
    }

    private void PasteIntoTrayChat()
    {
        try
        {
            string clipboard = Clipboard.GetText(TextDataFormat.UnicodeText);
            if (string.IsNullOrEmpty(clipboard))
                return;

            int selectionStart = TrayChatText.SelectionStart;
            string current = TrayChatText.Text ?? string.Empty;
            string merged = current.Remove(selectionStart, TrayChatText.SelectionLength)
                .Insert(selectionStart, clipboard);

            if (merged.Length > Core.Constants.MaxChatMessageLength)
                merged = merged[..Core.Constants.MaxChatMessageLength];

            TrayChatText.Text = merged;
            TrayChatText.CaretIndex = Math.Min(selectionStart + clipboard.Length, TrayChatText.Text.Length);
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"Tray chat paste failed: {ex.Message}");
        }

        FocusTrayChatInput();
    }

    private void FocusTrayChatInput()
    {
        if (!IsVisible)
            return;

        TrayChatText.Focus();
        Keyboard.Focus(TrayChatText);
        TrayChatText.CaretIndex = TrayChatText.Text.Length;
    }

    private void RefreshWindowsMedia()
    {
        MediaSessionInfo? session = _trayIconService.GetWindowsMediaSession();
        if (session is null)
        {
            WindowsMediaWidget.Visibility = Visibility.Collapsed;
            return;
        }

        WindowsMediaWidget.Visibility = Visibility.Visible;
        WindowsMediaTitle.Text = ResolveWindowsMediaTitle(session);
        WindowsMediaSubtitle.Text = ResolveWindowsMediaSubtitle(session);
        WindowsMediaPlayPauseText.Text = session.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing
            ? "⏸"
            : "▶";
        bool hasProgress = session.FullTime > TimeSpan.Zero && !session.IsLiveTime;
        WindowsMediaProgressBar.Visibility = hasProgress ? Visibility.Visible : Visibility.Collapsed;
        WindowsMediaProgressText.Visibility = hasProgress ? Visibility.Visible : Visibility.Collapsed;
        if (hasProgress)
        {
            WindowsMediaProgressBar.Value = session.TimePosition;
            WindowsMediaProgressText.Text = $"{FormatTime(session.CurrentTime)} / {FormatTime(session.FullTime)}";
        }
    }

    private void RefreshSpotify()
    {
        if (!_trayIconService.HasSpotifyWidget())
        {
            SpotifyWidget.Visibility = Visibility.Collapsed;
            return;
        }

        SpotifyDisplayState spotify = _trayIconService.SpotifyDisplay;
        SpotifyWidget.Visibility = Visibility.Visible;
        SpotifyTitle.Text = string.IsNullOrWhiteSpace(spotify.Title) ? "Spotify" : spotify.Title;
        SpotifySubtitle.Text = string.IsNullOrWhiteSpace(spotify.Artist)
            ? spotify.StatusText
            : spotify.Artist;
        SpotifyProgress.Text = BuildSpotifyProgressText(spotify);
        SpotifyPlayPauseText.Text = spotify.IsPlaying ? "⏸" : "▶";
        SpotifyLikeText.Text = spotify.IsLiked ? "♥" : "♡";
        SpotifyProgressBar.Visibility = spotify.DurationMs > 0 ? Visibility.Visible : Visibility.Collapsed;
        SpotifyProgressBar.Value = spotify.ProgressPercent;
    }

    private void RefreshIntegrationPanel()
    {
        IntegrationPanelSwitch.IsChecked = _isIntegrationPanelOpen;
        IntegrationPanel.Visibility = _isIntegrationPanelOpen ? Visibility.Visible : Visibility.Collapsed;
        SetArrowState(IntegrationPanelArrow, _isIntegrationPanelOpen);
        ApplyDetailPanelLayout();
    }

    private void RefreshIntegrationToggles()
    {
        SetIntegrationState(StatusIntegrationStateText, TrayIntegration.Status);
        SetIntegrationState(MediaLinkIntegrationStateText, TrayIntegration.MediaLink);
        SetIntegrationState(SpotifyIntegrationStateText, TrayIntegration.Spotify);
        SetIntegrationState(TwitchIntegrationStateText, TrayIntegration.Twitch);
        SetIntegrationState(TikTokIntegrationStateText, TrayIntegration.TikTok);
        SetIntegrationState(DiscordIntegrationStateText, TrayIntegration.Discord);
        SetIntegrationState(SoundpadIntegrationStateText, TrayIntegration.Soundpad);
        SetIntegrationState(WindowActivityIntegrationStateText, TrayIntegration.WindowActivity);
        SetIntegrationState(CurrentTimeIntegrationStateText, TrayIntegration.CurrentTime);
        SetIntegrationState(WeatherIntegrationStateText, TrayIntegration.Weather);
        SetIntegrationState(HeartRateIntegrationStateText, TrayIntegration.HeartRate);
        SetIntegrationState(ComponentStatsIntegrationStateText, TrayIntegration.ComponentStats);
        SetIntegrationState(TrackerBatteryIntegrationStateText, TrayIntegration.TrackerBattery);
        SetIntegrationState(VrcRadarIntegrationStateText, TrayIntegration.VrcRadar);
        SetIntegrationState(NetworkIntegrationStateText, TrayIntegration.NetworkStatistics);
    }

    private void RefreshStatusPanel()
    {
        StatusPanelSwitch.IsChecked = _isStatusPanelOpen;
        StatusPanel.Visibility = _isStatusPanelOpen ? Visibility.Visible : Visibility.Collapsed;
        SetArrowState(StatusPanelArrow, _isStatusPanelOpen);
        ApplyDetailPanelLayout();
    }

    private void RefreshStatusControls()
    {
        StatusCycleStateText.Text = _trayIconService.IsCycleStatusEnabled ? "On" : "Off";
        StatusCycleStateText.Foreground = _trayIconService.IsCycleStatusEnabled
            ? (Brush)FindResource("AccentTealBrush")
            : (Brush)FindResource("TextMutedBrush");

        StatusCycleOverrideStateText.Text = _trayIconService.IsCycleOverrideCurrentGroupEnabled ? "Selected group" : "All enabled";
        StatusCycleOverrideStateText.Foreground = _trayIconService.IsCycleOverrideCurrentGroupEnabled
            ? (Brush)FindResource("AccentTealBrush")
            : (Brush)FindResource("TextMutedBrush");

        RebuildStatusGroupButtons();
    }

    private void RebuildStatusGroupButtons()
    {
        StatusGroupsPanel.Children.Clear();
        string selectedGroupId = _trayIconService.SelectedStatusGroupId;
        AddStatusGroupButton("All groups", string.Empty, string.IsNullOrEmpty(selectedGroupId));

        foreach (StatusGroup group in _trayIconService.GetStatusGroups().OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase))
            AddStatusGroupButton(group.Name, group.GroupId, group.GroupId == selectedGroupId);
    }

    private void AddStatusGroupButton(string label, string groupId, bool isSelected)
    {
        var button = new Button
        {
            Style = (Style)FindResource("TrayMenuButtonStyle"),
            Background = isSelected ? SelectedStatusGroupBackground : StatusGroupBackground,
            BorderBrush = isSelected ? SelectedStatusGroupBorder : StatusGroupBorder,
            Tag = groupId,
            Content = new DockPanel
            {
                LastChildFill = true,
                Children =
                {
                    new TextBlock { Text = label, Foreground = (Brush)FindResource("TextPrimaryBrush") },
                    new TextBlock
                    {
                        Text = isSelected ? "Selected" : string.Empty,
                        Foreground = (Brush)FindResource(isSelected ? "AccentTealBrush" : "TextMutedBrush"),
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Margin = new Thickness(8, 0, 0, 0)
                    }
                }
            }
        };
        button.Click += StatusGroupButton_Click;
        StatusGroupsPanel.Children.Add(button);
    }

    private static Brush CreateBrush(string color)
    {
        Brush brush = (Brush)new BrushConverter().ConvertFromString(color)!;
        brush.Freeze();
        return brush;
    }

    private void SetIntegrationState(System.Windows.Controls.TextBlock target, TrayIntegration integration)
    {
        TrayIntegrationRouteState state = _trayIconService.GetIntegrationRouteState(integration);
        SetIntegrationRouteLabel(target, state);

        target.Inlines.Clear();
        target.Inlines.Add(new Run(state.Enabled ? "On" : "Off")
        {
            Foreground = state.ActiveForCurrentMode
                ? (Brush)FindResource("AccentTealBrush")
                : (Brush)FindResource(state.Enabled ? "TextSectionHeaderBrush" : "TextMutedBrush")
        });

        target.MinWidth = 28;
        target.TextAlignment = TextAlignment.Right;
    }

    private void SetIntegrationRouteLabel(System.Windows.Controls.TextBlock stateText, TrayIntegrationRouteState state)
    {
        if (stateText.Parent is not DockPanel row)
            return;

        row.LastChildFill = false;
        DockPanel.SetDock(stateText, Dock.Right);

        TextBlock? label = row.Children
            .OfType<TextBlock>()
            .FirstOrDefault(textBlock =>
                textBlock != stateText &&
                (textBlock.Tag is string || (!string.IsNullOrWhiteSpace(textBlock.Text) && textBlock.Text.Length > 2)));

        if (label is null)
            return;

        string baseLabel = label.Tag as string ?? label.Text;
        label.Tag ??= baseLabel;
        label.Inlines.Clear();
        label.Inlines.Add(new Run(baseLabel));
        label.Inlines.Add(new Run("  "));
        label.Inlines.Add(new Run("VR")
        {
            Foreground = state.VrEnabled
                ? (Brush)FindResource("TextSectionHeaderBrush")
                : (Brush)FindResource("TextMutedBrush")
        });
        label.Inlines.Add(new Run(" / ")
        {
            Foreground = (Brush)FindResource("TextMutedBrush")
        });
        label.Inlines.Add(new Run("Desktop")
        {
            Foreground = state.DesktopEnabled
                ? (Brush)FindResource("TextSectionHeaderBrush")
                : (Brush)FindResource("TextMutedBrush")
        });
    }

    private void CollapseDetailPanels(bool clearKeyboardFocus = false)
    {
        if (clearKeyboardFocus && Keyboard.FocusedElement == TrayChatText)
            Keyboard.ClearFocus();

        if (!_isIntegrationPanelOpen && !_isStatusPanelOpen)
            return;

        _isIntegrationPanelOpen = false;
        _isStatusPanelOpen = false;
        RefreshIntegrationPanel();
        RefreshStatusPanel();
    }

    private void ApplyDetailPanelLayout()
    {
        bool compact = _isIntegrationPanelOpen || _isStatusPanelOpen;
        Visibility compactVisibility = compact ? Visibility.Collapsed : Visibility.Visible;

        WindowsMediaControls.Visibility = compactVisibility;
        SpotifyControls.Visibility = compactVisibility;
        TrayChatHelp.Visibility = compactVisibility;
        TrayChatActions.Visibility = compactVisibility;
        ChattingCard.Padding = compact ? new Thickness(10, 8, 10, 8) : new Thickness(10);
    }

    private static void SetArrowState(TextBlock arrow, bool isOpen)
    {
        if (arrow.RenderTransform is RotateTransform transform)
            transform.Angle = isOpen ? 90 : 0;
    }

    private static string ResolveWindowsMediaTitle(MediaSessionInfo session)
    {
        if (!string.IsNullOrWhiteSpace(session.Title) && session.Title != "Title")
            return session.Title;

        return string.IsNullOrWhiteSpace(session.FriendlyAppName)
            ? "Windows media"
            : session.FriendlyAppName;
    }

    private static string ResolveWindowsMediaSubtitle(MediaSessionInfo session)
    {
        string artist = !string.IsNullOrWhiteSpace(session.Artist) && session.Artist != "Artist"
            ? session.Artist
            : string.Empty;

        if (!string.IsNullOrWhiteSpace(artist) && !string.IsNullOrWhiteSpace(session.FriendlyAppName))
            return $"{artist} • {session.FriendlyAppName}";

        if (!string.IsNullOrWhiteSpace(artist))
            return artist;

        return session.PlaybackStatus.ToString();
    }

    private static string BuildSpotifyProgressText(SpotifyDisplayState spotify)
    {
        string status = spotify.IsPlaying ? "Playing" : "Paused";
        string progress = spotify.ProgressDisplay;
        string device = spotify.DeviceName;

        string text = string.IsNullOrWhiteSpace(progress)
            ? status
            : string.Create(CultureInfo.InvariantCulture, $"{status} • {progress}");

        return string.IsNullOrWhiteSpace(device)
            ? text
            : $"{text} • {device}";
    }

    private static string FormatTime(TimeSpan time)
        => time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes}:{time.Seconds:00}";

    private static double Clamp(double value, double min, double max)
    {
        if (max < min)
            return min;

        return Math.Min(Math.Max(value, min), max);
    }

    private static Point GetCursorPositionInDips()
    {
        System.Drawing.Point cursor = Forms.Cursor.Position;
        Matrix transform = GetTransformFromDevice();
        return transform.Transform(new Point(cursor.X, cursor.Y));
    }

    private static Rect GetCurrentScreenWorkAreaInDips()
    {
        Forms.Screen screen = Forms.Screen.FromPoint(Forms.Cursor.Position);
        System.Drawing.Rectangle bounds = screen.WorkingArea;
        Matrix transform = GetTransformFromDevice();
        Point topLeft = transform.Transform(new Point(bounds.Left, bounds.Top));
        Point bottomRight = transform.Transform(new Point(bounds.Right, bounds.Bottom));
        return new Rect(topLeft, bottomRight);
    }

    private static Matrix GetTransformFromDevice()
    {
        PresentationSource? source = App.mainWindow is null
            ? null
            : PresentationSource.FromVisual(App.mainWindow);

        return source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
    }
}
