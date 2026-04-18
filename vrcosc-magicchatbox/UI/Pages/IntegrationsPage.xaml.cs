using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.UI.Pages
{
    /// <summary>Code-behind for the integrations page; applies and tracks the user-defined integration sort order.</summary>
    public partial class IntegrationsPage : UserControl
    {
        private ObservableCollection<string> _integrationSortOrder;
        private IntegrationsPageViewModel VM => (IntegrationsPageViewModel)DataContext;

        public IntegrationsPage()
        {
            InitializeComponent();
            Loaded += IntegrationsPage_Loaded;
        }

        private void IntegrationsPage_Loaded(object sender, RoutedEventArgs e)
        {
            VM.IntegrationDisplay.PropertyChanged += IntegrationDisplay_PropertyChanged;
            HookIntegrationSortOrder();
            ApplyIntegrationOrder();
        }

        public void ApplyIntegrationOrder()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(ApplyIntegrationOrder);
                return;
            }

            if (IntegrationsList == null) return;

            var itemMap = new Dictionary<string, ListBoxItem>(StringComparer.OrdinalIgnoreCase)
            {
                { "Status", StatusItem },
                { "Window", WindowActivityItem },
                { "HeartRate", HeartRateItem },
                { "TrackerBattery", TrackerBatteryItem },
                { "Component", ComponentStatsItem },
                { "Network", NetworkStatsItem },
                { "Time", TimeItem },
                { "Weather", WeatherItem },
                { "Twitch", TwitchItem },
                { "Soundpad", SoundpadItem },
                { "Spotify", SpotifyItem },
                { "MediaLink", MediaLinkItem }
            };

            IEnumerable<string> orderedKeys = VM.IntegrationDisplay.IntegrationSortOrder?.Count > 0
                ? VM.IntegrationDisplay.IntegrationSortOrder
                : IntegrationDisplayState.DefaultSortOrder;

            var usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            IntegrationsList.BeginInit();
            IntegrationsList.Items.Clear();

            foreach (var key in orderedKeys)
            {
                if (itemMap.TryGetValue(key, out var item))
                {
                    IntegrationsList.Items.Add(item);
                    usedKeys.Add(key);
                }
            }

            foreach (var kvp in itemMap)
            {
                if (!usedKeys.Contains(kvp.Key))
                    IntegrationsList.Items.Add(kvp.Value);
            }

            IntegrationsList.EndInit();
        }

        private void HookIntegrationSortOrder()
        {
            if (_integrationSortOrder != null)
                _integrationSortOrder.CollectionChanged -= IntegrationSortOrder_CollectionChanged;

            _integrationSortOrder = VM.IntegrationDisplay.IntegrationSortOrder;
            if (_integrationSortOrder != null)
                _integrationSortOrder.CollectionChanged += IntegrationSortOrder_CollectionChanged;
        }

        private void IntegrationSortOrder_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
            => ApplyIntegrationOrder();

        private void IntegrationDisplay_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IntegrationDisplayState.IntegrationSortOrder))
            {
                HookIntegrationSortOrder();
                ApplyIntegrationOrder();
            }
        }

        private void Update_Click(object sender, RoutedEventArgs e)
            => VM.ManualBuildOscCommand.Execute(null);

        private void RestartApplicationAsAdmin_Click(object sender, RoutedEventArgs e)
            => VM.RestartAsAdminCommand.Execute(null);

        private void MediaSessionPausePlay_Click(object sender, RoutedEventArgs e)
            => VM.MediaPlayPauseCommand.Execute((sender as Button)?.Tag as MediaSessionInfo);

        private void MediaSessionNext_Click(object sender, RoutedEventArgs e)
            => VM.MediaNextCommand.Execute((sender as Button)?.Tag as MediaSessionInfo);

        private void MediaSessionPrevious_Click(object sender, RoutedEventArgs e)
            => VM.MediaPreviousCommand.Execute((sender as Button)?.Tag as MediaSessionInfo);

        private void MediaProgressbar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var progress = sender as ProgressBar;
            var session = progress?.Tag as MediaSessionInfo;
            if (progress != null && session != null)
            {
                double fraction = e.GetPosition(progress).X / progress.ActualWidth;
                _ = VM.SeekMedia(session, fraction, progress.Maximum);
            }
        }

        private void MainDiscoundButton_grid_MouseUp(object sender, MouseButtonEventArgs e)
            => VM.ActivateSettingCommand.Execute("Settings_HeartRate");

        private void SoundPadPlay_Click(object sender, RoutedEventArgs e)
            => VM.SoundpadPlayPauseCommand.Execute(null);

        private void SoundPadPause_Click(object sender, RoutedEventArgs e)
            => VM.SoundpadPlayPauseCommand.Execute(null);

        private void SoundPadPrevious_Click(object sender, RoutedEventArgs e)
            => VM.SoundpadPreviousCommand.Execute(null);

        private void SoundPadNext_Click(object sender, RoutedEventArgs e)
            => VM.SoundpadNextCommand.Execute(null);

        private void SoundPadStop_Click(object sender, RoutedEventArgs e)
            => VM.SoundpadStopCommand.Execute(null);

        private void SoundPadRandon_Click(object sender, RoutedEventArgs e)
            => VM.SoundpadRandomCommand.Execute(null);
    }
}
