using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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
        private string backupPath;

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
                var settingsJson = File.ReadAllText(jsonFilePath);

                if (string.IsNullOrWhiteSpace(settingsJson) || settingsJson.All(c => c == '\0'))
                {
                    Logging.WriteInfo("The app_location.json file is empty or corrupted.");
                    SetDefaultPaths();
                }
                else
                {
                    try
                    {
                        JObject appLocation = JObject.Parse(settingsJson);
                        currentAppPath = appLocation["currentAppPath"].ToString();
                        tempPath = appLocation["tempPath"].ToString();
                        unzipPath = appLocation["unzipPath"].ToString();
                        magicChatboxExePath = appLocation["magicChatboxExePath"].ToString();
                        backupPath = Path.Combine(dataPath, "backup");
                    }
                    catch (Newtonsoft.Json.JsonReaderException ex)
                    {
                        Logging.WriteInfo($"Error parsing app_location.json: {ex.Message}");
                        SetDefaultPaths();
                    }
                }
            }
            else
            {
                SetDefaultPaths();
            }

            if (!Directory.Exists(tempPath))
            {
                Directory.CreateDirectory(tempPath);
            }
        }

        private void SetDefaultPaths()
        {
            currentAppPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            tempPath = Path.Combine(Path.GetTempPath(), "vrcosc_magicchatbox_update");
            unzipPath = Path.Combine(tempPath, "update_unzip");
            magicChatboxExePath = Path.Combine(unzipPath, "MagicChatbox.exe");
            backupPath = Path.Combine(dataPath, "backup");
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
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(currentAppPath, "MagicChatbox.exe"),
                UseShellExecute = true,
                WorkingDirectory = currentAppPath
            };
            Process.Start(startInfo);
            Environment.Exit(0);
        }


        private void ExtractCustomZip(string zipPath)
        {
            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(zipPath))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        // Build the full path for the entry's extraction
                        string destinationPath = Path.Combine(unzipPath, entry.FullName);

                        // If the entry is a directory (ends with '/'), only create the directory
                        if (string.IsNullOrEmpty(entry.Name))
                        {
                            Directory.CreateDirectory(destinationPath);
                        }
                        else
                        {
                            // Ensure the directory for this file exists
                            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                            // Extract the file
                            entry.ExtractToFile(destinationPath, true); // Overwrites the file if it already exists
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions (e.g., logging, user notification)
                Logging.WriteException(ex, MSGBox: true);
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

                // Create a backup before updating
                if (!Directory.Exists(backupPath))
                {
                    UpdateStatus("Creating backup directory");
                    Directory.CreateDirectory(backupPath);
                }
                else
                {
                    // Clear previous backup
                    UpdateStatus("Clearing previous backup");
                    Directory.Delete(backupPath, true);
                    Directory.CreateDirectory(backupPath);
                }
                UpdateStatus("Creating backup");
                CopyDirectory(new DirectoryInfo(currentAppPath), new DirectoryInfo(backupPath));

                // Update app_location.json with backupPath information
                SaveUpdateLocation(backupPath: backupPath); // Adjust SaveUpdateLocation method accordingly

                if (!useCustomZip)
                {
                    UpdateStatus("Requesting update");
                    string zipPath = Path.Combine(tempPath, "update.zip");
                    await DownloadAndExtractUpdate(zipPath);
                }
                else
                {
                    UpdateStatus("Extracting custom ZIP");
                    ExtractCustomZip(customZipPath);
                }

                // Start the updated application with a specific argument
                StartNewApplication("-update",unzipPath);
            }
            catch (Exception ex)
            {
                UpdateStatus("Update failed.");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Logging.WriteException(ex, MSGBox: true);
                });
            }
        }

        private void StartNewApplication(string argument, string Directory)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Directory, "MagicChatbox.exe"),
                Arguments = argument,
                UseShellExecute = true,
                WorkingDirectory = Directory
            };
            Process.Start(startInfo);
            Environment.Exit(0);
        }

        public void RollbackApplication(StartUp startUp)
        {
            UpdateStatus("Rolling back to previous version", startUp, 25);
            string jsonFilePath = Path.Combine(dataPath, "app_location.json");
            if (File.Exists(jsonFilePath))
            {
                UpdateStatus("Backup information found", startUp, 50);
                JObject appLocation = JObject.Parse(File.ReadAllText(jsonFilePath));
                string backupPath = appLocation["backupPath"].ToString();

                if (!Directory.Exists(backupPath))
                {
                    UpdateStatus("Backup directory not found. Rollback cannot proceed.", startUp);
                    Thread.Sleep(2000);
                    return;
                }


                UpdateStatus("Clearing current app path", startUp, 75);
                // Ensure the current app path is cleared before restoring from backup
                Directory.Delete(currentAppPath, true);
                Directory.CreateDirectory(currentAppPath);

                // Restore from backup
                UpdateStatus("Restoring from backup", startUp, 90);
                CopyDirectory(new DirectoryInfo(backupPath), new DirectoryInfo(currentAppPath));

                // Start the application normally after rollback
                UpdateStatus("Starting application", startUp, 100);
                Thread.Sleep(500);
                StartNewApplication("-clearbackup", currentAppPath);
            }
            else
            {
                UpdateStatus("Backup information not found. Rollback cannot proceed.", startUp);
                Thread.Sleep(2000);
            }
        }

        private void CopyDirectory(DirectoryInfo source, DirectoryInfo target)
        {
            Directory.CreateDirectory(target.FullName);

            // Copy each file into the new directory
            foreach (FileInfo fileInfo in source.GetFiles())
            {
                fileInfo.CopyTo(Path.Combine(target.FullName, fileInfo.Name), true);
            }

            // Copy each subdirectory using recursion
            foreach (DirectoryInfo subDirectory in source.GetDirectories())
            {
                DirectoryInfo nextTargetSubDir = target.CreateSubdirectory(subDirectory.Name);
                CopyDirectory(subDirectory, nextTargetSubDir);
            }
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

        public void StartRollback()
        {
            if (CheckIfBackupExists())
            {
                StartNewApplication("-rollback", backupPath);
            }

        }
        public bool CheckIfBackupExists()
        {
            string jsonFilePath = Path.Combine(dataPath, "app_location.json");
            if (File.Exists(jsonFilePath))
            {
               if(Directory.Exists(backupPath))
                {
                    string exePath = Path.Combine(backupPath, "MagicChatbox.exe");
                    if (File.Exists(exePath))
                    {
                        ViewModel.Instance.RollBackVersion = GetApplicationVersion(exePath);
                        return true;
                    }
                }
            }
            return false;
        }

        public Version GetApplicationVersion(string exePath)
        {

                FileVersionInfo fileInfo = FileVersionInfo.GetVersionInfo(exePath);
                if (Version.TryParse(fileInfo.FileVersion, out Version version))
                {
                    return version;
                }
            return null;
        }

        public void ClearBackUp()
        {
            if (Directory.Exists(backupPath))
            {
                Directory.Delete(backupPath, true);
            }
        }


        private void SaveUpdateLocation(string backupPath = null)
        {
            JObject appLocation = new JObject(
                new JProperty("currentAppPath", Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)),
                new JProperty("tempPath", tempPath),
                new JProperty("unzipPath", unzipPath),
                new JProperty("magicChatboxExePath", magicChatboxExePath)
            );

            if (backupPath != null)
            {
                appLocation.Add(new JProperty("backupPath", backupPath));
            }

            string jsonFilePath = Path.Combine(dataPath, "app_location.json");
            File.WriteAllText(jsonFilePath, appLocation.ToString());
        }

        public static void UpdateStatus(string message, StartUp startUp = null, double proc = 50)
        {
            if (Application.Current != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (startUp != null)
                    startUp.UpdateProgress(message, proc);
                    else
                    {
                        ViewModel.Instance.UpdateStatustxt = message;
                    }
                });
            }
        }
    }
}
