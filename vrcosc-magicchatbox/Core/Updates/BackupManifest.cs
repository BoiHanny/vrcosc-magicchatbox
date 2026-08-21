using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace vrcosc_magicchatbox.Core.Updates;

public enum BackupIntegrity
{
    Missing,
    Unreadable,
    Incomplete,
    Trusted
}

public readonly record struct BackupCheck(BackupIntegrity Integrity, string Description)
{
    public bool IsTrusted => Integrity == BackupIntegrity.Trusted;
}

public static class BackupManifest
{
    public const string FileName = "backup_manifest.json";
    private const int ManifestVersion = 1;

    public static (int FileCount, long TotalBytes) Measure(string directory)
    {
        var files = new DirectoryInfo(directory)
            .GetFiles("*", SearchOption.AllDirectories)
            .Where(file => !string.Equals(file.Name, FileName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return (files.Length, files.Sum(file => file.Length));
    }

    public static void Write(string directory, string? appVersion, DateTimeOffset createdUtc)
    {
        (int fileCount, long totalBytes) = Measure(directory);

        var manifest = new JObject(
            new JProperty("manifestVersion", ManifestVersion),
            new JProperty("fileCount", fileCount),
            new JProperty("totalBytes", totalBytes),
            new JProperty("appVersion", appVersion ?? string.Empty),
            new JProperty("createdUtc", createdUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)));

        string path = Path.Combine(directory, FileName);
        string tempPath = path + ".tmp";
        File.WriteAllText(tempPath, manifest.ToString());
        File.Move(tempPath, path, overwrite: true);
    }

    public static BackupCheck Verify(string directory, string executableName)
    {
        if (!Directory.Exists(directory))
        {
            return new BackupCheck(BackupIntegrity.Missing, "No backup directory exists.");
        }

        if (!File.Exists(Path.Combine(directory, executableName)))
        {
            return new BackupCheck(BackupIntegrity.Missing, $"The backup does not contain {executableName}.");
        }

        string manifestPath = Path.Combine(directory, FileName);
        if (!File.Exists(manifestPath))
        {
            return new BackupCheck(
                BackupIntegrity.Unreadable,
                "The backup predates integrity manifests, so it cannot be checked before use.");
        }

        JObject manifest;
        try
        {
            manifest = JObject.Parse(File.ReadAllText(manifestPath));
        }
        catch (Exception ex) when (ex is Newtonsoft.Json.JsonReaderException || ex is IOException)
        {
            return new BackupCheck(BackupIntegrity.Unreadable, "The backup manifest could not be read.");
        }

        int? expectedFiles = manifest.Value<int?>("fileCount");
        long? expectedBytes = manifest.Value<long?>("totalBytes");

        if (expectedFiles is null || expectedBytes is null)
        {
            return new BackupCheck(BackupIntegrity.Unreadable, "The backup manifest is missing its file totals.");
        }

        (int actualFiles, long actualBytes) = Measure(directory);

        if (actualFiles != expectedFiles || actualBytes != expectedBytes)
        {
            return new BackupCheck(
                BackupIntegrity.Incomplete,
                $"The backup is incomplete: expected {expectedFiles} files totalling {expectedBytes} bytes, found {actualFiles} totalling {actualBytes}.");
        }

        string version = manifest.Value<string>("appVersion") ?? string.Empty;
        return new BackupCheck(
            BackupIntegrity.Trusted,
            string.IsNullOrWhiteSpace(version)
                ? $"Backup verified: {actualFiles} files."
                : $"Backup of {version} verified: {actualFiles} files.");
    }

    public static string? ReadVersion(string directory)
    {
        string manifestPath = Path.Combine(directory, FileName);
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            string version = JObject.Parse(File.ReadAllText(manifestPath)).Value<string>("appVersion") ?? string.Empty;
            return string.IsNullOrWhiteSpace(version) ? null : version;
        }
        catch (Exception ex) when (ex is Newtonsoft.Json.JsonReaderException || ex is IOException)
        {
            return null;
        }
    }
}
