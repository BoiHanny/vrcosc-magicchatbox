using System.Windows;
using System.Windows.Input;

namespace MagicChatboxV2.Startup.Windows
{
    public partial class LoadingWindow : Window
    {
        public LoadingWindow(LoadingWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
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
