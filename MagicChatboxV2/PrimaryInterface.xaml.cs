using MagicChatboxV2.Services;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MagicChatboxV2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class PrimaryInterface : Window
    {
        private readonly ModuleManagerService _moduleManagerService;

        // Constructor injection of ModuleManagerService
        public PrimaryInterface(ModuleManagerService moduleManagerService)
        {
            InitializeComponent();
            _moduleManagerService = moduleManagerService;
        }

        private void GET_Click(object sender, RoutedEventArgs e)
        {
            // Retrieve formatted outputs from all active modules and display them
            var formattedOutputs = _moduleManagerService.GetFormattedOutputs();
            // Assuming you want to display these outputs in a TextBlock or similar control
            // For demonstration, updating the Button content; adjust based on your UI design
            GetFormattedOutput.Text = formattedOutputs;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true; // Cancel the closing
            this.Hide(); // Hide the window instead of closing it
            base.OnClosing(e);
        }

        private void disposemodules_Click(object sender, RoutedEventArgs e)
        {
            _moduleManagerService.DisposeModules();
        }

        private void startmodules_Click(object sender, RoutedEventArgs e)
        {
            _moduleManagerService.InitializeModules();
        }
    }

}