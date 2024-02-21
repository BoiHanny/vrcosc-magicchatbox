using MagicChatboxV2.Services;
using MagicChatboxV2.UIVM.Windows;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using static MagicChatboxV2.App;

namespace MagicChatboxV2.Helpers
{
    public class StartupHelper
    {
        private readonly IServiceProvider serviceProvider;
        private readonly PrimaryInterfaceFactory _mainWindowFactory;
        private LoadingWindow loadingWindow;

        public StartupHelper(IServiceProvider serviceProvider, PrimaryInterfaceFactory mainWindowFactory)
        {
            this.serviceProvider = serviceProvider;
            this._mainWindowFactory = mainWindowFactory;
        }

        // Start method to initiate the startup process
        public void Start()
        {
            try
            {
                ShowLoadingWindow();
                InitializeServices();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during service initialization");
                MessageBox.Show($"Failed to initialize services: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                CloseLoadingWindow();
                Application.Current.Shutdown();
                return;
            }

            CloseLoadingWindow();

            try
            {
                var vrChatService = serviceProvider.GetService<VRChatMonitorService>();
                vrChatService.OnVRChatStarted += OnVRChatStarted;
                vrChatService.StartMonitoring();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to start VRChat monitoring");
                MessageBox.Show($"Failed to start VRChat monitoring: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            }
        }

        // Show the loading window
        private void ShowLoadingWindow()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                loadingWindow = new LoadingWindow();
                loadingWindow.Show();
            });
        }

        // Initialize the services
        private void InitializeServices()
        {
            UpdateProgress(0, "Initializing System Tray...");
            var trayService = serviceProvider.GetService<SystemTrayService>();
            trayService.InitializeTrayIcon();
            UpdateProgress(50, "System Tray Initialized.");

            UpdateProgress(100, "Initialization Complete.");
        }

        // Update the progress of the loading window
        private void UpdateProgress(double progress, string status)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                loadingWindow.UpdateProgress(progress, status);
            });
        }

        // Close the loading window
        private void CloseLoadingWindow()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                loadingWindow.Close();
            });
        }

        // Event handler for when VRChat is started
        private void OnVRChatStarted()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var mainWindow = _mainWindowFactory();
                if (!mainWindow.IsVisible)
                {
                    mainWindow.Show();
                }
                else
                {
                    mainWindow.Activate();
                }
            });
        }

    }
}
