using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using vrcosc_magicchatbox.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.DataAndSecurity
{
    public class UpdateApp
    {
        private readonly string dataPath;
        private string currentAppPath;
        private string tempPath;
        private string unzipPath;
        private string magicChatboxExePath;

        public UpdateApp()
        {
            dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vrcosc-MagicChatbox");
            InitializePaths();
        }

        private void InitializePaths()
        {
            string jsonFilePath = Path.Combine(dataPath, "app_location.json");
            if (File.Exists(jsonFilePath))
            {
                JObject appLocation = JObject.Parse(File.ReadAllText(jsonFilePath));
                currentAppPath = appLocation["currentAppPath"].ToString();
                tempPath = appLocation["tempPath"].ToString();
                unzipPath = appLocation["unzipPath"].ToString();
                magicChatboxExePath = appLocation["magicChatboxExePath"].ToString();
            }
            else
            {
                currentAppPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                tempPath = Path.Combine(Path.GetTempPath(), "vrcosc_magicchatbox_update");
                unzipPath = Path.Combine(tempPath, "update_unzip");
                magicChatboxExePath = Path.Combine(unzipPath, "MagicChatbox.exe");
            }
        }

        public void UpdateApplication(bool admin = false, string customZipPath = null)
        {
            bool useCustomZip = !string.IsNullOrEmpty(customZipPath);
            DirectoryInfo currentAppDirectory = new DirectoryInfo(currentAppPath);

            if (useCustomZip)
            {
                unzipPath = Path.Combine(Path.GetTempPath(), "vrcosc_magicchatbox_custom_update");
                magicChatboxExePath = Path.Combine(unzipPath, "MagicChatbox.exe");
                ExtractCustomZip(customZipPath);
            }
            else
            {
                MoveToRecycleBin(currentAppDirectory, admin);
                CopyContentsFromUnzipPath(currentAppDirectory);
            }

            StartNewApplication();
        }

        private void HandleAccessIssues(bool admin)
        {
            Logging.WriteException(new Exception("Access denied, trying to run as admin"), MSGBox: true, autoclose:true);
        }

        private void MoveToRecycleBin(DirectoryInfo currentAppDirectory, bool admin)
        {
            try
            {
                foreach (FileInfo file in currentAppDirectory.GetFiles())
                {
                    FileSystem.DeleteFile(file.FullName, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                }
                foreach (DirectoryInfo dir in currentAppDirectory.GetDirectories())
                {
                    FileSystem.DeleteDirectory(dir.FullName, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                }
            }
            catch (Exception ex)
            {
                if (ex is UnauthorizedAccessException || ex is IOException)
                {
                    HandleAccessIssues(admin);
                }
                else
                {
                    throw;
                }
            }
        }

        private void CopyContentsFromUnzipPath(DirectoryInfo currentAppDirectory)
        {
            DirectoryInfo sourceDirectory = new DirectoryInfo(unzipPath);
            foreach (FileInfo file in sourceDirectory.GetFiles())
            {
                file.CopyTo(Path.Combine(currentAppPath, file.Name), true);
            }

            foreach (DirectoryInfo sourceSubDirectory in sourceDirectory.GetDirectories())
            {
                DirectoryInfo targetSubDirectory = currentAppDirectory.CreateSubdirectory(sourceSubDirectory.Name);
                foreach (FileInfo file in sourceSubDirectory.GetFiles())
                {
                    file.CopyTo(Path.Combine(targetSubDirectory.FullName, file.Name), true);
                }
            }
        }

        private void StartNewApplication()
        {
            ProcessStartInfo startInfoNoArgs = new ProcessStartInfo
            {
                FileName = Path.Combine(currentAppPath, "MagicChatbox.exe"),
                UseShellExecute = true,
                WorkingDirectory = currentAppPath
            };
            Process.Start(startInfoNoArgs);
            Environment.Exit(0);
        }

        private void ExtractCustomZip(string zipPath)
        {
            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    // Build the full path for the entry's extraction
                    string destinationPath = Path.Combine(unzipPath, entry.FullName);

                    // If the entry is a directory itself (ends in a slash), ensure it's created
                    if (entry.FullName.EndsWith("/"))
                    {
                        Directory.CreateDirectory(destinationPath);
                    }
                    else
                    {
                        // Ensure the directory for this file exists
                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                        entry.ExtractToFile(destinationPath, true);
                    }
                }
            }
        }

        public void SelectCustomZip()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "MagicChatbox ZIP file (*.zip)|*.zip", // Filter to allow only ZIP files
                Multiselect = false // Allow only one file to be selected
            };

            bool? result = openFileDialog.ShowDialog(); // Show the dialog and get the result

            if (result == true) // Check if a file was selected
            {
                string selectedFilePath = openFileDialog.FileName;
                if (File.Exists(selectedFilePath))
                {
                    PrepareUpdate(selectedFilePath);
                }
            }
        }

        public async void PrepareUpdate(string customZipPath = null)
        {
            try
            {
                bool useCustomZip = !string.IsNullOrEmpty(customZipPath);

                if (!useCustomZip)
                {
                    UpdateStatus("Requesting update");
                    string zipPath = Path.Combine(tempPath, "update.zip");
                    await DownloadAndExtractUpdate(zipPath);
                }
                else
                {
                    ExtractCustomZip(customZipPath);
                }

                // Update paths to reflect the actual running location and update locations
                currentAppPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                tempPath = Path.Combine(Path.GetTempPath(), "vrcosc_magicchatbox_update");
                unzipPath = Path.Combine(tempPath, "update_unzip");
                magicChatboxExePath = Path.Combine(unzipPath, "MagicChatbox.exe");

                // Create and save the app_location.json file
                SaveUpdateLocation();

                // Close the current application and start the update process
                CloseCurrentApplicationAndStartUpdate();
            }
            catch (Exception ex)
            {
                UpdateStatus("Update failed, check logs");
                Logging.WriteException(ex, MSGBox: false);
            }
        }


        private void CloseCurrentApplicationAndStartUpdate()
        {
            // Start the updated application
            ProcessStartInfo startInfo = new ProcessStartInfo(magicChatboxExePath)
            {
                Arguments = "-update",
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(magicChatboxExePath)
            };
            Process.Start(startInfo);


            Application.Current.Dispatcher.Invoke(() =>
            {
                Application.Current.Shutdown();
            });
        }

        private async Task DownloadAndExtractUpdate(string zipPath)
        {
            using (WebClient webClient = new WebClient())
            {
                await webClient.DownloadFileTaskAsync(ViewModel.Instance.UpdateURL, zipPath);
            }

            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    // Build the full path for the entry's extraction
                    string destinationPath = Path.Combine(unzipPath, entry.FullName);

                    // If the entry is a directory itself (ends in a slash), ensure it's created
                    if (entry.FullName.EndsWith("/"))
                    {
                        Directory.CreateDirectory(destinationPath);
                    }
                    else
                    {
                        // Ensure the directory for this file exists
                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                        entry.ExtractToFile(destinationPath, true);
                    }
                }
            }
        }

        private void SaveUpdateLocation()
        {
            JObject appLocation = new JObject(
                new JProperty("currentAppPath", currentAppPath),
                new JProperty("tempPath", tempPath),
                new JProperty("zipPath", Path.Combine(tempPath, "update.zip")),
                new JProperty("unzipPath", unzipPath),
                new JProperty("magicChatboxExePath", magicChatboxExePath)
            );

            string jsonFilePath = Path.Combine(dataPath, "app_location.json");
            File.WriteAllText(jsonFilePath, appLocation.ToString());
        }

        public static void UpdateStatus(string message)
        {
            if (Application.Current != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ViewModel.Instance.UpdateStatustxt = message;
                });
            }
        }
    }
}
