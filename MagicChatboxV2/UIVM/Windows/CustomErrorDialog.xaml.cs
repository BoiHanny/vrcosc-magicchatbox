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
using System.Windows.Threading;

namespace MagicChatboxV2.UIVM.Windows
{
    /// <summary>
    /// Interaction logic for CustomErrorDialog.xaml
    /// </summary>
    public partial class CustomErrorDialog : Window
    {
        private DispatcherTimer autoCloseTimer;

        public CustomErrorDialog(string errorMessage, string stackTrace)
        {
            InitializeComponent();
            txtMainError.Text = errorMessage;
            txtStackTrace.Text = stackTrace;
            StartAutoCloseTimer();
        }

        private void StartAutoCloseTimer()
        {
            autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) }; // Adjust time as needed
            autoCloseTimer.Tick += (sender, args) =>
            {
                autoCloseTimer.Stop();
                this.Close(); // Close dialog after timer
            };
            autoCloseTimer.Start();
        }

        private void BtnContinue_Click(object sender, RoutedEventArgs e)
        {
            this.Close(); // Allow user to continue using the application
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown(); // Exit application
        }
    }
}
