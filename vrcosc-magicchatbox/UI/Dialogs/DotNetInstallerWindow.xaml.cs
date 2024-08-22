using Microsoft.Win32;
using NAudio.Wave;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;

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

        private TaskCompletionSource<UserDecision> tcs;

        public DotNetInstallerWindow()
        {
            InitializeComponent();
            tcs = new TaskCompletionSource<UserDecision>();
            CheckDotNetInstallation();
        }

        public Task<UserDecision> WaitForUserDecisionAsync()
        {
            return tcs.Task;
        }

        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (AskInstallPage.Visibility == Visibility.Visible)
            {
                ShowInstallPage();
                tcs.SetResult(UserDecision.Install);
            }
            else
            {
                Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            tcs.SetResult(UserDecision.Cancel);
            Close();
        }

        private async void CheckDotNetInstallation()
        {
            ShowCheckPage();

            if (IsDotNet8Installed())
            {
                ShowConfirmationPage();
            }
            else
            {
                ShowAskInstallPage();
            }
        }

        public async Task InstallDotNet8Async()
        {
            try
            {
                if (!IsAdministrator())
                {
                    ElevateAndRestart("-installDotNetAdmin");
                    return;
                }

                string installerUrl = "https://dotnet.microsoft.com/download/dotnet/thank-you/runtime-8.0.0-windows-x64-installer";
                string installerPath = Path.Combine(Path.GetTempPath(), "dotnet-runtime-8.0.0-win-x64.exe");

                using (WebClient client = new WebClient())
                {
                    await client.DownloadFileTaskAsync(new Uri(installerUrl), installerPath);
                }

                var processInfo = new ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = "/quiet /norestart",
                    UseShellExecute = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    await process.WaitForExitAsync();
                    if (process.ExitCode != 0)
                    {
                        throw new Exception("Installation failed with exit code " + process.ExitCode);
                    }
                }

                ShowConfirmationPage();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error installing .NET 8: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public bool IsDotNet8Installed()
        {
            try
            {
                var key = @"SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedhost";
                using (var registryKey = Registry.LocalMachine.OpenSubKey(key))
                {
                    if (registryKey != null)
                    {
                        var version = registryKey.GetValue("Version") as string;
                        return version != null && version.StartsWith("8.");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error checking .NET installation: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return false;
        }

        private bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private void ElevateAndRestart(string argument)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Assembly.GetExecutingAssembly().Location,
                Arguments = argument,
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
                MessageBox.Show($"Failed to restart as admin: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void ShowCheckPage()
        {
            CheckDotNetPage.Visibility = Visibility.Visible;
            AskInstallPage.Visibility = Visibility.Hidden;
            InstallDotNetPage.Visibility = Visibility.Hidden;
            ConfirmDotNetPage.Visibility = Visibility.Hidden;
        }

        public void ShowAskInstallPage()
        {
            CheckDotNetPage.Visibility = Visibility.Hidden;
            AskInstallPage.Visibility = Visibility.Visible;
            NextButton.Visibility = Visibility.Visible;
            CancelButton.Visibility = Visibility.Visible;
        }

        public void ShowInstallPage()
        {
            AskInstallPage.Visibility = Visibility.Hidden;
            InstallDotNetPage.Visibility = Visibility.Visible;
            NextButton.Visibility = Visibility.Hidden;
            CancelButton.Visibility = Visibility.Hidden;
        }

        public void ShowConfirmationPage()
        {
            InstallDotNetPage.Visibility = Visibility.Hidden;
            ConfirmDotNetPage.Visibility = Visibility.Visible;
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
