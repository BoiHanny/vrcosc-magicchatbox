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
using vrcosc_magicchatbox.Core.Updates;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Classes.DataAndSecurity;

public class UpdateApp
{
    private static readonly SemaphoreSlim PrepareUpdateGate = new(1, 1);
    private static int _legacyWorkspacesChecked;
    private const string ExecutableName = "MagicChatbox.exe";
    private const int UpdateLocationMetadataVersion = 2;
    private string backupPath = string.Empty;
    private string currentAppPath = string.Empty;
    private readonly string dataPath;
    private string maintenanceRunnerPath = string.Empty;
    private string magicChatboxExePath = string.Empty;
    private string tempPath = string.Empty;
    private string unzipPath = string.Empty;
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

    private void CopyDirectoryContents(DirectoryInfo source, DirectoryInfo target, Action? fileCopied = null)
    {
        Directory.CreateDirectory(target.FullName);

        foreach (FileInfo fileInfo in source.GetFiles())
        {
            fileInfo.CopyTo(Path.Combine(target.FullName, fileInfo.Name), true);
            fileCopied?.Invoke();
        }

        foreach (DirectoryInfo subDirectory in source.GetDirectories())
        {
            DirectoryInfo nextTargetSubDir = new(Path.Combine(target.FullName, subDirectory.Name));
            CopyDirectoryContents(subDirectory, nextTargetSubDir, fileCopied);
        }
    }

    private void CopyDirectoryWithProgress(string sourcePath, string targetPath, string verb)
    {
        int total = Directory.Exists(sourcePath)
            ? Directory.GetFiles(sourcePath, "*", System.IO.SearchOption.AllDirectories).Length
            : 0;

        int copied = 0;
        var clock = Stopwatch.StartNew();
        var throttle = new ProgressThrottle();

        CopyDirectoryContents(new DirectoryInfo(sourcePath), new DirectoryInfo(targetPath), () =>
        {
            copied++;
            double percent = UpdateProgressState.PercentOf(copied, total);
            if (throttle.ShouldReport(clock.Elapsed, percent))
            {
                ReportProgress(percent, $"{verb} {copied} of {total} files");
            }
        });
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

    public string DataDirectory => dataPath;

    private UpdateProgressState Progress => _updateState.Progress;

    private void OnUi(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            _dispatcher.BeginInvoke(action);
        }
    }

    private void BeginProgress(string headline) => OnUi(() => Progress.Begin(headline));

    private void SetStep(UpdateStepKind kind, UpdateStepStatus status, string detail = "") =>
        OnUi(() => Progress.SetStep(kind, status, detail));

    private void ReportProgress(double percent, string detail) =>
        OnUi(() => Progress.Report(percent, detail));

    private void ReportIndeterminate(string detail) =>
        OnUi(() => Progress.ReportIndeterminate(detail));

    private void FailProgress(string detail) => OnUi(() => Progress.Fail(detail));

    private void CompleteProgress(string detail) => OnUi(() => Progress.Complete(detail));

