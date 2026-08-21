using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace vrcosc_magicchatbox
{
    public partial class StartUp : Window
    {
        private readonly Stopwatch _globalTimer = Stopwatch.StartNew();
        private readonly Stopwatch _stepTimer = new();
        private readonly Action? _cancelRequested;
        private string _prevMessage = "";
        private string _prevTime = "";
        private string _currentMessage = "";
        private string _nextMessage = "";
        private double _currentProgress;
        private int _cancelStarted;

        private const double BylineStartBlur = 5.0;
        private const double BylineMaxOpacity = 1.0;
        private const double BylineFullyRevealedAt = 30.0;

        private const double DriftShare = 0.28;
        private const double DriftSeconds = 6.0;
        private const double SettleMs = 200;

        private static readonly TimeSpan SplashCreationTimeout = TimeSpan.FromSeconds(15);

        private static readonly System.Windows.Media.SolidColorBrush VerifiedBrush = CreateFrozenBrush(0x6F, 0xD8, 0x9B);
        private static readonly System.Windows.Media.SolidColorBrush CautionBrush = CreateFrozenBrush(0xFF, 0xC1, 0x07);

        private static System.Windows.Media.SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
        {
            var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        private const int DwmWindowCornerPreference = 33;
        private const int DwmCornerRound = 2;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        private readonly BlurEffect _bylineBlur = new()
        {
            Radius = BylineStartBlur,
            RenderingBias = RenderingBias.Performance
        };

        public StartUp(Action? cancelRequested = null)
        {
            _cancelRequested = cancelRequested;
            InitializeComponent();

            BylineText.Effect = _bylineBlur;

            try
            {
                VersionText.Text = new Services.AppInfoService().GetApplicationVersion();
            }
            catch (Exception)
            {
                VersionText.Text = string.Empty;
            }
        }

        public static StartUp CreateOnOwnThread(Action? cancelRequested = null)
        {
            StartUp? created = null;
            Exception? failure = null;

            using var ready = new ManualResetEventSlim(false);

            var thread = new Thread(() =>
            {
                try
                {
                    created = new StartUp(cancelRequested);
                    created.Show();
                    created.Activate();
                }
                catch (Exception ex)
                {
                    failure = ex;
                    created = null;
                }
                finally
                {
                    ready.Set();
                }

                if (failure == null)
                    System.Windows.Threading.Dispatcher.Run();
            })
            {
                IsBackground = true,
                Name = "MagicChatbox Splash"
            };

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            if (!ready.Wait(SplashCreationTimeout))
                throw new TimeoutException("The startup window did not open in time.");

            if (failure != null)
                throw failure;

            return created!;
        }

        public void SetTopmostFromAnyThread(bool value)
        {
            InvokeOnSplashThread(() => Topmost = value);
        }

        public void CloseFromAnyThread()
        {
            var dispatcher = Dispatcher;

            InvokeOnSplashThread(Close);

            try
            {
                if (!dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
                    dispatcher.InvokeShutdown();
            }
            catch (Exception)
            {
            }
        }

        private void InvokeOnSplashThread(Action action)
        {
            var dispatcher = Dispatcher;

            if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                return;

            try
            {
                if (dispatcher.CheckAccess())
                    action();
                else
                    dispatcher.Invoke(action);
            }
            catch (Exception)
            {
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            try
            {
                IntPtr handle = new WindowInteropHelper(this).Handle;
                int preference = DwmCornerRound;
                DwmSetWindowAttribute(handle, DwmWindowCornerPreference, ref preference, sizeof(int));
            }
            catch (Exception)
            {
            }
        }

        private void RevealByline(double progressPercent)
        {
            double fraction = Math.Clamp(progressPercent / BylineFullyRevealedAt, 0d, 1d);
            double eased = fraction * fraction;

            BylineText.BeginAnimation(
                OpacityProperty,
                new DoubleAnimation(eased * BylineMaxOpacity, TimeSpan.FromMilliseconds(350))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                });

            _bylineBlur.BeginAnimation(
                BlurEffect.RadiusProperty,
                new DoubleAnimation((1d - fraction) * BylineStartBlur, TimeSpan.FromMilliseconds(350))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                });
        }

        public void ShowUpdateSteps(Core.Updates.UpdateHandoffInfo info)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(() => ShowUpdateSteps(info));
                return;
            }

            if (info.IsRollback)
            {
                StepDownload.Text = "✔ Backup checked";
                StepVerify.Text = "✔ Contents complete";
                StepUnpack.Text = "✔ Current version saved";
                StepInstall.Text = "● Restoring";
                TrustLine.Text = string.IsNullOrWhiteSpace(info.TargetVersion)
                    ? "Putting your previous version back. The version you are on now is kept, so you can undo this."
                    : $"Putting {info.TargetVersion} back. The version you are on now is kept, so you can undo this.";
            }
            else
            {
                StepDownload.Text = "✔ Download";
                StepUnpack.Text = "✔ Unpack";
                StepInstall.Text = "● Install";

                switch (info.Integrity)
                {
                    case Core.Updates.DigestVerificationStatus.Match:
                        StepVerify.Text = "✔ Verify integrity";
                        StepVerify.Foreground = VerifiedBrush;
                        TrustLine.Text = $"Valid package from the developer · sha256 {info.ShortHash}";
                        TrustLine.Foreground = VerifiedBrush;
                        break;

                    case Core.Updates.DigestVerificationStatus.NotPublished:
                        StepVerify.Text = "! Verify integrity";
                        StepVerify.Foreground = CautionBrush;
                        TrustLine.Text = "This release published no checksum, so the download could not be checked against one.";
                        TrustLine.Foreground = CautionBrush;
                        break;

                    default:
                        StepVerify.Text = "✕ Verify integrity";
                        StepVerify.Foreground = CautionBrush;
                        TrustLine.Text = "The download did not match its published checksum.";
                        TrustLine.Foreground = CautionBrush;
                        break;
                }
            }

            UpdateStepsPanel.Visibility = Visibility.Visible;
        }

        public void MarkInstallComplete()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(MarkInstallComplete);
                return;
            }

            StepInstall.Text = "✔ Install";
            StepInstall.Foreground = VerifiedBrush;
        }

        public void UpdateProgress(string message, double value, string? nextHint = null)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(() => UpdateProgress(message, value, nextHint));
                return;
            }

            string elapsed = "";
            if (_stepTimer.IsRunning && !string.IsNullOrEmpty(_currentMessage))
            {
                var ms = _stepTimer.ElapsedMilliseconds;
                elapsed = ms >= 1000 ? $"{ms / 1000.0:F1}s" : $"{ms}ms";
            }

            _prevMessage = _currentMessage;
            _prevTime = elapsed;
            _currentMessage = message;
            _nextMessage = nextHint ?? "";

            _stepTimer.Restart();

            PrevStepText.Text = _prevMessage;
            PrevStepTime.Text = _prevTime;
            CurrentStepText.Text = _currentMessage;
            CurrentStepTime.Text = $"{_globalTimer.Elapsed.TotalSeconds:F1}s";
            NextStepText.Text = _nextMessage;

            AnimateProgress(value);
            RevealByline(value);
        }

        private void AnimateProgress(double targetValue)
        {
            double settleTo = Math.Max(targetValue, ProgressBar.Value);
            double drift = Math.Max(settleTo, Math.Min(settleTo + (100d - settleTo) * DriftShare, 99d));

            var settleAt = TimeSpan.FromMilliseconds(SettleMs);
            var driftAt = settleAt + TimeSpan.FromSeconds(DriftSeconds);

            var animation = new DoubleAnimationUsingKeyFrames
            {
                FillBehavior = FillBehavior.HoldEnd,
                Duration = driftAt
            };

            animation.KeyFrames.Add(new EasingDoubleKeyFrame(
                settleTo,
                KeyTime.FromTimeSpan(settleAt),
                new QuadraticEase { EasingMode = EasingMode.EaseOut }));

            animation.KeyFrames.Add(new EasingDoubleKeyFrame(
                drift,
                KeyTime.FromTimeSpan(driftAt),
                new QuadraticEase { EasingMode = EasingMode.EaseOut }));

            _currentProgress = settleTo;
            ProgressBar.BeginAnimation(RangeBase.ValueProperty, animation);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (Interlocked.Exchange(ref _cancelStarted, 1) == 1)
                return;

            CancelButton.IsEnabled = false;
            Cursor = Cursors.Wait;
            CurrentStepText.Text = "Cancelling startup...";
            NextStepText.Text = "Closing MagicChatbox if startup is stuck...";
            try
            {
                _cancelRequested?.Invoke();
            }
            catch (ObjectDisposedException)
            {
            }

            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                if (Application.Current?.Dispatcher.HasShutdownFinished != true)
                    Environment.Exit(0);
            });

            var app = Application.Current;
            app?.Dispatcher.BeginInvoke(new Action(() => app.Shutdown()));
        }

        private void DraggableGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            if (IsOverButton(e.OriginalSource as DependencyObject))
                return;

            DragMove();
        }

        private static bool IsOverButton(DependencyObject? source)
        {
            while (source != null)
            {
                if (source is System.Windows.Controls.Primitives.ButtonBase)
                    return true;

                source = System.Windows.Media.VisualTreeHelper.GetParent(source);
            }

            return false;
        }
    }
}
