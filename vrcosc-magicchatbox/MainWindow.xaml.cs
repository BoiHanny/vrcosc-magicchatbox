using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.UI.Dialogs;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.Models;

namespace vrcosc_magicchatbox
{
    /// <summary>
    /// Main application window. Owns the scan loop, module host, and persistence coordinator lifecycle.
    /// </summary>
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
        private ViewModel VM => (ViewModel)DataContext;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            IntPtr handle = (new System.Windows.Interop.WindowInteropHelper(this)).Handle;
            System.Windows.Interop.HwndSource.FromHwnd(handle)?.AddHook(WindowProc);

            this.StateChanged += MainWindow_StateChanged;
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




        public MainWindow(ScanLoopService scanLoop, ModuleBootstrapper bootstrapper, IModuleHost moduleHost, IStatePersistenceCoordinator persistence)
        {
            InitializeComponent();

            _scanLoop = scanLoop;
            _bootstrapper = bootstrapper;
            _moduleHost = moduleHost;
            _persistence = persistence;

            Closing += MainWindow_ClosingAsync;
        }

        public void ApplyIntegrationOrder()
        {
            integrationsPage?.ApplyIntegrationOrder();
        }

        private void ReorderIntegrations_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new UI.Dialogs.ReorderIntegrations(VM.Integrations.IntegrationDisplay, VM.Integrations.IntegrationSettingsProvider);
            DialogWindowHelper.PrepareModal(dialog, this);
            dialog.ShowDialog();
        }

        private void WhisperModule_SentChat()
        {
            Dispatcher.Invoke(() => VM.Chatting.OnWhisperSentChat());
        }

        private void WhisperModule_TranscriptionReceived(string newTranscription)
        {
            VM.Chatting.OnTranscriptionReceived(newTranscription);
        }


        public async Task InitializeAsync()
        {
            _bootstrapper.CreateLateModules();
            _moduleHost.Whisper.TranscriptionReceived += WhisperModule_TranscriptionReceived;
            _moduleHost.Whisper.SentChatMessage += WhisperModule_SentChat;

            VM.SelectedMenuIndex = VM.AppSettingsInstance.CurrentMenuItem;
        }

        /// <summary>
        /// Called after the window is shown and first frame rendered — starts background scan loop.
        /// </summary>
        public void StartBackgroundProcessing()
        {
            _scanLoop.Start();
            _ = Task.Run(() => _scanLoop.Scantick(true));
        }

        public static event EventHandler ShadowOpacityChanged;

        private void Button_close_Click(object sender, RoutedEventArgs e)
        {
            this.Visibility = Visibility.Hidden;
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


        private async void MainWindow_ClosingAsync(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Cancel the window closing event temporarily to await the async task
            e.Cancel = true;

            try
            {
                Hide();
                await SaveDataToDiskAsync();
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: true, exitapp: true);
            }
            finally
            {
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


        private void TikTokTTSVoices_combo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox)
                VM.Options.TtsSection.OnTtsVoiceSelected(comboBox.SelectedItem as Voice);
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

        /// <summary>
        /// Updates the startup overlay progress display (call from any thread).
        /// </summary>
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

            // Animate progress bar smoothly
            var anim = new DoubleAnimation(progressPercent, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            OverlayProgressBar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, anim);
        }

        /// <summary>
        /// Fades out and collapses the startup overlay.
        /// </summary>
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