    private static string GetWorkspaceRoot() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Vrcosc-MagicChatbox",
            "update");

    private void ResetUpdateWorkspacePaths()
    {
        tempPath = GetWorkspaceRoot();
        unzipPath = Path.Combine(tempPath, "update_unzip");
        maintenanceRunnerPath = Path.Combine(tempPath, "maintenance_runner");
        magicChatboxExePath = Path.Combine(unzipPath, ExecutableName);
    }

    private static void RemoveLegacyWorkspaces()
    {
        if (Interlocked.Exchange(ref _legacyWorkspacesChecked, 1) != 0)
        {
            return;
        }

        string[] legacyPaths =
        [
            Path.Combine(Path.GetTempPath(), "vrcosc_magicchatbox_update"),
            Path.Combine(Path.GetTempPath(), "vrcosc_magicchatbox_custom_update")
        ];

        foreach (string legacyPath in legacyPaths)
        {
            try
            {
                if (Directory.Exists(legacyPath))
                {
                    Directory.Delete(legacyPath, true);
                    Logging.WriteInfo($"Removed legacy update workspace: {legacyPath}");
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Logging.WriteInfo($"Could not remove legacy update workspace {legacyPath}: {ex.Message}");
            }
        }
    }

    private DigestVerificationResult VerifyDownloadedPackage(string zipPath, bool requireDigest)
    {
        SetStep(UpdateStepKind.Verify, UpdateStepStatus.Running, "Hashing the download");
        ReportIndeterminate("Checking the download against the checksum GitHub published");

        var clock = Stopwatch.StartNew();
        var throttle = new ProgressThrottle();

        DigestVerificationResult verification = ReleaseAssetDigest.Verify(
            _updateState.UpdateDigest,
            zipPath,
            (hashed, total) =>
            {
                double percent = UpdateProgressState.PercentOf(hashed, total);
                if (throttle.ShouldReport(clock.Elapsed, percent))
                {
                    ReportProgress(percent, $"Verifying {UpdateProgressState.DescribeBytes(hashed)} of {UpdateProgressState.DescribeBytes(total)}");
                }
            });

        switch (verification.Status)
        {
            case DigestVerificationStatus.Match:
                Logging.WriteInfo($"Update package verified against the published SHA-256 ({verification.Expected}).");
                SetStep(
                    UpdateStepKind.Verify,
                    UpdateStepStatus.Done,
                    $"Valid package from the developer · {ShortHash(verification.Actual)}");
                break;

            case DigestVerificationStatus.NotPublished:
                Logging.WriteInfo("No SHA-256 was published for this release asset, so the package could not be verified.");

                // Nobody is watching an unattended install land, so an unverifiable package is
                // refused outright rather than downgraded to a warning the user never reads.
                if (requireDigest)
                {
                    TryDeleteFile(zipPath);
                    SetStep(UpdateStepKind.Verify, UpdateStepStatus.Failed, "No checksum published for this release");
                    throw new InvalidOperationException(
                        "This release did not publish a checksum, so it cannot be installed without supervision. " +
                        "Install it from the update button instead.");
                }

                SetStep(
                    UpdateStepKind.Verify,
                    UpdateStepStatus.Warning,
                    "No checksum published for this release");
                break;

            case DigestVerificationStatus.Mismatch:
                TryDeleteFile(zipPath);
                SetStep(UpdateStepKind.Verify, UpdateStepStatus.Failed, "Checksum did not match");
                throw new InvalidOperationException(
                    "The downloaded update did not match the checksum published for it. " +
                    $"Expected {verification.Expected}, got {verification.Actual}. The download was discarded.");
        }

        return verification;
    }

    private static string ShortHash(string? sha256) =>
        string.IsNullOrWhiteSpace(sha256) ? string.Empty : sha256.Length >= 12 ? sha256[..12] : sha256;

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            Logging.WriteInfo($"Could not delete {path}: {ex.Message}");
        }
    }

    private async Task<DigestVerificationResult> DownloadAndExtractUpdate(string zipPath, bool requireDigest)
    {
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

        UpdateStatus("Downloading update");
        SetStep(UpdateStepKind.Download, UpdateStepStatus.Running);

        long? expectedBytes = response.Content.Headers.ContentLength;
        var downloadClock = Stopwatch.StartNew();
        var downloadThrottle = new ProgressThrottle();
        long received = 0;

        await using (var fs = new FileStream(zipPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        await using (var source = await response.Content.ReadAsStreamAsync())
        {
            byte[] buffer = new byte[81920];
            int read;

            while ((read = await source.ReadAsync(buffer)) > 0)
            {
                await fs.WriteAsync(buffer.AsMemory(0, read));
                received += read;

                double percent = UpdateProgressState.PercentOf(received, expectedBytes);
                if (downloadThrottle.ShouldReport(downloadClock.Elapsed, percent))
                {
                    string detail = UpdateProgressState.DescribeTransfer(received, expectedBytes, downloadClock.Elapsed);
                    if (expectedBytes is > 0)
                    {
                        ReportProgress(percent, detail);
                    }
                    else
                    {
                        ReportIndeterminate(detail);
                    }
                }
            }

            await fs.FlushAsync();
        }

        if (expectedBytes is > 0 && received != expectedBytes)
        {
            TryDeleteFile(zipPath);
            SetStep(UpdateStepKind.Download, UpdateStepStatus.Failed, "Download was cut short");
            throw new IOException(
                $"The download ended early: expected {expectedBytes} bytes, received {received}.");
        }

        SetStep(UpdateStepKind.Download, UpdateStepStatus.Done, UpdateProgressState.DescribeBytes(received));

        UpdateStatus("Verifying download");
        DigestVerificationResult verification = VerifyDownloadedPackage(zipPath, requireDigest);

        UpdateStatus("Unpacking update");
        SetStep(UpdateStepKind.Unpack, UpdateStepStatus.Running);

        string targetFullPath = Path.GetFullPath(unzipPath);
        using (ZipArchive archive = ZipFile.OpenRead(zipPath))
        {
            int entryCount = archive.Entries.Count;
            int extracted = 0;
            var unpackClock = Stopwatch.StartNew();
            var unpackThrottle = new ProgressThrottle();

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string destinationPath = Path.GetFullPath(Path.Combine(unzipPath, entry.FullName));

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
                    string? directory = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(directory))
                        Directory.CreateDirectory(directory);
                    entry.ExtractToFile(destinationPath, true);
                }

                extracted++;
                double percent = UpdateProgressState.PercentOf(extracted, entryCount);
                if (unpackThrottle.ShouldReport(unpackClock.Elapsed, percent))
                {
                    ReportProgress(percent, $"Unpacked {extracted} of {entryCount} files");
                }
            }

            SetStep(UpdateStepKind.Unpack, UpdateStepStatus.Done, $"{entryCount} files");
        }

        TryDeleteFile(zipPath);

        UpdateHandoff.Write(dataPath, new UpdateHandoffInfo(
            TargetVersion(),
            verification.Status,
            verification.Actual ?? string.Empty,
            IsRollback: false));

        return verification;
    }

    /// <summary>
    /// Hands a package that was staged earlier to the maintenance runner. Applying an update
    /// always replaces the running installation, so this only ever happens at a cold start,
    /// before the window is up and before anything is connected.
    /// </summary>
    public bool TryStartStagedInstall()
    {
        PendingUpdateInfo? pending = PendingUpdate.Read(dataPath);
        if (pending == null)
            return false;

        try
        {
            if (string.IsNullOrWhiteSpace(pending.StagedPath) ||
                !File.Exists(Path.Combine(pending.StagedPath, ExecutableName)))
            {
                Logging.WriteInfo("The staged update is missing its files; discarding it.");
                PendingUpdate.Clear(dataPath);
                return false;
            }

            if (Version.TryParse(_updateState.AppVersion?.VersionNumber, out Version? running) &&
                Version.TryParse(pending.Version, out Version? staged) &&
                staged <= running)
            {
                Logging.WriteInfo($"The staged update ({pending.Version}) is not newer than {running}; discarding it.");
                PendingUpdate.Clear(dataPath);
                return false;
            }

            magicChatboxExePath = Path.Combine(pending.StagedPath, ExecutableName);
            SaveUpdateLocation(backupPath);

            // Cleared before the handoff rather than after it: a package that fails to apply
            // must not be retried on every launch from here on.
            PendingUpdate.Clear(dataPath);

            Logging.WriteInfo($"Installing the update staged for {pending.Version}.");
            StartMaintenanceRunner("-update");
            return true;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            PendingUpdate.Clear(dataPath);
            return false;
        }
    }

    public void DiscardStagedUpdate()
    {
        PendingUpdate.Clear(dataPath);

        try
        {
            if (Directory.Exists(unzipPath))
            {
                ClearDirectoryContents(unzipPath);
                Directory.Delete(unzipPath, true);
            }
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            Logging.WriteInfo($"Could not clean up the staged update: {ex.Message}");
        }
    }

    private string TargetVersionOrDefault()
    {
        string version = TargetVersion();
        return string.IsNullOrWhiteSpace(version) ? "the latest release" : version;
    }

    private void EnsureRoomForUpdate()
    {
        long installSize = MeasureDirectory(currentAppPath);
        if (installSize <= 0)
            return;

        // The download, the unpacked copy and the maintenance runner all live in the workspace
        // at once, and the backup is a full second copy of the installation.
        RequireFreeSpace(tempPath, installSize * 3);
        RequireFreeSpace(backupPath, installSize);
    }

    private static long MeasureDirectory(string path)
    {
        try
        {
            if (!Directory.Exists(path))
                return 0;

            return new DirectoryInfo(path)
                .EnumerateFiles("*", System.IO.SearchOption.AllDirectories)
                .Sum(file => file.Length);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private static void RequireFreeSpace(string path, long requiredBytes)
    {
        long available;

        try
        {
            string? root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrWhiteSpace(root))
                return;

            available = new DriveInfo(root).AvailableFreeSpace;
        }
        catch (Exception ex) when (ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException)
        {
            return;
        }

        if (available >= requiredBytes)
            return;

        throw new IOException(
            $"Not enough free space on {Path.GetPathRoot(Path.GetFullPath(path))} to install the update. " +
            $"{UpdateProgressState.DescribeBytes(requiredBytes)} is needed and " +
            $"{UpdateProgressState.DescribeBytes(available)} is free.");
    }

    private string TargetVersion()
    {
        if (!string.IsNullOrWhiteSpace(_updateState.UpdateVersion))
            return _updateState.UpdateVersion;

        return _updateState.PendingUpdateChannel == UpdateChannel.PreRelease
            ? _updateState.PreReleaseVersion?.VersionNumber ?? string.Empty
            : _updateState.LatestReleaseVersion?.VersionNumber ?? string.Empty;
    }


    private void ExtractCustomZip(string zipPath)
    {
        string targetFullPath = Path.GetFullPath(unzipPath);
        using (ZipArchive archive = ZipFile.OpenRead(zipPath))
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string destinationPath = Path.GetFullPath(Path.Combine(unzipPath, entry.FullName));

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
                    string? directory = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(directory))
                        Directory.CreateDirectory(directory);
                    entry.ExtractToFile(destinationPath, true);
                }
            }
        }

        Logging.WriteInfo($"Extracted custom ZIP to: {unzipPath}");
    }

    private void HandleAccessIssues(bool admin, string relaunchArgument)
    {
        if (!admin && TryRelaunchElevated(relaunchArgument))
        {
            return;
        }

        Logging.WriteException(new Exception("Access denied while applying files. Try running MagicChatbox as administrator."), MSGBox: true, autoclose: true);
    }

    private static bool TryRelaunchElevated(string relaunchArgument)
    {
        try
        {
            string currentExePath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, ExecutableName);
            Process.Start(new ProcessStartInfo
            {
                FileName = currentExePath,
                Arguments = relaunchArgument,
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Path.GetDirectoryName(currentExePath)
            });
            Environment.Exit(0);
            return true;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return false;
        }
    }

    private void PrepareMaintenanceRunner()
    {
        string sourceDirectory = GetCurrentAppDirectory();
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
        string actualCurrentAppPath = GetCurrentAppDirectory();
        string defaultBackupPath = Path.Combine(dataPath, "backup");

        if (!Directory.Exists(dataPath))
        {
            Directory.CreateDirectory(dataPath);
            Logging.WriteInfo($"Created data directory at: {dataPath}");
        }

        SetDefaultPaths();
        RemoveLegacyWorkspaces();

        if (!createNewAppLocation && File.Exists(jsonFilePath))
        {
            try
            {
                string settingsJson = File.ReadAllText(jsonFilePath);

                if (string.IsNullOrWhiteSpace(settingsJson) || settingsJson.All(c => c == '\0'))
                {
                    Logging.WriteInfo("The app_location.json file is empty or corrupted.");
                    SetDefaultPaths();
                }
                else
                {
                    JObject appLocation = JObject.Parse(settingsJson);
                    currentAppPath = NormalizePathOrFallback(appLocation["currentAppPath"]?.ToString(), actualCurrentAppPath, requireExistingDirectory: true);
                    ResetUpdateWorkspacePaths();
                    backupPath = NormalizePathOrFallback(appLocation["backupPath"]?.ToString(), defaultBackupPath, requireExistingDirectory: true);
                }
            }
            catch (Exception ex) when (ex is Newtonsoft.Json.JsonReaderException || ex is IOException || ex is UnauthorizedAccessException)
            {
                Logging.WriteInfo($"Error reading app_location.json: {ex.Message}");
                SetDefaultPaths();
            }
        }

        try
        {
            SaveUpdateLocation();
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            Logging.WriteInfo($"Could not save app_location.json: {ex.Message}");
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


    private void SaveUpdateLocation(string? backupPath = null)
    {
        Directory.CreateDirectory(dataPath);

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
        string tempFilePath = jsonFilePath + ".tmp";
        File.WriteAllText(tempFilePath, appLocation.ToString());
        File.Move(tempFilePath, jsonFilePath, overwrite: true);
    }

    private void SetDefaultPaths()
    {
        currentAppPath = GetCurrentAppDirectory();
        ResetUpdateWorkspacePaths();
        backupPath = Path.Combine(dataPath, "backup");
    }

    private static string GetCurrentAppDirectory()
        => Path.GetFullPath(AppContext.BaseDirectory);

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
        if (!File.Exists(jsonFilePath))
        {
            return false;
        }

        BackupCheck check = BackupManifest.Verify(backupPath, ExecutableName);
        if (check.Integrity is BackupIntegrity.Missing or BackupIntegrity.Incomplete)
        {
            if (check.Integrity == BackupIntegrity.Incomplete)
            {
                Logging.WriteInfo($"Hiding the rollback option: {check.Description}");
            }

            return false;
        }

        Version? backupVersion = GetApplicationVersion(Path.Combine(backupPath, ExecutableName));
        if (backupVersion != null)
        {
            _updateState.RollBackVersion = backupVersion;
        }

        return true;
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

    public Version? GetApplicationVersion(string exePath)
    {
        try
        {
            if (!File.Exists(exePath))
            {
                return null;
            }

            FileVersionInfo fileInfo = FileVersionInfo.GetVersionInfo(exePath);
            if (Version.TryParse(fileInfo.FileVersion, out Version? version))
            {
                return version;
            }
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            Logging.WriteInfo($"Could not read the version of {exePath}: {ex.Message}");
        }

        return null;
    }

    public async Task PrepareUpdate(string? customZipPath = null, bool unattended = false)
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

            string targetVersion = useCustomZip
                ? "a hand-picked package"
                : TargetVersionOrDefault();

            BeginProgress(useCustomZip ? "Installing a custom package" : $"Updating to {targetVersion}");

            EnsureRoomForUpdate();

            UpdateStatus("Preparing backup directory");
            ReportIndeterminate("Backing up the current version so you can go back");
            ClearAndRecreateDirectory(backupPath, "Prepare backup directory");
            Logging.WriteInfo($"Prepared backup directory at: {backupPath}");

            UpdateStatus("Creating backup");
            CopyDirectoryWithProgress(currentAppPath, backupPath, "Backed up");
            BackupManifest.Write(backupPath, _updateState.AppVersion?.VersionNumber, DateTimeOffset.UtcNow);
            Logging.WriteInfo("Wrote the backup manifest.");

            SaveUpdateLocation(backupPath);
            Logging.WriteInfo("Saved update location with backupPath.");

            UpdateStatus("Preparing update workspace");
            ResetExtractionWorkspace();

            DigestVerificationResult verification = default;

            if (string.IsNullOrEmpty(customZipPath))
            {
                UpdateStatus("Requesting update");
                string zipPath = Path.Combine(tempPath, "update.zip");
                verification = await DownloadAndExtractUpdate(zipPath, requireDigest: unattended);
            }
            else
            {
                UpdateStatus("Extracting custom ZIP");
                SetStep(UpdateStepKind.Download, UpdateStepStatus.Done, "Local file");
                SetStep(UpdateStepKind.Verify, UpdateStepStatus.Warning, "Hand-picked package, nothing to check it against");
                SetStep(UpdateStepKind.Unpack, UpdateStepStatus.Running);
                ExtractCustomZip(customZipPath);
                SetStep(UpdateStepKind.Unpack, UpdateStepStatus.Done);
                UpdateHandoff.Write(dataPath, new UpdateHandoffInfo(
                    string.Empty,
                    DigestVerificationStatus.NotPublished,
                    string.Empty,
                    IsRollback: false));
            }

            string launchDirectory = ResolveApplicationDirectory(unzipPath);
            magicChatboxExePath = Path.Combine(launchDirectory, ExecutableName);
            SaveUpdateLocation(backupPath);

            if (unattended)
            {
                PendingUpdate.Write(dataPath, new PendingUpdateInfo(
                    TargetVersion(),
                    _updateState.PendingUpdateChannel ?? UpdateChannel.Stable,
                    launchDirectory,
                    verification.Actual ?? string.Empty,
                    DateTimeOffset.UtcNow));

                SetStep(UpdateStepKind.Install, UpdateStepStatus.Pending, "Waiting for the next restart");

                // Complete, not just a progress report: the card can only be dismissed once it has
                // finished or failed, and staging in the background is finished as far as this session
                // goes. Reporting progress and returning left the card on screen with a dead close
                // button for the rest of the session.
                CompleteProgress("Ready to install the next time MagicChatbox starts");
                Logging.WriteInfo($"Staged {TargetVersion()} for install on the next start.");
                return;
            }

            SetStep(UpdateStepKind.Install, UpdateStepStatus.Running, "Restarting to swap the files");
            ReportIndeterminate("Restarting to finish the install");
            StartMaintenanceRunner("-update");
        }
        catch (Exception ex)
        {
            UpdateStatus("Update failed.");
            FailProgress(ex.Message);
            _dispatcher.BeginInvoke(() =>
            {
                _updateState.CanUpdate = true;
                _updateState.CanUpdateLabel = true;

                // An update nobody asked for must not interrupt with a modal error. The progress card
                // already shows the failure, and the caller toasts. A hand-started update keeps the
                // dialog, because someone is waiting on the answer.
                Logging.WriteException(ex, MSGBox: !unattended);
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

            UpdateStatus("Checking the backup", startUp, 35);
            BackupCheck check = BackupManifest.Verify(rollbackSourcePath, ExecutableName);
            Logging.WriteInfo($"Rollback backup check: {check.Integrity} - {check.Description}");

            if (check.Integrity is BackupIntegrity.Missing or BackupIntegrity.Incomplete)
            {
                UpdateStatus($"{check.Description} Rollback cannot proceed.", startUp);
                Logging.WriteException(
                    new Exception($"Rollback stopped before touching your installation. {check.Description}"),
                    MSGBox: true,
                    autoclose: true);
                Thread.Sleep(Core.Constants.UpdateSleepDelayMs);
                return;
            }

            string? currentVersion = GetApplicationVersion(Path.Combine(currentAppPath, ExecutableName))?.ToString()
                ?? _updateState.AppVersion?.VersionNumber;

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
            bool backupRefreshSucceeded = false;
            try
            {
                ExecuteWithRetry(() => ClearDirectoryContents(currentAppPath), "Clear current app path");

                UpdateStatus("Restoring from backup", startUp, 90);
                CopyDirectory(new DirectoryInfo(rollbackSourcePath), new DirectoryInfo(currentAppPath));
                magicChatboxExePath = Path.Combine(currentAppPath, ExecutableName);

                UpdateStatus("Preserving rollback path", startUp, 95);
                ClearAndRecreateDirectory(backupPath, "Refresh backup directory after rollback");
                CopyDirectory(new DirectoryInfo(rollbackRecoveryPath), new DirectoryInfo(backupPath));
                BackupManifest.Write(backupPath, currentVersion, DateTimeOffset.UtcNow);
                SaveUpdateLocation(backupPath);
                backupRefreshSucceeded = true;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
            {
                if (!admin && TryRelaunchElevated("-rollbackadmin"))
                {
                    return;
                }

                UpdateStatus("Rollback failed, putting the version you were on back", startUp, 80);

                string failureMessage = DescribeUpdateFailure(ex).Replace("Update failed:", "Rollback failed:");
                if (TryRestoreFrom(rollbackRecoveryPath, "Restore the version rollback started from"))
                {
                    Logging.WriteException(
                        new Exception($"{failureMessage} The version you were on has been put back."),
                        MSGBox: true,
                        autoclose: true);
                    StartNewApplication();
                }
                else
                {
                    Logging.WriteException(new Exception(failureMessage), MSGBox: true, autoclose: true);
                }

                return;
            }
            finally
            {
                if (backupRefreshSucceeded)
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
            UpdateHandoff.Write(dataPath, new UpdateHandoffInfo(
                BackupManifest.ReadVersion(backupPath) ?? _updateState.RollBackVersion?.ToString() ?? string.Empty,
                DigestVerificationStatus.NotPublished,
                string.Empty,
                IsRollback: true));

            StartMaintenanceRunner("-rollback");
            return;
        }

        Logging.WriteException(new Exception("No rollback backup was found."), MSGBox: true);
    }

    public void UpdateApplication(bool admin = false, string? customZipPath = null)
    {
        if (!string.IsNullOrEmpty(customZipPath))
        {
            unzipPath = Path.Combine(GetWorkspaceRoot(), "custom_unzip");
            magicChatboxExePath = Path.Combine(unzipPath, ExecutableName);
            ResetExtractionWorkspace();
            ExtractCustomZip(customZipPath);
        }

        DirectoryInfo currentAppDirectory = new DirectoryInfo(currentAppPath);

        try
        {
            ExecuteWithRetry(() => ClearDirectoryContents(currentAppPath), "Replace current installation");
            string sourceRoot = ResolveApplicationDirectory(unzipPath);
            ExecuteWithRetry(() => CopyDirectoryContents(new DirectoryInfo(sourceRoot), currentAppDirectory), "Copy update files");
            magicChatboxExePath = Path.Combine(currentAppPath, ExecutableName);
            SaveUpdateLocation();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
        {
            if (!admin && TryRelaunchElevated("-updateadmin"))
            {
                return;
            }

            string failureMessage = DescribeUpdateFailure(ex);
            if (TryRestoreFromBackup())
            {
                Logging.WriteException(
                    new Exception($"{failureMessage} The previous version has been restored."),
                    MSGBox: true,
                    autoclose: true);
                StartNewApplication();
            }
            else
            {
                Logging.WriteException(new Exception(failureMessage), MSGBox: true, autoclose: true);
            }

            return;
        }

        StartNewApplication();
    }

    private bool TryRestoreFromBackup() => TryRestoreFrom(backupPath, "Restore previous version");

    private bool TryRestoreFrom(string sourcePath, string operationName)
    {
        try
        {
            string installRoot = Path.GetFullPath(currentAppPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string sourceRoot = Path.GetFullPath(sourcePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (sourceRoot.Equals(installRoot, StringComparison.OrdinalIgnoreCase) ||
                sourceRoot.StartsWith(installRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!Directory.Exists(sourcePath) || !File.Exists(Path.Combine(sourcePath, ExecutableName)))
            {
                return false;
            }

            ExecuteWithRetry(() => ClearDirectoryContents(currentAppPath), "Clear installation for restore");
            ExecuteWithRetry(
                () => CopyDirectoryContents(new DirectoryInfo(sourcePath), new DirectoryInfo(currentAppPath)),
                operationName);
            magicChatboxExePath = Path.Combine(currentAppPath, ExecutableName);
            return true;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return false;
        }
    }

    private static string DescribeUpdateFailure(Exception ex)
    {
        Exception root = ex;
        while (root.InnerException != null)
        {
            root = root.InnerException;
        }

        const int HrDiskFull = unchecked((int)0x80070070);
        const int HrHandleDiskFull = unchecked((int)0x80070027);
        const int HrSharingViolation = unchecked((int)0x80070020);
        const int HrLockViolation = unchecked((int)0x80070021);

        string reason = root switch
        {
            UnauthorizedAccessException => "access was denied while applying files. Try running MagicChatbox as administrator.",
            IOException { HResult: HrDiskFull or HrHandleDiskFull } => "the disk is full.",
            IOException { HResult: HrSharingViolation or HrLockViolation } => "a file is locked by another program (antivirus or a running MagicChatbox instance). Close it and try again.",
            FileNotFoundException or DirectoryNotFoundException => "the update package is incomplete or missing files.",
            _ => root.Message
        };

        return $"Update failed: {reason}";
    }

    public void UpdateStatus(string message, StartUp? startUp = null, double proc = 50)
    {
        _dispatcher.BeginInvoke(() =>
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
