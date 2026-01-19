using System.Collections.ObjectModel;
using System.Windows;
using vrcosc_magicchatbox.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.UI.Dialogs
{
    /// <summary>
    /// Interaction logic for ReorderIntegrations.xaml
    /// </summary>
    public partial class ReorderIntegrations : Window
    {
        public ObservableCollection<string> TempOrder { get; }

        public ReorderIntegrations()
        {
            InitializeComponent();

            var sourceOrder = ViewModel.Instance.IntegrationSortOrder?.Count > 0
                ? ViewModel.Instance.IntegrationSortOrder
                : ViewModel.DefaultIntegrationSortOrder;

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
            foreach (var key in ViewModel.DefaultIntegrationSortOrder)
            {
                TempOrder.Add(key);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Instance.IntegrationSortOrder = new ObservableCollection<string>(TempOrder);

            if (Owner is MainWindow mainWindow)
            {
                mainWindow.ApplyIntegrationOrder();
            }

            DataController.ManageSettingsXML(true);
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
