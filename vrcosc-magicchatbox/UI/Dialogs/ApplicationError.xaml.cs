using System;
using System.Threading.Tasks;
using System.Windows;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes;
using vrcosc_magicchatbox.ViewModels;
using Newtonsoft.Json.Linq;
using vrcosc_magicchatbox.Classes.Modules;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using vrcosc_magicchatbox.DataAndSecurity;

namespace vrcosc_magicchatbox.UI.Dialogs
{
    /// <summary>
    /// Interaction logic for ManualPulsoidAuth.xaml
    /// </summary>
    public partial class ApplicationError : Window
    {
        public ApplicationError(Exception ex, bool autoclose, int autoCloseinMiliSeconds)
        {
            InitializeComponent();
            MainError.Text = ex.Message;
            CallStack.Text = ex.StackTrace;
            if(autoclose)
                _ = AutoClose(autoCloseinMiliSeconds);
            DataContext = ViewModel.Instance;
            CheckUpdateBtnn_Click(null, null);
        }

        private async Task AutoClose(int autoCloseinMiliSeconds)
        {
            await Task.Delay(autoCloseinMiliSeconds);
            Close();
        }

        private void Discord_Click(object sender, RoutedEventArgs e)
        { Process.Start("explorer", "https://discord.gg/ZaSFwBfhvG"); }

        private void Github_Click(object sender, RoutedEventArgs e)
        { Process.Start("explorer", "https://github.com/BoiHanny/vrcosc-magicchatbox/issues/new/choose"); }



        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
        {
            string logFolderPath = @"C:\temp\Vrcosc-MagicChatbox";
            if (Directory.Exists(logFolderPath))
            {
                Process.Start("explorer", logFolderPath);
            }
        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            UpdateApp updater = new UpdateApp();
            updater.SelectCustomZip();
        }

        private void NewVersion_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ViewModel.Instance.CanUpdate)
            {
                ViewModel.Instance.CanUpdate = false;
                ViewModel.Instance.CanUpdateLabel = false;
                UpdateApp updateApp = new UpdateApp();
                Task.Run(() => updateApp.PrepareUpdate());
            }
            else
            {
                Process.Start("explorer", "http://github.com/BoiHanny/vrcosc-magicchatbox/releases");
            }
        }

        private async Task ManualUpdateCheckAsync()
        {
            var updateCheckTask = DataController.CheckForUpdateAndWait(true);
            var delayTask = Task.Delay(TimeSpan.FromSeconds(8));

            await Task.WhenAny(updateCheckTask, delayTask);
        }

        private void CheckUpdateBtnn_Click(object sender, RoutedEventArgs e) { ManualUpdateCheckAsync(); }

        private void rollback_Click(object sender, RoutedEventArgs e)
        {
            UpdateApp updater = new UpdateApp();
            updater.StartRollback();
        }
    }
}
