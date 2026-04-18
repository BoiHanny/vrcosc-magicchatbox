using System.Collections.ObjectModel;
using System.Windows;
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

            TempOrder = new ObservableCollection<string>(sourceOrder);
            DataContext = this;
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            int index = OrderList.SelectedIndex;
            if (index > 0)
            {
                TempOrder.Move(index, index - 1);
                OrderList.SelectedIndex = index - 1;
            }
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            int index = OrderList.SelectedIndex;
            if (index >= 0 && index < TempOrder.Count - 1)
            {
                TempOrder.Move(index, index + 1);
                OrderList.SelectedIndex = index + 1;
            }
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            TempOrder.Clear();
            foreach (var key in IntegrationDisplayState.DefaultSortOrder)
            {
                TempOrder.Add(key);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            _integrationDisplay.IntegrationSortOrder = new ObservableCollection<string>(TempOrder);

            if (Owner is MainWindow mainWindow)
            {
                mainWindow.ApplyIntegrationOrder();
            }

            var provider = _integrationSettingsProvider;
            provider.Value.SavedSortOrder = _integrationDisplay.IntegrationSortOrder;
            provider.Save();
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Button_close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
