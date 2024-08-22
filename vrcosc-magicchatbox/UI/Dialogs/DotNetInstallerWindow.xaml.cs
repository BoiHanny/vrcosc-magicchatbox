using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace vrcosc_magicchatbox.UI.Dialogs
{
    public partial class DotNetInstallerWindow : Window
    {
        public enum UserDecision
        {
            None,
            Install,
            Cancel
        }

        private TaskCompletionSource<UserDecision> _taskCompletionSource;

        public DotNetInstallerWindow()
        {
            InitializeComponent();
            _taskCompletionSource = new TaskCompletionSource<UserDecision>();
            Loaded += async (s, e) => await CheckDotNetInstallationAsync();
        }

        public Task<UserDecision> WaitForUserDecisionAsync()
        {
            return _taskCompletionSource.Task;
        }

        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (AskInstallPage.Visibility == Visibility.Visible)
            {
                ShowInstallPage();
                _taskCompletionSource.SetResult(UserDecision.Install);

                if (!IsAdministrator())
                {
                    ElevateAndRestart();
                }
                else
                {
                    await InstallDotNet8Async();
                }
            }
            else
            {
                Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _taskCompletionSource.SetResult(UserDecision.Cancel);
            Close();
        }

        private async Task CheckDotNetInstallationAsync()
        {
            ShowCheckPage();

            bool isDotNetInstalled = await Task.Run(() => IsDotNet8Installed());
            if (isDotNetInstalled)
            {
                ShowConfirmationPage();
            }
            else
            {
                ShowAskInstallPage();
            }
        }

        private async Task InstallDotNet8Async()
        {
            ShowInstallPage();
            string installerUrl = "https://download.visualstudio.microsoft.com/download/pr/907765b0-2bf8-494e-93aa-5ef9553c5d68/a9308dc010617e6716c0e6abd53b05ce/windowsdesktop-runtime-8.0.8-win-x64.exe";
            string installerPath = Path.Combine(Path.GetTempPath(), "runtime-desktop-8.0.8-windows-x64-installer.exe");

            try
            {
                await DownloadFileAsync(installerUrl, installerPath);
                await RunInstallerAsync(installerPath);
                bool isDotNetInstalled = await Task.Run(() => IsDotNet8Installed());

                if (isDotNetInstalled)
                {
                    ShowConfirmationPage();
                }
                else
                {
                    ShowInstallationFailedPage("Installation completed but .NET 8 wasn't detected. Please try again.");
                }
            }
            catch (Exception ex)
            {
                ShowInstallationFailedPage($"Installation failed: {ex.Message}");
            }
        }

        private async Task DownloadFileAsync(string url, string outputPath)
        {
            using (WebClient client = new WebClient())
            {
                await client.DownloadFileTaskAsync(new Uri(url), outputPath);
            }
        }

        private async Task RunInstallerAsync(string installerPath)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/Passive /norestart",
                UseShellExecute = true,
                CreateNoWindow = false
            };

            using (var process = Process.Start(processInfo))
            {
                await process.WaitForExitAsync();
                if (process.ExitCode != 0)
                {
                    throw new Exception($"Installer exited with code {process.ExitCode}");
                }
            }
        }

        private bool IsDotNet8Installed()
        {
            string desktopRuntimePath = @"C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App";
            return Directory.Exists(desktopRuntimePath) && Directory.GetDirectories(desktopRuntimePath).Any(name => Path.GetFileName(name).StartsWith("8."));
        }

        private bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private void ElevateAndRestart()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Process.GetCurrentProcess().MainModule.FileName,
                Arguments = "-installDotNetAdmin",
                Verb = "runas",
                UseShellExecute = true
            };

            try
            {
                Process.Start(startInfo);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to restart as administrator: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void ShowCheckPage()
        {
            SetPageVisibility(CheckDotNetPage);
            SetButtonVisibility(false, false);
        }

        public void ShowAskInstallPage()
        {
            SetPageVisibility(AskInstallPage);
            SetButtonVisibility(true, true);
        }

        public void ShowInstallPage()
        {
            SetPageVisibility(InstallDotNetPage);
            SetButtonVisibility(false, false);
        }

        public void ShowConfirmationPage()
        {
            SetPageVisibility(ConfirmDotNetPage);
            NextButton.Visibility = Visibility.Hidden;
            CancelButton.Visibility = Visibility.Hidden;
            CloseButton.Visibility = Visibility.Hidden;  // Initially hide the Close button

            CountdownTextBlock.Text = "This window will close in 3 seconds...";

            StartCountdown(3);
        }

        private void StartCountdown(int seconds)
        {
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            int remainingTime = seconds;

            timer.Tick += (sender, args) =>
            {
                remainingTime--;

                if (remainingTime > 0)
                {
                    CountdownTextBlock.Text = $"This window will close in {remainingTime} second{(remainingTime > 1 ? "s" : "")}...";
                }
                else
                {
                    timer.Stop();
                    CloseWindow_Click(null, null);  // Trigger the close action
                }
            };

            timer.Start();
        }



        public void ShowInstallationFailedPage(string errorMessage)
        {
            InstallationFailedText.Text = errorMessage;
            SetPageVisibility(InstallationFailedPage);
            SetButtonVisibility(false, false);
        }

        private void SetPageVisibility(UIElement visiblePage)
        {
            CheckDotNetPage.Visibility = Visibility.Hidden;
            AskInstallPage.Visibility = Visibility.Hidden;
            InstallDotNetPage.Visibility = Visibility.Hidden;
            ConfirmDotNetPage.Visibility = Visibility.Hidden;
            InstallationFailedPage.Visibility = Visibility.Hidden;

            visiblePage.Visibility = Visibility.Visible;
        }

        private void SetButtonVisibility(bool nextVisible, bool cancelVisible)
        {
            NextButton.Visibility = nextVisible ? Visibility.Visible : Visibility.Hidden;
            CancelButton.Visibility = cancelVisible ? Visibility.Visible : Visibility.Hidden;
        }

        private async void RetryInstall_Click(object sender, RoutedEventArgs e)
        {
            if (!IsAdministrator())
            {
                ElevateAndRestart();
            }
            else
            {
                await InstallDotNet8Async();
            }
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            if(sender == null)
            {
                Close();
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = Process.GetCurrentProcess().MainModule.FileName,
                UseShellExecute = true
            });
            Task.Delay(1000).Wait();
            Application.Current.Shutdown();
        }

        private void WhatsNET_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://dotnet.microsoft.com/en-us/learn/dotnet/what-is-dotnet",
                UseShellExecute = true
            });
        }

        private void Discord_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://discord.gg/ZaSFwBfhvG",
                UseShellExecute = true
            });
        }

        private void Github_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/BoiHanny/vrcosc-magicchatbox",
                UseShellExecute = true
            });
        }
    }
}
