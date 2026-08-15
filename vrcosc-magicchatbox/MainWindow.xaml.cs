using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using System.Windows.Threading;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.UI.Dialogs;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.Models;

namespace vrcosc_magicchatbox
{
    public partial class MainWindow : Window
    {
        private const int WM_ENTERSIZEMOVE = 0x0231;
        private const int WM_EXITSIZEMOVE = 0x0232;
        private ResizeMode previousResizeMode = ResizeMode.CanResize;
        private static double _shadowOpacity;
        public static readonly DependencyProperty ShadowOpacityProperty = DependencyProperty.Register(
            "ShadowOpacity",
            typeof(double),
            typeof(MainWindow),
            new PropertyMetadata(0.0));

        private readonly ScanLoopService _scanLoop;
        private readonly IStatePersistenceCoordinator _persistence;
        private readonly ModuleBootstrapper _bootstrapper;
        private readonly IModuleHost _moduleHost;
        private readonly ITrayIconService _trayIconService;
        private readonly HotkeyManagement _hotkeyManagement;
        private HwndSource? _windowSource;
        private bool _shutdownRequested;
        public bool _isTrayClosing;
        private readonly ISettingsProvider<AppSettings> _appSettingsProvider;
        public ViewModel VM => (ViewModel)DataContext;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            if (_windowSource is not null)
                return;

            IntPtr handle = (new WindowInteropHelper(this)).Handle;
            _windowSource = HwndSource.FromHwnd(handle);
            _windowSource?.AddHook(WindowProc);

            this.StateChanged += MainWindow_StateChanged;
        }

