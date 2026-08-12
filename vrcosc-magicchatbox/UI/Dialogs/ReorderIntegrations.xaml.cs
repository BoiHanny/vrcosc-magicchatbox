using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.UI.Dialogs
{
    public partial class ReorderIntegrations : Window
    {
        public ObservableCollection<ReorderRow> TempOrder { get; }
        private readonly HashSet<string> _hiddenKeys;
        private readonly IntegrationDisplayState _integrationDisplay;
        private readonly ISettingsProvider<IntegrationSettings> _integrationSettingsProvider;
        private Point _dragStartPoint;

        public ReorderIntegrations(
            IntegrationDisplayState integrationDisplay,
            ISettingsProvider<IntegrationSettings> integrationSettingsProvider)
        {
            InitializeComponent();
            _integrationDisplay = integrationDisplay;
            _integrationSettingsProvider = integrationSettingsProvider;

            var sourceOrder = _integrationDisplay.IntegrationSortOrder?.Count > 0
                ? _integrationDisplay.IntegrationSortOrder
                : IntegrationDisplayState.DefaultSortOrder;

            _hiddenKeys = IntegrationTileCatalog.ResolveHidden(
                integrationSettingsProvider.Value?.HiddenTiles);

            TempOrder = new ObservableCollection<ReorderRow>(
                IntegrationDisplayState.NormalizeSortOrder(sourceOrder)
                    .Where(key => !IntegrationDisplayState.IsFollower(key))
                    .Select(CreateRow));

            DataContext = this;
        }

        private ReorderRow CreateRow(string key) => new(
            key,
            IntegrationTileCatalog.DisplayNameFor(key),
            _hiddenKeys.Contains(key));

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            int index = OrderList.SelectedIndex;
            if (index <= 0 || index >= TempOrder.Count) return;

            TempOrder.Move(index, index - 1);
            OrderList.SelectedIndex = index - 1;
            OrderList.ScrollIntoView(OrderList.SelectedItem);
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            int index = OrderList.SelectedIndex;
            if (index < 0 || index >= TempOrder.Count - 1) return;

            TempOrder.Move(index, index + 1);
            OrderList.SelectedIndex = index + 1;
            OrderList.ScrollIntoView(OrderList.SelectedItem);
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            TempOrder.Clear();
            foreach (var key in IntegrationDisplayState.DefaultSortOrder)
            {
                if (IntegrationDisplayState.IsFollower(key))
                    continue;

                TempOrder.Add(CreateRow(key));
            }
        }

        private void OrderList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);

            if (e.OriginalSource is DependencyObject source
                && FindAncestor<ListBoxItem>(source) is { } item)
                OrderList.SelectedItem = item.DataContext;
        }

        private void OrderList_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || OrderList.SelectedItem is not ReorderRow selectedItem)
                return;

            Point currentPosition = e.GetPosition(null);
            Vector distance = _dragStartPoint - currentPosition;

            if (Math.Abs(distance.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(distance.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            DragDrop.DoDragDrop(OrderList, selectedItem, DragDropEffects.Move);
        }

        private void OrderList_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(ReorderRow)))
                return;

            var droppedItem = (ReorderRow)e.Data.GetData(typeof(ReorderRow));
            var targetItem = e.OriginalSource is DependencyObject source
                ? FindAncestor<ListBoxItem>(source)?.DataContext as ReorderRow
                : null;

            int oldIndex = TempOrder.IndexOf(droppedItem);
            int newIndex = targetItem is null ? TempOrder.Count - 1 : TempOrder.IndexOf(targetItem);

            if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex)
                return;

            TempOrder.Move(oldIndex, newIndex);
            OrderList.SelectedItem = droppedItem;
            OrderList.ScrollIntoView(droppedItem);
        }

        private static T? FindAncestor<T>(DependencyObject? current)
            where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T ancestor)
                    return ancestor;

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            _integrationDisplay.IntegrationSortOrder =
                IntegrationDisplayState.NormalizeSortOrder(TempOrder.Select(row => row.Key));

            if (Owner is MainWindow mainWindow)
            {
                mainWindow.ApplyIntegrationOrder();
            }

            var provider = _integrationSettingsProvider;
            provider.Value.SavedSortOrder = _integrationDisplay.IntegrationSortOrder;
            provider.Save();
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => CloseDialog();

        private void Button_close_Click(object sender, RoutedEventArgs e) => CloseDialog();

        private void CloseDialog()
        {
            Close();
        }
    }

    /// <summary>
    /// One row in the reorder list. Carries the friendly name and whether the tile is currently hidden
    /// on the Integrations page, so the dialog stops silently disagreeing with what the user sees there.
    /// </summary>
    public sealed class ReorderRow
    {
        public ReorderRow(string key, string displayName, bool isHidden)
        {
            Key = key;
            DisplayName = displayName;
            IsHidden = isHidden;
        }

        public string Key { get; }

        public string DisplayName { get; }

        public bool IsHidden { get; }

        public string Label => IsHidden ? $"{DisplayName}  (hidden)" : DisplayName;

        public double RowOpacity => IsHidden ? 0.45 : 1.0;

        public string RowToolTip => IsHidden
            ? $"{DisplayName} is hidden on the Integrations page. Its position here is still saved."
            : DisplayName;

        public override string ToString() => Key;
    }
}
