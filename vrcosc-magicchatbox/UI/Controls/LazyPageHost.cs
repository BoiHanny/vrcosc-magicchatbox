using System;
using System.Diagnostics;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace vrcosc_magicchatbox.UI.Controls
{
    public sealed class LazyPageHost : Decorator
    {
        private static readonly Duration EnterDuration = new(TimeSpan.FromMilliseconds(170));

        private readonly TranslateTransform _slide = new();

        private DispatcherTimer? _teardown;

        public static readonly DependencyProperty PageTemplateProperty = DependencyProperty.Register(
            nameof(PageTemplate), typeof(DataTemplate), typeof(LazyPageHost),
            new PropertyMetadata(null));

        public static readonly DependencyProperty PageIndexProperty = DependencyProperty.Register(
            nameof(PageIndex), typeof(int), typeof(LazyPageHost),
            new PropertyMetadata(-1, OnStateChanged));

        public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
            nameof(SelectedIndex), typeof(int), typeof(LazyPageHost),
            new PropertyMetadata(-1, OnStateChanged));

        public static readonly DependencyProperty IsHostActiveProperty = DependencyProperty.Register(
            nameof(IsHostActive), typeof(bool), typeof(LazyPageHost),
            new PropertyMetadata(true, OnStateChanged));

        public static readonly DependencyProperty KeepAliveProperty = DependencyProperty.Register(
            nameof(KeepAlive), typeof(bool), typeof(LazyPageHost),
            new PropertyMetadata(true));

        public static readonly DependencyProperty TeardownDelayProperty = DependencyProperty.Register(
            nameof(TeardownDelay), typeof(TimeSpan), typeof(LazyPageHost),
            new PropertyMetadata(TimeSpan.FromSeconds(3)));

        public LazyPageHost()
        {
            Visibility = Visibility.Collapsed;
            RenderTransform = _slide;
        }

        public DataTemplate? PageTemplate
        {
            get => (DataTemplate?)GetValue(PageTemplateProperty);
            set => SetValue(PageTemplateProperty, value);
        }

        public int PageIndex
        {
            get => (int)GetValue(PageIndexProperty);
            set => SetValue(PageIndexProperty, value);
        }

        public int SelectedIndex
        {
            get => (int)GetValue(SelectedIndexProperty);
            set => SetValue(SelectedIndexProperty, value);
        }

        public bool IsHostActive
        {
            get => (bool)GetValue(IsHostActiveProperty);
            set => SetValue(IsHostActiveProperty, value);
        }

        public bool KeepAlive
        {
            get => (bool)GetValue(KeepAliveProperty);
            set => SetValue(KeepAliveProperty, value);
        }

        public TimeSpan TeardownDelay
        {
            get => (TimeSpan)GetValue(TeardownDelayProperty);
            set => SetValue(TeardownDelayProperty, value);
        }

        public bool IsRealized => Child != null;

        public void Realize()
        {
            _teardown?.Stop();

            if (Child != null || PageTemplate == null)
                return;

            long startTicks = Stopwatch.GetTimestamp();
            long startAllocated = GC.GetAllocatedBytesForCurrentThread();

            Child = PageTemplate.LoadContent() as UIElement;

            if (Child == null)
                return;

            string name = Child.GetType().Name;

            // Coercion catches values set from here on; this settles what the page was built with.
            ReducedVisuals.Refresh(Child);

            Core.Diagnostics.PerfProbe.Record(
                $"nav.build.{name}",
                Stopwatch.GetElapsedTime(startTicks).TotalMilliseconds,
                GC.GetAllocatedBytesForCurrentThread() - startAllocated);

            Logging.WriteInfo($"Page built: {name}");
        }

        public void Release()
        {
            _teardown?.Stop();

            if (Child == null)
                return;

            StopTransition();
            CommitPendingEdit();
            ReleaseFocus();

            string released = Child.GetType().Name;

            long startTicks = Stopwatch.GetTimestamp();
            Child = null;
            Core.Diagnostics.PerfProbe.Record(
                $"nav.release.{released}",
                Stopwatch.GetElapsedTime(startTicks).TotalMilliseconds);

            Logging.WriteInfo($"Page released: {released}");
        }

        private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((LazyPageHost)d).Sync();

        private void Sync()
        {
            if (IsHostActive && PageIndex >= 0 && PageIndex == SelectedIndex)
            {
                bool wasHidden = Visibility != Visibility.Visible;

                Realize();
                Visibility = Visibility.Visible;

                if (wasHidden)
                    PlayEnterTransition();

                return;
            }

            StopTransition();
            Visibility = Visibility.Collapsed;

            if (Child == null)
                return;

            CommitPendingEdit();

            if (!KeepAlive)
                ScheduleTeardown();
        }

        private void PlayEnterTransition()
        {
            if (ReducedVisuals.IsEnabled)
                return;

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            BeginAnimation(OpacityProperty, new DoubleAnimation(0.0, 1.0, EnterDuration)
            {
                FillBehavior = FillBehavior.Stop,
                EasingFunction = ease,
            });

            _slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10.0, 0.0, EnterDuration)
            {
                FillBehavior = FillBehavior.Stop,
                EasingFunction = ease,
            });
        }

        private void StopTransition()
        {
            BeginAnimation(OpacityProperty, null);
            _slide.BeginAnimation(TranslateTransform.YProperty, null);
        }

        private void ScheduleTeardown()
        {
            if (_teardown == null)
            {
                _teardown = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
                {
                    Interval = TeardownDelay,
                };
                _teardown.Tick += OnTeardownTick;
            }
            else
            {
                _teardown.Interval = TeardownDelay;
            }

            _teardown.Stop();
            _teardown.Start();
        }

        private void OnTeardownTick(object? sender, EventArgs e)
        {
            _teardown?.Stop();

            if (IsHostActive && PageIndex == SelectedIndex)
                return;

            Release();
        }

        private void ReleaseFocus()
        {
            if (Keyboard.FocusedElement is DependencyObject focused && IsInThisPage(focused))
                Keyboard.ClearFocus();

            DependencyObject? scope = FocusManager.GetFocusScope(this);
            if (scope != null
                && FocusManager.GetFocusedElement(scope) is DependencyObject scoped
                && IsInThisPage(scoped))
            {
                FocusManager.SetFocusedElement(scope, null);
            }
        }

        private void CommitPendingEdit()
        {
            if (Keyboard.FocusedElement is not TextBox box || !IsInThisPage(box))
                return;

            box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }

        private bool IsInThisPage(DependencyObject element)
        {
            DependencyObject? current = element;

            while (current != null)
            {
                if (ReferenceEquals(current, this))
                    return true;

                current = current is Visual or System.Windows.Media.Media3D.Visual3D
                    ? VisualTreeHelper.GetParent(current)
                    : LogicalTreeHelper.GetParent(current);
            }

            return false;
        }

    }
}
