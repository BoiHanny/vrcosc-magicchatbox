using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace vrcosc_magicchatbox.Core.Updates;

/// <summary>
/// Versions that were installed automatically and then failed to start. Without this the next
/// check would keep offering the build that was just rolled back.
/// </summary>
public static class UpdateBlocklist
{
    private const string FileName = "blocked_updates.json";
    private const int MaxEntries = 20;

    public static string PathFor(string dataPath) => Path.Combine(dataPath, FileName);

    public static IReadOnlyList<string> Read(string dataPath)
    {
        try
        {
            string path = PathFor(dataPath);
            if (!File.Exists(path))
                return [];

            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
                return [];

            return JArray.Parse(json)
                .Select(entry => entry.Value<string>())
                .OfType<string>()
                .Where(version => !string.IsNullOrWhiteSpace(version))
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
        {
            return [];
        }
    }

    public static IReadOnlyList<string> Add(string dataPath, string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return Read(dataPath);

        List<string> versions = Read(dataPath).ToList();

        if (versions.Any(existing => ReleaseVersion.Compare(existing, version) == 0))
            return versions;

        versions.Add(version);

        if (versions.Count > MaxEntries)
            versions.RemoveRange(0, versions.Count - MaxEntries);

        try
        {
            Directory.CreateDirectory(dataPath);

            string path = PathFor(dataPath);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, new JArray(versions).ToString());
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
        }

        return versions;
    }
}
