using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace vrcosc_magicchatbox
{
    /// <summary>
    /// Interaction logic for StartUp.xaml
    /// </summary>
    public partial class StartUp : Window
    {
        public StartUp()
        {
            InitializeComponent();
        }

        public void UpdateProgress(string message, double value)
        {
            ProgressMessage.Text = message;
            ProgressBar.Value = value;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void DraggableGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }
    }
}
