using Microsoft.VisualBasic.FileIO;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Windows;
using vrcosc_magicchatbox.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.DataAndSecurity
{
    public class UpdateApp
    {
        public UpdateApp() { }

        public void UpdateApplication(bool admin = false)
        {
            string dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vrcosc-MagicChatbox");
            string jsonFilePath = Path.Combine(dataPath, "app_location.json");

            // Read the JSON file and retrieve the paths
            JObject appLocation = JObject.Parse(File.ReadAllText(jsonFilePath));
            string currentAppPath = appLocation["currentAppPath"].ToString();
            string tempPath = appLocation["tempPath"].ToString();
            string zipPath = appLocation["zipPath"].ToString();
            string unzipPath = appLocation["unzipPath"].ToString();
            string magicChatboxExePath = appLocation["magicChatboxExePath"].ToString();
            DirectoryInfo currentAppDirectory = new DirectoryInfo(currentAppPath);
            // Delete the content of currentAppPath


            try
            {
                // Move the content of currentAppPath to the Recycle Bin
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
                    if (admin == true)
                    {
                        MessageBoxResult result = MessageBox.Show($"Looks like you have MagicChatBox Running.\nPlease close MagicChatBox.\n\n'OK' to retry.\n'Cancel' to stop the update process.", $"MagicChatBox updater {ViewModel.Instance.AppVersion.VersionNumber} | Directory in Use", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                        if (result == MessageBoxResult.Cancel)
                        {
                            System.Environment.Exit(0);
                        }
                    }
                    if (admin == false)
                    {
                        MessageBoxResult result = MessageBox.Show($"Looks like the updater needs more rights.\n\n'OK' to request admin rights \n'Cancel' to stop the update process.", $"MagicChatBox updater {ViewModel.Instance.AppVersion.VersionNumber}", MessageBoxButton.OKCancel, MessageBoxImage.Information);
                        if (result == MessageBoxResult.Cancel)
                        {
                            System.Environment.Exit(0);
                        }
                    }

                    // If the user doesn't have the right to delete the content of currentAppPath, restart the application with administrator privileges and the -updateadmin argument
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = magicChatboxExePath,
                        UseShellExecute = true,
                        Verb = "runas",
                        Arguments = "-updateadmin",
                    };
                    try
                    {
                        Process.Start(startInfo);
                    }
                    catch
                    {
                        System.Environment.Exit(0);
                    }
                    System.Environment.Exit(0);
                }
                throw;
            }

            // Copy the content of unzipPath to currentAppPath
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
            // Start MagicChatbox.exe from currentAppPath without any arguments
            ProcessStartInfo startInfoNoArgs = new ProcessStartInfo
            {
                FileName = Path.Combine(currentAppPath, "MagicChatbox.exe"),
                UseShellExecute = true,
                WorkingDirectory = currentAppPath
            };
            Process.Start(startInfoNoArgs);
            System.Environment.Exit(0);
        }

        public static void UpdateStatus(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ViewModel.Instance.UpdateStatustxt = message;
            });
        }


        public static async void PrepareUpdate()
        {
            try
            {
                UpdateStatus($"Requesting update");
                string tempPath = Path.Combine(Path.GetTempPath(), "vrcosc_magicchatbox_update");
                string zipPath = Path.Combine(tempPath, "update.zip");
                string unzipPath = Path.Combine(tempPath, "update_unzip");

                UpdateStatus("Deleting old updates");
                if (Directory.Exists(tempPath))
                {
                    Directory.Delete(tempPath, true);
                }

                // Ensure directories exist
                UpdateStatus("Creating directories");
                DataController.CreateIfMissing(tempPath);
                DataController.CreateIfMissing(unzipPath);

                // Download the zip file
                UpdateStatus("Downloading update");
                using (WebClient webClient = new WebClient())
                {
                    await webClient.DownloadFileTaskAsync(ViewModel.Instance.UpdateURL, zipPath);

                }

                // Extract the contents of the zip file
                ViewModel.Instance.UpdateStatustxt = "Extracting update";
                using (ZipArchive archive = ZipFile.OpenRead(zipPath))
                {
                    int fileCount = archive.Entries.Count;
                    int prosessedFileCount = 0;
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string destinationPath = Path.Combine(unzipPath, entry.FullName);

                        // Check if it's a directory
                        if (string.IsNullOrEmpty(Path.GetFileName(entry.FullName)))
                        {
                            ViewModel.Instance.UpdateStatustxt = $"Creating directory {prosessedFileCount}/{fileCount}";
                            DataController.CreateIfMissing(destinationPath);
                            prosessedFileCount += 1;
                        }
                        else
                        {
                            ViewModel.Instance.UpdateStatustxt = $"Extracting file {prosessedFileCount}/{fileCount}";

                            // Ensure the destination directory exists
                            string destinationDirectory = Path.GetDirectoryName(destinationPath);
                            DataController.CreateIfMissing(destinationDirectory);

                            entry.ExtractToFile(destinationPath, true);
                            prosessedFileCount += 1;
                        }
                    }

                }

                // Create a JSON file with the location path of the current running app
                ViewModel.Instance.UpdateStatustxt = "Saving update location";
                string currentAppPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string jsonFilePath = Path.Combine(ViewModel.Instance.DataPath, "app_location.json");
                //currentAppPath = @"C:\\Users\\hanno\\source\\repos\\vrcosc-magicchatbox\\vrcosc-magicchatbox\\bin\\Debug\\net6.0-windows";
                string magicChatboxExePath = Path.Combine(unzipPath, "MagicChatbox.exe");
                using (StreamWriter sw = File.CreateText(jsonFilePath))
                {
                    JObject appLocation = new JObject(
                        new JProperty("currentAppPath", currentAppPath),
                        new JProperty("tempPath", tempPath),
                        new JProperty("zipPath", zipPath),
                        new JProperty("unzipPath", unzipPath),
                        new JProperty("magicChatboxExePath", magicChatboxExePath)
                    );
                    sw.Write(appLocation.ToString());
                }

                // Start MagicChatbox.exe with the -update argument
                UpdateStatus("Starting MagicChatbox update");
                Thread.Sleep(1000);
                ProcessStartInfo startInfo = new ProcessStartInfo(magicChatboxExePath)
                {
                    Arguments = "-update"
                };
                Process.Start(startInfo);
                MainWindow.FireExitSave();
                UpdateStatus("Exit");

                // Close the current running MagicChatbox.exe
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                // Handle exceptions
                UpdateStatus("Update failed, check logs");
                Logging.WriteException(ex, makeVMDump: true, MSGBox: false);
            }
        }

    }
}