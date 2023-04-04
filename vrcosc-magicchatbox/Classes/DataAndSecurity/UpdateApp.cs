using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using vrcosc_magicchatbox.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.DataAndSecurity
{
    public class UpdateApp
    {
        public UpdateApp() { }

        public void UpdateApplication()
        {
            string datap = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vrcosc-MagicChatbox");
            MessageBox.Show("I'm updating");
        }

        public static async void PrepareUpdate()
        {
            try
            {
                string tempPath = Path.Combine(Path.GetTempPath(), "vrcosc_magicchatbox_update");
                string zipPath = Path.Combine(tempPath, "update.zip");
                string unzipPath = Path.Combine(tempPath, "update_unzip");

                if (Directory.Exists(tempPath))
                {
                    Directory.Delete(tempPath, true);
                }

                // Ensure directories exist
                DataController.CreateIfMissing(tempPath);
                DataController.CreateIfMissing(unzipPath);

                // Download the zip file
                using (WebClient webClient = new WebClient())
                {
                    await webClient.DownloadFileTaskAsync(ViewModel.Instance.NewVersionURL, zipPath);
                }

                // Extract the contents of the zip file
                using (ZipArchive archive = ZipFile.OpenRead(zipPath))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string destinationPath = Path.Combine(unzipPath, entry.FullName);

                        if (entry.FullName.EndsWith("/")) // Check if it's a directory
                        {
                            string directoryPath = destinationPath.TrimEnd('/'); // Remove the trailing slash
                            DataController.CreateIfMissing(directoryPath);
                        }
                        else
                        {
                            string destinationDirectory = Path.GetDirectoryName(destinationPath);
                            entry.ExtractToFile(destinationPath, true);
                        }
                    }
                }

                // Create a JSON file with the location path of the current running app
                string currentAppPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string jsonFilePath = Path.Combine(ViewModel.Instance.DataPath, "app_location.json");

                using (StreamWriter sw = File.CreateText(jsonFilePath))
                {
                    JObject appLocation = new JObject(
                        new JProperty("currentAppPath", currentAppPath)
                    );
                    sw.Write(appLocation.ToString());
                }

                // Start MagicChatbox.exe with the -update argument
                string magicChatboxExePath = Path.Combine(unzipPath, "MagicChatbox.exe");
                ProcessStartInfo startInfo = new ProcessStartInfo(magicChatboxExePath)
                {
                    Arguments = "-update"
                };
                Process.Start(startInfo);

                // Close the current running MagicChatbox.exe
                System.Environment.Exit(0);
            }
            catch (Exception ex)
            {
                // Handle exceptions
                DataAndSecurity.Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
            }
        }

    }
}