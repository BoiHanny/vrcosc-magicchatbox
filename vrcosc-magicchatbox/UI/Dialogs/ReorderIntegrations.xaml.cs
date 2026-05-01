using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.UI.Dialogs
{
    /// <summary>
    /// Interaction logic for ReorderIntegrations.xaml
    /// </summary>
    public partial class ReorderIntegrations : Window
    {
        public ObservableCollection<string> TempOrder { get; }
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

            TempOrder = IntegrationDisplayState.NormalizeSortOrder(sourceOrder);
            DataContext = this;
        }

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
                TempOrder.Add(key);
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
            if (e.LeftButton != MouseButtonState.Pressed || OrderList.SelectedItem is not string selectedItem)
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
            if (!e.Data.GetDataPresent(typeof(string)))
                return;

            var droppedItem = (string)e.Data.GetData(typeof(string));
            var targetItem = e.OriginalSource is DependencyObject source
                ? FindAncestor<ListBoxItem>(source)?.DataContext as string
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
            _integrationDisplay.IntegrationSortOrder = IntegrationDisplayState.NormalizeSortOrder(TempOrder);

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
}
