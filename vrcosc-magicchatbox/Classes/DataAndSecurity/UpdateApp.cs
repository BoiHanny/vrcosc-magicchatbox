using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Classes.DataAndSecurity;

/// <summary>
/// Handles application updates and rollbacks: downloading releases from GitHub,
/// extracting ZIPs with path-traversal protection, backing up the current installation,
/// and restarting into the updated or rolled-back version.
/// </summary>
public class UpdateApp
{
    private string backupPath;
    private string currentAppPath;
    private readonly string dataPath;
    private string magicChatboxExePath;
    private string tempPath;
    private string unzipPath;
    private readonly AppUpdateState _updateState;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IUiDispatcher _dispatcher;

    public UpdateApp(AppUpdateState updateState, IHttpClientFactory httpClientFactory, IUiDispatcher dispatcher, bool createNewAppLocation = false)
    {
        _updateState = updateState;
        _httpClientFactory = httpClientFactory;
        _dispatcher = dispatcher;
        dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vrcosc-MagicChatbox");
        InitializePaths(createNewAppLocation);
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

    private void CopyDirectory(DirectoryInfo source, DirectoryInfo target)
    {
        Directory.CreateDirectory(target.FullName);

        foreach (FileInfo fileInfo in source.GetFiles())
        {
            fileInfo.CopyTo(Path.Combine(target.FullName, fileInfo.Name), true);
        }

        foreach (DirectoryInfo subDirectory in source.GetDirectories())
        {
            DirectoryInfo nextTargetSubDir = target.CreateSubdirectory(subDirectory.Name);
            CopyDirectory(subDirectory, nextTargetSubDir);
        }
    }


    private async Task DownloadAndExtractUpdate(string zipPath)
    {
        // Validate update URL against allowed patterns
        string updateUrl = _updateState.UpdateURL;
        if (string.IsNullOrWhiteSpace(updateUrl) ||
            !Uri.TryCreate(updateUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != "https" ||
            !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Untrusted update URL rejected: {updateUrl}");
        }

        var httpClient = _httpClientFactory.CreateClient("GitHub");
        httpClient.Timeout = TimeSpan.FromMinutes(5);
        using var response = await httpClient.GetAsync(updateUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        using var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await response.Content.CopyToAsync(fs);

        string targetFullPath = Path.GetFullPath(unzipPath);
        using (ZipArchive archive = ZipFile.OpenRead(zipPath))
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string destinationPath = Path.GetFullPath(Path.Combine(unzipPath, entry.FullName));

                // Prevent path traversal: ensure extracted path stays within target
                if (!destinationPath.StartsWith(targetFullPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    && !destinationPath.Equals(targetFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Zip entry path traversal blocked: {entry.FullName}");
                }

                if (entry.FullName.EndsWith("/"))
                {
                    Directory.CreateDirectory(destinationPath);
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                    entry.ExtractToFile(destinationPath, true);
                }
            }
        }
    }


    private void ExtractCustomZip(string zipPath)
    {
        try
        {
            string targetFullPath = Path.GetFullPath(unzipPath);
            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string destinationPath = Path.GetFullPath(Path.Combine(unzipPath, entry.FullName));

                    // Prevent path traversal: ensure extracted path stays within target
                    if (!destinationPath.StartsWith(targetFullPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                        && !destinationPath.Equals(targetFullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException($"Zip entry path traversal blocked: {entry.FullName}");
                    }

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(destinationPath);
                    }
                    else
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                        entry.ExtractToFile(destinationPath, true);
                    }
                }
            }
            Logging.WriteInfo($"Extracted custom ZIP to: {unzipPath}");
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: true);
        }
    }

    private void HandleAccessIssues(bool admin)
    {
        Logging.WriteException(new Exception("Access denied, trying to run as admin"), MSGBox: true, autoclose: true);
    }

