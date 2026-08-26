using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
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
using Forms = System.Windows.Forms;

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
                    rect = FollowTheMonitorItWasOpenedOn(rect);

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

        /// <summary>
        /// Reopening from the taskbar on a different monitor should put the window where you are
        /// looking, not where you left it three monitors away. Launching on the same monitor keeps
        /// the exact saved position, so nothing moves for the common single-screen case.
        /// </summary>
        private Rect FollowTheMonitorItWasOpenedOn(Rect saved)
        {
            try
            {
                double scale = DeviceScale();

                var savedPixels = new Rect(
                    saved.Left * scale,
                    saved.Top * scale,
                    saved.Width * scale,
                    saved.Height * scale);

                Forms.Screen launchScreen = Forms.Screen.FromPoint(Forms.Cursor.Position);
                Rect launchArea = ToRect(launchScreen.WorkingArea);

                if (WindowPlacementPolicy.BelongsTo(savedPixels, launchArea))
                    return saved;

                Rect savedArea = ToRect(Forms.Screen.FromRectangle(ToRectangle(savedPixels)).WorkingArea);
                Rect movedPixels = WindowPlacementPolicy.MoveToWorkArea(savedPixels, savedArea, launchArea);

                Logging.WriteInfo(
                    $"Window restored onto the monitor it was opened from rather than its saved position.");

                return new Rect(
                    movedPixels.Left / scale,
                    movedPixels.Top / scale,
                    movedPixels.Width / scale,
                    movedPixels.Height / scale);
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"Could not resolve the launch monitor, keeping the saved position: {ex.Message}");
                return saved;
            }
        }

        private static double DeviceScale()
        {
            Forms.Screen? primary = Forms.Screen.PrimaryScreen;
            if (primary is null || SystemParameters.PrimaryScreenWidth <= 0)
                return 1;

            double scale = primary.Bounds.Width / SystemParameters.PrimaryScreenWidth;
            return scale > 0 ? scale : 1;
        }

        private static Rect ToRect(System.Drawing.Rectangle rectangle)
            => new(rectangle.Left, rectangle.Top, rectangle.Width, rectangle.Height);

        private static System.Drawing.Rectangle ToRectangle(Rect rect)
            => new((int)rect.Left, (int)rect.Top, (int)Math.Max(1, rect.Width), (int)Math.Max(1, rect.Height));

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

        private void MainWindow_StateChanged(object? sender, EventArgs e)
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

            UpdateUiObservable();
        }

        private void MainWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateUiObservable();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateUiObservable();
        }

        private void UpdateUiObservable()
        {
            if (DataContext is ViewModel viewModel)
                viewModel.IsWindowOnScreen = IsVisible && WindowState != WindowState.Minimized;
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
            Loaded += MainWindow_Loaded;
            IsVisibleChanged += MainWindow_IsVisibleChanged;

            if (Core.Diagnostics.PerfProbe.IsEnabled)
                PreviewKeyDown += OnPerfDumpRequested;
        }

        public void ApplyIntegrationOrder()
        {
            FindDescendant<UI.Pages.IntegrationsPage>(integrationsHost)?.ApplyIntegrationOrder();
        }

        private static T? FindDescendant<T>(DependencyObject? root) where T : DependencyObject
        {
            if (root == null) return null;

            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T match) return match;

                var found = FindDescendant<T>(child);
                if (found != null) return found;
            }

            return null;
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

        public static event EventHandler? ShadowOpacityChanged;

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
            UpdateUiObservable();

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

        private async void MainWindow_ClosingAsync(object? sender, System.ComponentModel.CancelEventArgs e)
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

            Core.Diagnostics.UiPerfMonitor.Start(this);
            Core.Diagnostics.PerfProbe.Mark("MainWindow first content rendered");
            StartPerfAutoDump();
            ApplyReducedVisuals();

            if (_revealWanted)
                Reveal();
        }

        /// <summary>Ctrl+Shift+F12 dumps a snapshot, Ctrl+Shift+F11 runs the navigation benchmark. --perf only.</summary>
        private async void OnPerfDumpRequested(object sender, KeyEventArgs e)
        {
            if (!Core.Diagnostics.PerfProbe.IsEnabled
                || Keyboard.Modifiers != (ModifierKeys.Control | ModifierKeys.Shift))
            {
                return;
            }

            if (e.Key == Key.F12)
            {
                e.Handled = true;
                DumpPerfSnapshot("hotkey");
                return;
            }

            if (e.Key != Key.F11 || DataContext is not ViewModel viewModel)
                return;

            e.Handled = true;
            Logging.WriteInfo("[Perf] Navigation benchmark started; the window will cycle pages.");

            string report = await Core.Diagnostics.NavigationBenchmark.RunAsync(
                this,
                index => viewModel.SelectedMenuIndex = index,
                () => viewModel.SelectedMenuIndex);

            Logging.WriteInfo(report);
            DumpPerfSnapshot("nav-benchmark");
        }

        private void ApplyReducedVisuals()
        {
            var settings = _appSettingsProvider?.Value;
            if (settings == null)
                return;

            bool forced = Environment.GetEnvironmentVariable("MAGICCHATBOX_REDUCED_VISUALS") == "1";
            var appState = App.Services.GetRequiredService<IAppState>();

            void Resolve() => UI.Controls.ReducedVisuals.IsEnabled =
                forced
                || settings.ReducedVisuals
                || (settings.ReducedVisualsInVr && appState.IsVRRunning);

            settings.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(AppSettings.ReducedVisuals) or nameof(AppSettings.ReducedVisualsInVr))
                    Resolve();
            };

            if (appState is INotifyPropertyChanged observableState)
            {
                observableState.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(IAppState.IsVRRunning))
                        Dispatcher.BeginInvoke(Resolve);
                };
            }

            UI.Controls.ReducedVisuals.Changed += () => Dispatcher.BeginInvoke(() => UI.Controls.ReducedVisuals.Refresh(this));
            Resolve();
            UI.Controls.ReducedVisuals.Refresh(this);
        }

        private void StartPerfAutoDump()
        {
            if (!Core.Diagnostics.PerfProbe.IsEnabled)
                return;

            // Periodic dumps make a soak run self-recording; without them a snapshot needs someone at the keyboard.
            var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle, Dispatcher)
            {
                Interval = TimeSpan.FromSeconds(60),
            };
            timer.Tick += (_, _) => DumpPerfSnapshot("auto");
            timer.Start();

            Closed += (_, _) =>
            {
                timer.Stop();
                DumpPerfSnapshot("shutdown");
            };

            if (Environment.GetEnvironmentVariable("MAGICCHATBOX_PERF_NAVBENCH") == "1")
                RunNavigationBenchmarkAsync();

            if (Environment.GetEnvironmentVariable("MAGICCHATBOX_PERF_SCENARIO") == "1")
                RunUiScenarioAsync();
        }

        private async void RunUiScenarioAsync()
        {
            if (DataContext is not ViewModel viewModel)
                return;

            await Task.Delay(TimeSpan.FromSeconds(10));

            var integrations = App.Services
                .GetRequiredService<Core.Configuration.ISettingsProvider<IntegrationSettings>>().Value;

            var runner = new Core.Diagnostics.UiScenarioRunner(
                this,
                integrations,
                index => viewModel.SelectedMenuIndex = index);

            Logging.WriteInfo(await runner.RunAsync());
            DumpPerfSnapshot("ui-scenario");
        }

        private async void RunNavigationBenchmarkAsync()
        {
            if (DataContext is not ViewModel viewModel)
                return;

            // Let the first page settle so the benchmark measures switches and not the tail of startup.
            await Task.Delay(TimeSpan.FromSeconds(10));

            string report = await Core.Diagnostics.NavigationBenchmark.RunAsync(
                this,
                index => viewModel.SelectedMenuIndex = index,
                () => viewModel.SelectedMenuIndex);

            report += await SweepOptionsScrollAsync(viewModel);

            Logging.WriteInfo(report);
            DumpPerfSnapshot("nav-benchmark");
        }

        private async Task<string> SweepOptionsScrollAsync(ViewModel viewModel)
        {
            viewModel.SelectedMenuIndex = 3;
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            await Task.Delay(500);

            var page = FindDescendant<UI.Pages.OptionsPage>(optionsHost);
            ScrollViewer? scroll = page == null ? null : FindDescendant<ScrollViewer>(page);

            return scroll == null
                ? "[Perf] Scroll sweep skipped: Options scroll viewer not found.\n"
                : await Core.Diagnostics.NavigationBenchmark.SweepScrollAsync(scroll, "options");
        }

        internal void DumpPerfSnapshot(string reason)
        {
            if (!Core.Diagnostics.PerfProbe.IsEnabled)
                return;

            Core.Diagnostics.VisualTreeCensus.Result census = Core.Diagnostics.VisualTreeCensus.Take(this);
            Logging.WriteInfo(census.Describe("MainWindow"));
            Logging.WriteInfo(Core.Diagnostics.BindingErrorProbe.Describe());
            Logging.WriteInfo(
                $"[Perf] Layout updates {Core.Diagnostics.UiPerfMonitor.LayoutUpdateCount}, "
                + $"frames {Core.Diagnostics.UiPerfMonitor.FrameCount}");

            foreach ((string name, Core.Diagnostics.PerfProbe.SampleSet.Snapshot sample)
                in Core.Diagnostics.PerfProbe.Snapshot())
            {
                Logging.WriteInfo(
                    $"[Perf] {name}: n={sample.Count} mean={sample.MeanMs:F2}ms "
                    + $"p95={sample.P95Ms:F2}ms max={sample.MaxMs:F2}ms alloc={sample.AllocatedBytes / 1024}KB");
            }

            string? path = Core.Diagnostics.PerfProbe.WriteReport(reason);
            if (path != null)
                Logging.WriteInfo($"[Perf] Snapshot written to {path}");
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

        public void HideStartupOverlay(bool animate = true)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(() => HideStartupOverlay(animate));
                return;
            }

            UpdateOverlayProgress("Restoring open page...", 100);

            if (!animate)
            {
                StartupOverlay.BeginAnimation(UIElement.OpacityProperty, null);
                StartupOverlay.Opacity = 0;
                StartupOverlay.Visibility = Visibility.Collapsed;
                StartupOverlay.IsHitTestVisible = false;
                return;
            }

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
