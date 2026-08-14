using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.UI.Pages
{
    public partial class IntegrationsPage : UserControl
    {
        private ObservableCollection<string> _integrationSortOrder;

        // One entry per realised lyrics ribbon (there are two - the Spotify card and Media link), holding
        // the ScrollViewer it subscribed to and the exact delegate, so Unloaded can detach it again.
        private readonly Dictionary<FrameworkElement, (ScrollViewer Scroller, ScrollChangedEventHandler Handler)> _ribbonScrollHooks = new();

        private IntegrationsPageViewModel? VM => DataContext as IntegrationsPageViewModel;

        public IntegrationsPage()
        {
            InitializeComponent();
            DataContextChanged += (_, e) =>
            {
                if (e.OldValue is IntegrationsPageViewModel oldVm)
                    oldVm.IntegrationDisplay.PropertyChanged -= IntegrationDisplay_PropertyChanged;

                if (e.NewValue is IntegrationsPageViewModel vm)
                {
                    vm.IntegrationDisplay.PropertyChanged += IntegrationDisplay_PropertyChanged;
                    HookIntegrationSortOrder();

                    vm.TileLayoutChanged -= OnTileLayoutChanged;
                    vm.TileLayoutChanged += OnTileLayoutChanged;
                    vm.TileShown -= OnTileShown;
                    vm.TileShown += OnTileShown;

                    ApplyIntegrationOrder();
                }
            };
        }

        public void ApplyIntegrationOrder()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(ApplyIntegrationOrder);
                return;
            }

            var vm = VM;
            if (vm == null || IntegrationsList == null) return;

            var itemMap = new Dictionary<string, ListBoxItem>(StringComparer.OrdinalIgnoreCase)
            {
                { "Status", StatusItem },
                { "Window", WindowActivityItem },
                { "HeartRate", HeartRateItem },
                { "TrackerBattery", TrackerBatteryItem },
                { "VrPerformance", VrPerformanceItem },
                { "Component", ComponentStatsItem },
                { "Network", NetworkStatsItem },
                { "Time", TimeItem },
                { "Weather", WeatherItem },
                { "Twitch", TwitchItem },
                { "TikTokLive", TikTokLiveItem },
                { "Discord", DiscordItem },
                { "VrcRadar", VrcRadarItem },
                { "Soundpad", SoundpadItem },
                { "Spotify", SpotifyItem },
                { "MediaLink", MediaLinkItem }
            };

            IEnumerable<string> orderedKeys = vm.IntegrationDisplay.IntegrationSortOrder?.Count > 0
                ? vm.IntegrationDisplay.IntegrationSortOrder
                : IntegrationDisplayState.DefaultSortOrder;

            var hidden = IntegrationTileCatalog.ResolveHidden(vm.IntegrationSettings?.HiddenTiles);
            var usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            IntegrationsList.BeginInit();
            IntegrationsList.Items.Clear();

            // The hidden strip is the first item rather than a pinned row above the list, so it scrolls
            // away with the content instead of permanently taking space at the top of the page.
            if (HiddenStripItem != null && hidden.Count > 0)
                IntegrationsList.Items.Add(HiddenStripItem);

            foreach (var key in orderedKeys)
            {
                if (itemMap.TryGetValue(key, out var item))
                {
                    // Recorded as used before the hidden check. If a hidden key were left unrecorded, the
                    // safety-net pass below would add it straight back at the bottom of the list.
                    usedKeys.Add(key);

                    if (!hidden.Contains(key))
                        IntegrationsList.Items.Add(item);
                }
            }

            foreach (var kvp in itemMap)
            {
                if (usedKeys.Contains(kvp.Key)) continue;
                if (hidden.Contains(kvp.Key)) continue;
                IntegrationsList.Items.Add(kvp.Value);
            }

            IntegrationsList.EndInit();
        }

        private void OnTileLayoutChanged(object sender, EventArgs e) => ApplyIntegrationOrder();

        private void OnTileShown(object sender, string key)
        {
            if (!IntegrationTileCatalog.TryGet(key, out var tile)) return;

            // The item is only added to Items during the relayout above, so wait for that pass to finish
            // before trying to scroll to it.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var container = IntegrationsList.Items
                    .OfType<ListBoxItem>()
                    .FirstOrDefault(i => string.Equals(i.Name, tile.ElementName, StringComparison.Ordinal));

                if (container != null)
                    IntegrationsList.ScrollIntoView(container);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void HideTile_Click(object sender, RoutedEventArgs e)
        {
            // No CommandParameter anywhere in the XAML: the key is recovered from the tile the button sits in,
            // so 16 hand-typed strings cannot drift out of sync with itemMap.
            if (sender is not DependencyObject source) return;

            var container = ItemsControl.ContainerFromElement(IntegrationsList, source) as ListBoxItem;
            if (container?.Name == null) return;

            var tile = IntegrationTileCatalog.Tiles
                .FirstOrDefault(t => string.Equals(t.ElementName, container.Name, StringComparison.Ordinal));

            if (tile != null)
                VM?.HideTileCommand.Execute(tile.Key);
        }

        private void HookIntegrationSortOrder()
        {
            if (_integrationSortOrder != null)
                _integrationSortOrder.CollectionChanged -= IntegrationSortOrder_CollectionChanged;

            _integrationSortOrder = VM?.IntegrationDisplay.IntegrationSortOrder;
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
            => VM?.ManualBuildOscCommand.Execute(null);

        private void ResolveComponentStatsAccess_Click(object sender, RoutedEventArgs e)
            => VM?.ResolveComponentStatsAccessCommand.Execute(null);

        private void MediaSessionPausePlay_Click(object sender, RoutedEventArgs e)
            => VM?.MediaPlayPauseCommand.Execute((sender as Button)?.Tag as MediaSessionInfo);

        private void MediaSessionNext_Click(object sender, RoutedEventArgs e)
            => VM?.MediaNextCommand.Execute((sender as Button)?.Tag as MediaSessionInfo);

        private void MediaSessionPrevious_Click(object sender, RoutedEventArgs e)
            => VM?.MediaPreviousCommand.Execute((sender as Button)?.Tag as MediaSessionInfo);

        private void MediaSessionRow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (IsMediaSessionControl(e.OriginalSource as DependencyObject))
                return;

            VM?.SelectMediaSessionCommand.Execute((sender as FrameworkElement)?.Tag as MediaSessionInfo);
        }

        private static bool IsMediaSessionControl(DependencyObject? source)
        {
            while (source != null)
            {
                if (source is ButtonBase or ProgressBar or Slider or TextBox)
                    return true;

                source = VisualTreeHelper.GetParent(source);
            }

            return false;
        }

        private void MediaProgressbar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var progress = sender as ProgressBar;
            var session = progress?.Tag as MediaSessionInfo;
            var vm = VM;
            if (progress != null && session != null && vm != null && progress.ActualWidth > 0)
            {
                double fraction = Math.Clamp(e.GetPosition(progress).X / progress.ActualWidth, 0d, 1d);
                _ = vm.SeekMedia(session, fraction, progress.Maximum);
            }
        }

        private void MainDiscoundButton_grid_MouseUp(object sender, MouseButtonEventArgs e)
            => VM?.ActivateSettingCommand.Execute("Settings_HeartRate");

        private void SoundPadPlay_Click(object sender, RoutedEventArgs e)
            => VM?.SoundpadPlayPauseCommand.Execute(null);

        private void SoundPadPause_Click(object sender, RoutedEventArgs e)
            => VM?.SoundpadPlayPauseCommand.Execute(null);

        private void SoundPadPrevious_Click(object sender, RoutedEventArgs e)
            => VM?.SoundpadPreviousCommand.Execute(null);

        private void SoundPadNext_Click(object sender, RoutedEventArgs e)
            => VM?.SoundpadNextCommand.Execute(null);

        private void SoundPadStop_Click(object sender, RoutedEventArgs e)
            => VM?.SoundpadStopCommand.Execute(null);

        private void SoundPadRandon_Click(object sender, RoutedEventArgs e)
            => VM?.SoundpadRandomCommand.Execute(null);

        /// <summary>
        /// The lyrics ribbon hops between the Spotify and Media link cards, and the losing card's copy
        /// of the template just collapses. WPF hides the popup along with it, but the toggle would stay
        /// latched, so the next click on it would only untick and open nothing.
        /// </summary>
        /// <remarks>
        /// A style trigger on IsVisible cannot do this: a user click sets IsChecked as a local value,
        /// which outranks any style setter.
        /// </remarks>
        private void LyricsRibbonRoot_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is true) return;

            CloseLyricsFlyout(sender as FrameworkElement);
        }

        /// <summary>
        /// The sync flyout is a <see cref="System.Windows.Controls.Primitives.Popup"/>, so it is pinned in
        /// screen space and does not travel with the ribbon when the integrations list scrolls. Closing it
        /// on scroll is cheaper than trying to keep it glued.
        /// </summary>
        /// <remarks>
        /// ScrollChanged cannot be handled on the ribbon itself, despite being a bubbling routed event: the
        /// ScrollViewer that raises it is an ANCESTOR of the ribbon, inside the ListBox template, so the
        /// event travels away from the ribbon rather than through it. The subscription therefore goes on
        /// that ScrollViewer, and the handler closes only the ribbon instance it was created for - the
        /// template is realised twice, once on the Spotify card and once on Media link, and scrolling must
        /// not reach across into the other card's popup.
        /// </remarks>
        private void LyricsRibbonRoot_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement root || _ribbonScrollHooks.ContainsKey(root)) return;

            var scroller = FindAncestorScrollViewer(root);
            if (scroller == null) return;

            void OnScrollChanged(object s, ScrollChangedEventArgs args)
            {
                // Extent/viewport changes raise this too - a relayout must not slam the flyout shut.
                if (args.VerticalChange == 0 && args.HorizontalChange == 0) return;

                CloseLyricsFlyout(root);
            }

            scroller.ScrollChanged += OnScrollChanged;
            _ribbonScrollHooks[root] = (scroller, OnScrollChanged);
        }

        private void LyricsRibbonRoot_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement root) return;
            if (!_ribbonScrollHooks.Remove(root, out var hook)) return;

            hook.Scroller.ScrollChanged -= hook.Handler;
            CloseLyricsFlyout(root);
        }

        private static void CloseLyricsFlyout(FrameworkElement? ribbonRoot)
        {
            if (ribbonRoot?.FindName("LyricsSyncToggle") is ToggleButton toggle)
                toggle.IsChecked = false;
        }

        private static ScrollViewer? FindAncestorScrollViewer(DependencyObject? from)
        {
            while (from != null)
            {
                if (from is ScrollViewer scroller) return scroller;
                from = VisualTreeHelper.GetParent(from);
            }

            return null;
        }

        private void SpotifyVolume_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Slider slider)
                _ = VM?.SetSpotifyVolume(slider.Value);
        }
    }
}
