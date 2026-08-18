using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace vrcosc_magicchatbox.UI.Controls
{
    public sealed class LazyPageHost : Decorator
    {
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

        public static readonly DependencyProperty KeepAliveProperty = DependencyProperty.Register(
            nameof(KeepAlive), typeof(bool), typeof(LazyPageHost),
            new PropertyMetadata(true));

        public static readonly DependencyProperty TeardownDelayProperty = DependencyProperty.Register(
            nameof(TeardownDelay), typeof(TimeSpan), typeof(LazyPageHost),
            new PropertyMetadata(TimeSpan.FromSeconds(20)));

        public LazyPageHost()
        {
            Visibility = Visibility.Collapsed;
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

            Child = PageTemplate.LoadContent() as UIElement;
        }

        public void Release()
        {
            _teardown?.Stop();
            Child = null;
        }

        private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((LazyPageHost)d).Sync();

        private void Sync()
        {
            if (PageIndex >= 0 && PageIndex == SelectedIndex)
            {
                Realize();
                Visibility = Visibility.Visible;
                return;
            }

            Visibility = Visibility.Collapsed;

            if (!KeepAlive && Child != null)
                ScheduleTeardown();
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

            if (PageIndex == SelectedIndex)
                return;

            Child = null;
        }
    }
}
