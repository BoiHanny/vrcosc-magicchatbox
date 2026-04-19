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
    private static readonly SemaphoreSlim PrepareUpdateGate = new(1, 1);
    private const string ExecutableName = "MagicChatbox.exe";
    private const int UpdateLocationMetadataVersion = 2;
    private string backupPath;
    private string currentAppPath;
    private readonly string dataPath;
    private string maintenanceRunnerPath;
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
        string sourceRoot = ResolveApplicationDirectory(unzipPath);
        CopyDirectoryContents(new DirectoryInfo(sourceRoot), currentAppDirectory);
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    private void CopyDirectory(DirectoryInfo source, DirectoryInfo target)
    {
        CopyDirectoryContents(source, target);
    }

    private void CopyDirectoryContents(DirectoryInfo source, DirectoryInfo target)
    {
        Directory.CreateDirectory(target.FullName);

        foreach (FileInfo fileInfo in source.GetFiles())
        {
            fileInfo.CopyTo(Path.Combine(target.FullName, fileInfo.Name), true);
        }

        foreach (DirectoryInfo subDirectory in source.GetDirectories())
        {
            DirectoryInfo nextTargetSubDir = new(Path.Combine(target.FullName, subDirectory.Name));
            CopyDirectoryContents(subDirectory, nextTargetSubDir);
        }
    }

    private static void NormalizeAttributes(DirectoryInfo directory)
    {
        foreach (FileInfo file in directory.GetFiles("*", System.IO.SearchOption.AllDirectories))
        {
            file.Attributes = FileAttributes.Normal;
        }

        foreach (DirectoryInfo subDirectory in directory.GetDirectories("*", System.IO.SearchOption.AllDirectories))
        {
            subDirectory.Attributes = FileAttributes.Normal;
        }
    }

    private static void ExecuteWithRetry(Action action, string operationName, int maxAttempts = 5, int delayMs = 500)
    {
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                lastException = ex;
                if (attempt == maxAttempts)
                {
                    break;
                }

                Thread.Sleep(delayMs);
            }
        }

        throw new IOException($"{operationName} failed after {maxAttempts} attempts.", lastException);
    }

    private void ClearDirectoryContents(string path)
    {
        DirectoryInfo directory = new(path);
        if (!directory.Exists)
        {
            directory.Create();
            return;
        }

        NormalizeAttributes(directory);

        foreach (FileInfo file in directory.GetFiles())
        {
            file.Attributes = FileAttributes.Normal;
            file.Delete();
        }

        foreach (DirectoryInfo subDirectory in directory.GetDirectories())
        {
            NormalizeAttributes(subDirectory);
            subDirectory.Delete(true);
        }
    }

    private static string NormalizePathOrFallback(string? storedPath, string fallbackPath, bool requireExistingDirectory = false)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
        {
            return fallbackPath;
        }

        try
        {
            string fullPath = Path.GetFullPath(storedPath);
            if (requireExistingDirectory && !Directory.Exists(fullPath))
            {
                return fallbackPath;
            }

            return fullPath;
        }
        catch
        {
            return fallbackPath;
        }
    }

    private static string NormalizeFilePathOrFallback(string? storedPath, string fallbackPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
        {
            return fallbackPath;
        }

        try
        {
            return Path.GetFullPath(storedPath);
        }
        catch
        {
            return fallbackPath;
        }
    }

    private static void ClearAndRecreateDirectory(string path, string operationName)
    {
        ExecuteWithRetry(() =>
        {
            if (Directory.Exists(path))
            {
                DirectoryInfo directory = new(path);
                NormalizeAttributes(directory);

                foreach (FileInfo file in directory.GetFiles("*", System.IO.SearchOption.AllDirectories))
                {
                    file.Attributes = FileAttributes.Normal;
                }

                foreach (DirectoryInfo subDirectory in directory.GetDirectories("*", System.IO.SearchOption.AllDirectories))
                {
                    subDirectory.Attributes = FileAttributes.Normal;
                }

                directory.Delete(true);
            }

            Directory.CreateDirectory(path);
        }, operationName);
    }

    private string ResolveApplicationDirectory(string extractedRoot)
    {
        string fullRoot = Path.GetFullPath(extractedRoot);
        string directExePath = Path.Combine(fullRoot, ExecutableName);
        if (File.Exists(directExePath))
        {
            return fullRoot;
        }

        string[] candidateDirectories = Directory
            .GetFiles(fullRoot, ExecutableName, System.IO.SearchOption.AllDirectories)
            .Select(Path.GetDirectoryName)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFullPath(path!))
            .Where(path =>
                path.Equals(fullRoot, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path.Count(c => c == Path.DirectorySeparatorChar))
            .ThenBy(path => path.Length)
            .ToArray();

        if (candidateDirectories.Length == 0)
        {
            throw new FileNotFoundException($"Could not find {ExecutableName} in extracted update package.");
        }

        return candidateDirectories[0];
    }

    private void SaveResolvedUpdateLocation(string? backupDirectory = null)
    {
        string resolvedAppDirectory = ResolveApplicationDirectory(unzipPath);
        magicChatboxExePath = Path.Combine(resolvedAppDirectory, ExecutableName);
        SaveUpdateLocation(backupDirectory);
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

        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        await using (var fs = new FileStream(zipPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await response.Content.CopyToAsync(fs);
            await fs.FlushAsync();
        }

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

    private void HandleAccessIssues(bool admin, string relaunchArgument)
    {
        if (!admin)
        {
            try
            {
                string currentExePath = Assembly.GetExecutingAssembly().Location;
                Process.Start(new ProcessStartInfo
                {
                    FileName = currentExePath,
                    Arguments = relaunchArgument,
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = Path.GetDirectoryName(currentExePath)
                });
                Environment.Exit(0);
                return;
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        Logging.WriteException(new Exception("Access denied while applying files. Try running MagicChatbox as administrator."), MSGBox: true, autoclose: true);
    }

    private void PrepareMaintenanceRunner()
    {
        string sourceDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        if (PathsEqual(sourceDirectory, maintenanceRunnerPath))
        {
            return;
        }

        ClearAndRecreateDirectory(maintenanceRunnerPath, "Prepare maintenance runner");
        CopyDirectoryContents(new DirectoryInfo(sourceDirectory), new DirectoryInfo(maintenanceRunnerPath));
    }

    private void InitializePaths(bool createNewAppLocation)
    {
        string jsonFilePath = Path.Combine(dataPath, "app_location.json");
        string actualCurrentAppPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        string defaultTempPath = Path.Combine(Path.GetTempPath(), "vrcosc_magicchatbox_update");
        string defaultUnzipPath = Path.Combine(defaultTempPath, "update_unzip");
        string defaultMaintenanceRunnerPath = Path.Combine(defaultTempPath, "maintenance_runner");
        string defaultExePath = Path.Combine(defaultUnzipPath, ExecutableName);
        string defaultBackupPath = Path.Combine(dataPath, "backup");

        if (!Directory.Exists(dataPath))
        {
            Directory.CreateDirectory(dataPath);
            Logging.WriteInfo($"Created data directory at: {dataPath}");
        }

        if (createNewAppLocation)
        {
            SetDefaultPaths();
            SaveUpdateLocation();
            return;
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
                    currentAppPath = NormalizePathOrFallback(appLocation["currentAppPath"]?.ToString(), actualCurrentAppPath, requireExistingDirectory: true);
                    tempPath = NormalizePathOrFallback(appLocation["tempPath"]?.ToString(), defaultTempPath);
                    unzipPath = NormalizePathOrFallback(appLocation["unzipPath"]?.ToString(), defaultUnzipPath);
                    maintenanceRunnerPath = NormalizePathOrFallback(appLocation["maintenanceRunnerPath"]?.ToString(), defaultMaintenanceRunnerPath);
                    magicChatboxExePath = NormalizeFilePathOrFallback(appLocation["magicChatboxExePath"]?.ToString(), defaultExePath);
                    backupPath = NormalizePathOrFallback(appLocation["backupPath"]?.ToString(), defaultBackupPath);
                }
                catch (Exception ex) when (ex is Newtonsoft.Json.JsonReaderException || ex is IOException || ex is UnauthorizedAccessException)
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

        if (!Directory.Exists(maintenanceRunnerPath))
        {
            Directory.CreateDirectory(maintenanceRunnerPath);
            Logging.WriteInfo($"Created maintenance runner directory at: {maintenanceRunnerPath}");
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
                HandleAccessIssues(admin, "-updateadmin");
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
            new JProperty("metadataVersion", UpdateLocationMetadataVersion),
            new JProperty("currentAppPath", currentAppPath),
            new JProperty("tempPath", tempPath),
            new JProperty("unzipPath", unzipPath),
            new JProperty("maintenanceRunnerPath", maintenanceRunnerPath),
            new JProperty("backupPath", backupPath ?? this.backupPath),
            new JProperty("magicChatboxExePath", magicChatboxExePath)
        );

        string jsonFilePath = Path.Combine(dataPath, "app_location.json");
        File.WriteAllText(jsonFilePath, appLocation.ToString());
    }

    private void SetDefaultPaths()
    {
        currentAppPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        tempPath = Path.Combine(Path.GetTempPath(), "vrcosc_magicchatbox_update");
        unzipPath = Path.Combine(tempPath, "update_unzip");
        maintenanceRunnerPath = Path.Combine(tempPath, "maintenance_runner");
        magicChatboxExePath = Path.Combine(unzipPath, ExecutableName);
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

        if (!Directory.Exists(maintenanceRunnerPath))
        {
            Directory.CreateDirectory(maintenanceRunnerPath);
            Logging.WriteInfo($"Created maintenance runner directory at: {maintenanceRunnerPath}");
        }

        if (!Directory.Exists(backupPath))
        {
            Directory.CreateDirectory(backupPath);
            Logging.WriteInfo($"Created backup directory at: {backupPath}");
        }
    }

    private void ResetExtractionWorkspace()
    {
        ExecuteWithRetry(() =>
        {
            Directory.CreateDirectory(tempPath);

            if (Directory.Exists(unzipPath))
            {
                ClearDirectoryContents(unzipPath);
                Directory.Delete(unzipPath, true);
            }

            Directory.CreateDirectory(unzipPath);
        }, "Prepare update workspace");
    }

    private void StartMaintenanceRunner(string argument)
    {
        PrepareMaintenanceRunner();
        SaveUpdateLocation();
        StartNewApplication(argument, maintenanceRunnerPath);
    }

    private void StartNewApplication()
    {
        string exePath = Path.GetFullPath(Path.Combine(currentAppPath, ExecutableName));
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
        string exePath = Path.GetFullPath(Path.Combine(Directory, ExecutableName));
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
                string exePath = Path.Combine(backupPath, ExecutableName);
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
        if (!Directory.Exists(backupPath))
            return;

        try
        {
            ExecuteWithRetry(() =>
            {
                ClearDirectoryContents(backupPath);
                Directory.Delete(backupPath, true);
            }, "Delete backup directory", maxAttempts: 10, delayMs: 750);
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"Delayed backup cleanup skipped: {ex.Message}");
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
        bool gateAcquired = false;
        try
        {
            gateAcquired = await PrepareUpdateGate.WaitAsync(0);
            if (!gateAcquired)
            {
                UpdateStatus("Update already in progress.");
                Logging.WriteInfo("Ignored duplicate update request because an update is already being prepared.");
                return;
            }

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

            SaveUpdateLocation(backupPath);
            Logging.WriteInfo("Saved update location with backupPath.");

            UpdateStatus("Preparing update workspace");
            ResetExtractionWorkspace();

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

            string launchDirectory = ResolveApplicationDirectory(unzipPath);
            magicChatboxExePath = Path.Combine(launchDirectory, ExecutableName);
            SaveUpdateLocation(backupPath);
            StartMaintenanceRunner("-update");
        }
        catch (Exception ex)
        {
            UpdateStatus("Update failed.");
            _dispatcher.Invoke(() =>
            {
                _updateState.CanUpdate = true;
                _updateState.CanUpdateLabel = true;
                Logging.WriteException(ex, MSGBox: true);
            });
        }
        finally
        {
            if (gateAcquired)
            {
                PrepareUpdateGate.Release();
            }
        }
    }

    public void RollbackApplication(StartUp startUp, bool admin = false)
    {
        UpdateStatus("Rolling back to previous version", startUp, 25);
        string jsonFilePath = Path.Combine(dataPath, "app_location.json");
        if (File.Exists(jsonFilePath))
        {
            UpdateStatus("Backup information found", startUp, 50);
            JObject appLocation = JObject.Parse(File.ReadAllText(jsonFilePath));
            string rollbackSourcePath = NormalizePathOrFallback(appLocation["backupPath"]?.ToString(), backupPath, requireExistingDirectory: true);

            if (!Directory.Exists(rollbackSourcePath))
            {
                UpdateStatus("Backup directory not found. Rollback cannot proceed.", startUp);
                Thread.Sleep(Core.Constants.UpdateSleepDelayMs);
                return;
            }

            string rollbackRecoveryPath = Path.Combine(dataPath, "rollback_recovery");
            UpdateStatus("Backing up current version", startUp, 60);
            try
            {
                ClearAndRecreateDirectory(rollbackRecoveryPath, "Prepare rollback recovery backup");
                CopyDirectory(new DirectoryInfo(currentAppPath), new DirectoryInfo(rollbackRecoveryPath));
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
            {
                HandleAccessIssues(admin, "-rollbackadmin");
                return;
            }

            UpdateStatus("Clearing current app path", startUp, 75);
            try
            {
                ExecuteWithRetry(() => ClearDirectoryContents(currentAppPath), "Clear current app path");

                UpdateStatus("Restoring from backup", startUp, 90);
                CopyDirectory(new DirectoryInfo(rollbackSourcePath), new DirectoryInfo(currentAppPath));
                magicChatboxExePath = Path.Combine(currentAppPath, ExecutableName);

                UpdateStatus("Preserving rollback path", startUp, 95);
                ClearAndRecreateDirectory(backupPath, "Refresh backup directory after rollback");
                CopyDirectory(new DirectoryInfo(rollbackRecoveryPath), new DirectoryInfo(backupPath));
                SaveUpdateLocation(backupPath);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
            {
                HandleAccessIssues(admin, "-rollbackadmin");
                return;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(rollbackRecoveryPath))
                    {
                        ClearDirectoryContents(rollbackRecoveryPath);
                        Directory.Delete(rollbackRecoveryPath, true);
                    }
                }
                catch (Exception ex)
                {
                    Logging.WriteInfo($"Rollback recovery cleanup skipped: {ex.Message}");
                }
            }

            UpdateStatus("Starting application", startUp, 100);
            Thread.Sleep(500);
            StartNewApplication();
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
            StartMaintenanceRunner("-rollback");
            return;
        }

        Logging.WriteException(new Exception("No rollback backup was found."), MSGBox: true);
    }

    public void UpdateApplication(bool admin = false, string customZipPath = null)
    {
        bool useCustomZip = !string.IsNullOrEmpty(customZipPath);

        if (useCustomZip)
        {
            unzipPath = Path.Combine(Path.GetTempPath(), "vrcosc_magicchatbox_custom_update");
            magicChatboxExePath = Path.Combine(unzipPath, ExecutableName);
            ResetExtractionWorkspace();
            ExtractCustomZip(customZipPath);
        }

        DirectoryInfo currentAppDirectory = new DirectoryInfo(currentAppPath);

        try
        {
            ExecuteWithRetry(() => ClearDirectoryContents(currentAppPath), "Replace current installation");
            string sourceRoot = ResolveApplicationDirectory(unzipPath);
            CopyDirectoryContents(new DirectoryInfo(sourceRoot), currentAppDirectory);
            magicChatboxExePath = Path.Combine(currentAppPath, ExecutableName);
            SaveUpdateLocation();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
        {
            HandleAccessIssues(admin, "-updateadmin");
            return;
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