        private void RestoreWindowPlacement()
        {
            try
            {
                var settings = _appSettingsProvider?.Value;
                if (settings == null) return;

                var virtualScreen = new Rect(
                    SystemParameters.VirtualScreenLeft,
                    SystemParameters.VirtualScreenTop,
                    SystemParameters.VirtualScreenWidth,
                    SystemParameters.VirtualScreenHeight);

                var placement = WindowPlacementPolicy.Resolve(
                    settings.WindowLeft,
                    settings.WindowTop,
                    settings.WindowWidth,
                    settings.WindowHeight,
                    virtualScreen,
                    new Size(MinWidth, MinHeight));

                if (placement is { } rect)
                {
                    WindowStartupLocation = WindowStartupLocation.Manual;
                    Left = rect.Left;
                    Top = rect.Top;
                    Width = rect.Width;
                    Height = rect.Height;
                }

                if (settings.WindowMaximized)
                    WindowState = WindowState.Maximized;
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"Could not restore window placement: {ex.Message}");
            }
        }

        private void SaveWindowPlacement()
        {
            try
            {
                var settings = _appSettingsProvider?.Value;
                if (settings == null) return;

                if (WindowState == WindowState.Minimized)
                    return;

                var bounds = WindowState == WindowState.Maximized
                    ? RestoreBounds
                    : new Rect(Left, Top, Width, Height);

                if (bounds.IsEmpty || double.IsNaN(bounds.Left) || double.IsNaN(bounds.Top))
                    return;

                settings.WindowLeft = bounds.Left;
                settings.WindowTop = bounds.Top;
                settings.WindowWidth = bounds.Width;
                settings.WindowHeight = bounds.Height;
                settings.WindowMaximized = WindowState == WindowState.Maximized;
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"Could not save window placement: {ex.Message}");
            }
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowChrome.GetWindowChrome(this).GlassFrameThickness = new Thickness(0);
                this.BorderThickness = new Thickness(8);
            }
            else
            {
                WindowChrome.GetWindowChrome(this).GlassFrameThickness = new Thickness(1);
                this.BorderThickness = new Thickness(0);

                if (WindowState == WindowState.Minimized && VM.AppSettingsInstance.MinimizeToTrayOnMinimize)
                    HideToTray();
            }
        }

        private IntPtr WindowProc(IntPtr hwnd, int uMsg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (uMsg)
            {
                case WM_ENTERSIZEMOVE:
                    if (ResizeMode == ResizeMode.CanResize || ResizeMode == ResizeMode.CanResizeWithGrip)
                    {
                        previousResizeMode = ResizeMode;
                        ResizeMode = ResizeMode.NoResize;
                        OnStartResize();
                    }
                    break;

                case WM_EXITSIZEMOVE:
                    if (ResizeMode == ResizeMode.NoResize)
                    {
                        ResizeMode = previousResizeMode;
                        OnEndResize();
                    }
                    break;
            }

            return IntPtr.Zero;
        }

        private void OnStartResize()
        {
            WindowChrome windowChrome = WindowChrome.GetWindowChrome(this);
            windowChrome.GlassFrameThickness = new Thickness(0);
        }

        private void OnEndResize()
        {
            WindowChrome windowChrome = WindowChrome.GetWindowChrome(this);
            windowChrome.GlassFrameThickness = new Thickness(1);
        }

        public MainWindow(
            ScanLoopService scanLoop,
            ModuleBootstrapper bootstrapper,
            IModuleHost moduleHost,
            IStatePersistenceCoordinator persistence,
            ITrayIconService trayIconService,
            HotkeyManagement hotkeyManagement,
            ISettingsProvider<AppSettings> appSettingsProvider)
        {
            InitializeComponent();

            _scanLoop = scanLoop;
            _bootstrapper = bootstrapper;
            _moduleHost = moduleHost;
            _persistence = persistence;
            _trayIconService = trayIconService;
            _hotkeyManagement = hotkeyManagement;
            _appSettingsProvider = appSettingsProvider;

            RestoreWindowPlacement();

            Closing += MainWindow_ClosingAsync;
            PreviewMouseDown += MainWindow_PreviewMouseDown;
            ContentRendered += OnFirstContentRendered;
        }

        public void ApplyIntegrationOrder()
        {
            integrationsPage?.ApplyIntegrationOrder();
        }

        private void ReorderIntegrations_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new UI.Dialogs.ReorderIntegrations(VM.Integrations.IntegrationDisplay, VM.Integrations.IntegrationSettingsProvider);
                DialogWindowHelper.PrepareModal(dialog, this);
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        private void MainWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not ViewModel viewModel)
                return;

            if (e.ChangedButton == MouseButton.XButton1)
            {
                viewModel.NavigateBackCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.ChangedButton == MouseButton.XButton2)
            {
                viewModel.NavigateForwardCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void WhisperModule_SentChat()
        {
            Dispatcher.Invoke(() => VM.Chatting.OnWhisperSentChat());
        }

        private void WhisperModule_TranscriptionReceived(string newTranscription)
        {
            Dispatcher.BeginInvoke(() => VM.Chatting.OnTranscriptionReceived(newTranscription));
        }

        public async Task InitializeAsync()
        {
            _bootstrapper.CreateLateModules();
            _moduleHost.Whisper.TranscriptionReceived += WhisperModule_TranscriptionReceived;
            _moduleHost.Whisper.SentChatMessage += WhisperModule_SentChat;

            VM.SelectedMenuIndex = VM.AppSettingsInstance.CurrentMenuItem;
        }

        public void StartBackgroundProcessing()
        {
            _scanLoop.Start();
            _ = _scanLoop.Scantick(true);
        }

        public static event EventHandler ShadowOpacityChanged;

        private void Button_close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Button_minimize_Click(object sender, RoutedEventArgs e)
        { this.WindowState = WindowState.Minimized; }

        private void Drag_area_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void MasterSwitch_Click(object sender, RoutedEventArgs e)
        {
            VM.HandleMasterSwitchToggled();
        }

        private void HideToTray(string? notificationText = "Still running in the tray.")
        {
            Hide();

            if (VM.AppSettingsInstance.EnableTrayNotifications &&
                VM.AppSettingsInstance.ShowTrayRunningReminder &&
                !string.IsNullOrWhiteSpace(notificationText))
            {
                var openTrayAction = new ToastAction("Open Magic Tray", () =>
                {
                    _trayIconService.OpenContextMenu();
                    return Task.CompletedTask;
                });

                _trayIconService.Notify(WithTrayShortcutHint(notificationText), openTrayAction, showMainWindowOnClick: false);
            }
        }

        private string WithTrayShortcutHint(string notificationText)
        {
            if (VM.AppSettingsInstance.OpenTrayWithAltX && !string.IsNullOrWhiteSpace(_hotkeyManagement.TrayShortcutDisplayText))
                return $"{notificationText}{Environment.NewLine}Open Magic Tray with {_hotkeyManagement.TrayShortcutDisplayText}.";

            return notificationText;
        }

        private async void MainWindow_ClosingAsync(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveWindowPlacement();

            if (VM.AppSettingsInstance.CloseToTray && !_isTrayClosing)
            {
                e.Cancel = true;
                HideToTray("Still running in the tray.");
                return;
            }

            if (_shutdownRequested)
                return;

            _shutdownRequested = true;

            e.Cancel = true;

            try
            {
                _scanLoop.Stop();
                Hide();
                await SaveDataToDiskAsync();
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: true);
            }
            finally
            {
                if (_moduleHost.Whisper != null)
                {
                    _moduleHost.Whisper.TranscriptionReceived -= WhisperModule_TranscriptionReceived;
                    _moduleHost.Whisper.SentChatMessage -= WhisperModule_SentChat;
                }

                Application.Current.Shutdown();
            }
        }

        public async Task SaveDataToDiskAsync()
        {
            await _persistence.PrepareForShutdownAsync();
        }

        public void FireExitSave()
        {
            _persistence.PersistAllState();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_windowSource is not null)
            {
                _windowSource.RemoveHook(WindowProc);
                _windowSource = null;
            }

            base.OnClosed(e);
        }

        private void TikTokTTSVoices_combo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is Voice voice)
                VM.Options.TtsSection.OnTtsVoiceSelected(voice);
        }

        private void SelectTTS()
        {
            foreach (var voice in TikTokTTSVoices_combo.Items)
            {
                if (voice is Voice v && v.ApiName == VM.TtsAudio.SelectedTikTokTTSVoice?.ApiName)
                {
                    TikTokTTSVoices_combo.SelectedItem = voice;
                    break;
                }
            }
        }

        public static double ShadowOpacity
        {
            get => _shadowOpacity;
            set
            {
                if (_shadowOpacity != value)
                {
                    _shadowOpacity = value;
                    ShadowOpacityChanged?.Invoke(null, EventArgs.Empty);
                }
            }
        }

        #region Startup Overlay

        private string _lastOverlayStep = "";

        public void UpdateOverlayProgress(string currentStep, double progressPercent, string nextHint = "")
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(() => UpdateOverlayProgress(currentStep, progressPercent, nextHint));
                return;
            }

            OverlayPrevStep.Text = _lastOverlayStep;
            OverlayCurrentStep.Text = currentStep;
            OverlayNextStep.Text = nextHint;
            _lastOverlayStep = currentStep;

            var anim = new DoubleAnimation(progressPercent, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            OverlayProgressBar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, anim);
        }

        private double? _revealLeft;
        private double? _revealTop;
        private bool _parkedOffScreen;

        public void PrepareHiddenStart()
        {
            if (WindowState != WindowState.Maximized)
            {
                _revealLeft = double.IsNaN(Left) ? null : Left;
                _revealTop = double.IsNaN(Top) ? null : Top;
                _parkedOffScreen = true;

                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = SystemParameters.VirtualScreenLeft - Width - 400;
                Top = SystemParameters.VirtualScreenTop - Height - 400;
                return;
            }

            Opacity = 0;
        }

        private bool _hasRendered;
        private bool _revealWanted;
        private DispatcherTimer? _revealSafetyNet;
        private Action? _onVisible;

        private void OnFirstContentRendered(object? sender, EventArgs e)
        {
            ContentRendered -= OnFirstContentRendered;
            _hasRendered = true;

            if (_revealWanted)
                Reveal();
        }

        public void FadeInAfterStartup(Action? onVisible = null)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(() => FadeInAfterStartup(onVisible));
                return;
            }

            _onVisible = onVisible;

            if (_hasRendered)
            {
                Reveal();
                return;
            }

            _revealWanted = true;

            _revealSafetyNet?.Stop();
            _revealSafetyNet = new DispatcherTimer(
                TimeSpan.FromSeconds(5),
                DispatcherPriority.Normal,
                (_, _) =>
                {
                    Logging.WriteInfo("[Startup] First frame never arrived; revealing the window anyway.");
                    Reveal();
                },
                Dispatcher);
        }

        public void AbandonHiddenStart()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(AbandonHiddenStart);
                return;
            }

            _revealWanted = false;
            _onVisible = null;
            _revealSafetyNet?.Stop();
            _revealSafetyNet = null;

            UnparkOffScreen();

            BeginAnimation(OpacityProperty, null);
            ClearValue(OpacityProperty);
        }

        private void UnparkOffScreen()
        {
            if (!_parkedOffScreen)
                return;

            _parkedOffScreen = false;

            if (_revealLeft is { } left && _revealTop is { } top)
            {
                Left = left;
                Top = top;
                return;
            }

            var area = SystemParameters.WorkArea;
            Left = area.Left + ((area.Width - Width) / 2);
            Top = area.Top + ((area.Height - Height) / 2);
        }

        private void Reveal()
        {
            _revealWanted = false;
            _revealSafetyNet?.Stop();
            _revealSafetyNet = null;

            if (_parkedOffScreen)
            {
                Opacity = 0;
                UnparkOffScreen();
            }

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            fadeIn.Completed += (_, _) =>
            {
                BeginAnimation(OpacityProperty, null);
                ClearValue(OpacityProperty);

                Action? handover = _onVisible;
                _onVisible = null;
                handover?.Invoke();
            };

            BeginAnimation(OpacityProperty, fadeIn);
        }

        public void HideStartupOverlay()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(() => HideStartupOverlay());
                return;
            }

            UpdateOverlayProgress("Restoring open page...", 100);

            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(400))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            fadeOut.Completed += (_, _) =>
            {
                StartupOverlay.Visibility = Visibility.Collapsed;
                StartupOverlay.IsHitTestVisible = false;
            };
            StartupOverlay.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        #endregion
    }
}
