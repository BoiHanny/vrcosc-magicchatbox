using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
