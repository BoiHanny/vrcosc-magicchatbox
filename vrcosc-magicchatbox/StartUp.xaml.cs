using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace vrcosc_magicchatbox
{
    /// <summary>
    /// Loading splash with rolling 3-strip step display, per-step timing, and smooth progress bar.
    /// </summary>
    public partial class StartUp : Window
    {
        private readonly Stopwatch _globalTimer = Stopwatch.StartNew();
        private readonly Stopwatch _stepTimer = new();
        private string _prevMessage = "";
        private string _prevTime = "";
        private string _currentMessage = "";
        private string _nextMessage = "";
        private double _currentProgress;

        public StartUp()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Advance to a new step. The previous step rolls up, the current message becomes the
        /// active strip, and <paramref name="nextHint"/> (if any) previews below at 0.5 opacity.
        /// The progress bar animates smoothly to <paramref name="value"/>.
        /// </summary>
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
        }

        private void AnimateProgress(double targetValue)
        {
            var animation = new DoubleAnimation
            {
                From = _currentProgress,
                To = targetValue,
                Duration = TimeSpan.FromMilliseconds(350),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            _currentProgress = targetValue;
            ProgressBar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, animation);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void DraggableGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }
    }
}