    private void InitializePaths(bool createNewAppLocation)
    {
        string jsonFilePath = Path.Combine(dataPath, "app_location.json");
        string actualCurrentAppPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        if (!Directory.Exists(dataPath))
        {
            Directory.CreateDirectory(dataPath);
            Logging.WriteInfo($"Created data directory at: {dataPath}");
        }

        if (!File.Exists(jsonFilePath))
        {
            SetDefaultPaths();
            SaveUpdateLocation();
        }
        else
        {
            var settingsJson = File.ReadAllText(jsonFilePath);

            if (string.IsNullOrWhiteSpace(settingsJson) || settingsJson.All(c => c == '\0'))
            {
                Logging.WriteInfo("The app_location.json file is empty or corrupted.");
                SetDefaultPaths();
                SaveUpdateLocation();
            }
            else
            {
                try
                {
                    JObject appLocation = JObject.Parse(settingsJson);
                    currentAppPath = appLocation["currentAppPath"]?.ToString();

                    if (createNewAppLocation)
                    {
                        SetDefaultPaths();
                        SaveUpdateLocation();
                    }
                    else
                    {
                        tempPath = appLocation["tempPath"]?.ToString();
                        unzipPath = appLocation["unzipPath"]?.ToString();
                        magicChatboxExePath = appLocation["magicChatboxExePath"]?.ToString();
                        backupPath = Path.Combine(dataPath, "backup");
                    }
                }
                catch (Newtonsoft.Json.JsonReaderException ex)
                {
                    Logging.WriteInfo($"Error parsing app_location.json: {ex.Message}");
                    SetDefaultPaths();
                    SaveUpdateLocation();
                }
            }
        }

        if (!Directory.Exists(tempPath))
        {
            Directory.CreateDirectory(tempPath);
            Logging.WriteInfo($"Created temp directory at: {tempPath}");
        }

        if (!Directory.Exists(unzipPath))
        {
            Directory.CreateDirectory(unzipPath);
            Logging.WriteInfo($"Created unzip directory at: {unzipPath}");
        }

        if (!Directory.Exists(backupPath))
        {
            Directory.CreateDirectory(backupPath);
            Logging.WriteInfo($"Created backup directory at: {backupPath}");
        }
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


    private void SaveUpdateLocation(string backupPath = null)
    {
        JObject appLocation = new JObject(
            new JProperty("currentAppPath", currentAppPath),
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

    private void SetDefaultPaths()
    {
        currentAppPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        tempPath = Path.Combine(Path.GetTempPath(), "vrcosc_magicchatbox_update");
        unzipPath = Path.Combine(tempPath, "update_unzip");
        magicChatboxExePath = Path.Combine(unzipPath, "MagicChatbox.exe");
        backupPath = Path.Combine(dataPath, "backup");

        if (!Directory.Exists(tempPath))
        {
            Directory.CreateDirectory(tempPath);
            Logging.WriteInfo($"Created temp directory at: {tempPath}");
        }

        if (!Directory.Exists(unzipPath))
        {
            Directory.CreateDirectory(unzipPath);
            Logging.WriteInfo($"Created unzip directory at: {unzipPath}");
        }

        if (!Directory.Exists(backupPath))
        {
            Directory.CreateDirectory(backupPath);
            Logging.WriteInfo($"Created backup directory at: {backupPath}");
        }
    }

    private void StartNewApplication()
    {
        string exePath = Path.GetFullPath(Path.Combine(currentAppPath, "MagicChatbox.exe"));
        string appDir = Path.GetFullPath(currentAppPath);
        if (!exePath.StartsWith(appDir, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid application path detected.");

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = false,
            WorkingDirectory = currentAppPath
        };
        Process.Start(startInfo);
        Environment.Exit(0);
    }

    private void StartNewApplication(string argument, string Directory)
    {
        string exePath = Path.GetFullPath(Path.Combine(Directory, "MagicChatbox.exe"));
        string appDir = Path.GetFullPath(Directory);
        if (!exePath.StartsWith(appDir, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid application path detected.");

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = argument,
            UseShellExecute = false,
            WorkingDirectory = Directory
        };
        Process.Start(startInfo);
        Environment.Exit(0);
    }

    public bool CheckIfBackupExists()
    {
        string jsonFilePath = Path.Combine(dataPath, "app_location.json");
        if (File.Exists(jsonFilePath))
        {
            if (Directory.Exists(backupPath))
            {
                string exePath = Path.Combine(backupPath, "MagicChatbox.exe");
                if (File.Exists(exePath))
                {
                    _updateState.RollBackVersion = GetApplicationVersion(exePath);
                    return true;
                }
            }
        }
        return false;
    }

    public void ClearBackUp()
    {
        if (Directory.Exists(backupPath))
        {
            Directory.Delete(backupPath, true);
        }
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

    public async Task PrepareUpdate(string customZipPath = null)
    {
        try
        {
            bool useCustomZip = !string.IsNullOrEmpty(customZipPath);

            if (!Directory.Exists(backupPath))
            {
                UpdateStatus("Creating backup directory");
                Directory.CreateDirectory(backupPath);
                Logging.WriteInfo($"Created backup directory at: {backupPath}");
            }
            else
            {
                UpdateStatus("Clearing previous backup");
                Directory.Delete(backupPath, true);
                Directory.CreateDirectory(backupPath);
                Logging.WriteInfo($"Cleared and recreated backup directory at: {backupPath}");
            }
            UpdateStatus("Creating backup");
            CopyDirectory(new DirectoryInfo(currentAppPath), new DirectoryInfo(backupPath));

            SaveUpdateLocation(backupPath: backupPath);
            Logging.WriteInfo("Saved update location with backupPath.");

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

            StartNewApplication("-update", unzipPath);
        }
        catch (Exception ex)
        {
            UpdateStatus("Update failed.");
            _dispatcher.Invoke(() =>
            {
                Logging.WriteException(ex, MSGBox: true);
            });
        }
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
                Thread.Sleep(Core.Constants.UpdateSleepDelayMs);
                return;
            }


            UpdateStatus("Clearing current app path", startUp, 75);
            Directory.Delete(currentAppPath, true);
            Directory.CreateDirectory(currentAppPath);

            UpdateStatus("Restoring from backup", startUp, 90);
            CopyDirectory(new DirectoryInfo(backupPath), new DirectoryInfo(currentAppPath));

            UpdateStatus("Starting application", startUp, 100);
            Thread.Sleep(500);
            StartNewApplication("-clearbackup", currentAppPath);
        }
        else
        {
            UpdateStatus("Backup information not found. Rollback cannot proceed.", startUp);
            Thread.Sleep(Core.Constants.UpdateSleepDelayMs);
        }
    }


    public void SelectCustomZip()
    {
        OpenFileDialog openFileDialog = new OpenFileDialog
        {
            Filter = "MagicChatbox ZIP file (*.zip)|*.zip",
            Multiselect = false
        };

        bool? result = openFileDialog.ShowDialog();

        if (result == true)
        {
            string selectedFilePath = openFileDialog.FileName;
            if (File.Exists(selectedFilePath))
            {
                _ = PrepareUpdate(selectedFilePath);
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

    public void UpdateStatus(string message, StartUp startUp = null, double proc = 50)
    {
        _dispatcher.Invoke(() =>
        {
            if (startUp != null)
                startUp.UpdateProgress(message, proc);
            else
            {
                _updateState.UpdateStatustxt = message;
            }
        });
    }
}
