using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace vrcosc_magicchatbox.Core.Updates;

public sealed record StartupHealth(
    bool StartInProgress,
    int ConsecutiveFailures,
    string AutoInstalledVersion)
{
    public static readonly StartupHealth Clean = new(false, 0, string.Empty);
}

public readonly record struct StartupHealthCheck(
    bool PreviousStartFailed,
    int ConsecutiveFailures,
    string AutoInstalledVersion)
{
    public bool WasAutoInstalled => !string.IsNullOrWhiteSpace(AutoInstalledVersion);
}

/// <summary>
/// Records whether the last launch ever reached a working state. An unattended install has no
/// human watching it land, so this is the only way a build that dies during startup can be
/// noticed and undone.
/// </summary>
public static class StartupHealthBeacon
{
    private const string FileName = "startup_health.json";

    public static string PathFor(string dataPath) => Path.Combine(dataPath, FileName);

    public static StartupHealth Read(string dataPath)
    {
        try
        {
            string path = PathFor(dataPath);
            if (!File.Exists(path))
                return StartupHealth.Clean;

            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
                return StartupHealth.Clean;

            JObject payload = JObject.Parse(json);

            return new StartupHealth(
                payload.Value<bool>("startInProgress"),
                payload.Value<int>("consecutiveFailures"),
                payload.Value<string>("autoInstalledVersion") ?? string.Empty);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
        {
            return StartupHealth.Clean;
        }
    }

    public static StartupHealthCheck MarkStarting(string dataPath)
    {
        StartupHealth previous = Read(dataPath);

        int failures = previous.StartInProgress ? previous.ConsecutiveFailures + 1 : 0;

        Save(dataPath, new StartupHealth(true, failures, previous.AutoInstalledVersion));

        return new StartupHealthCheck(previous.StartInProgress, failures, previous.AutoInstalledVersion);
    }

    public static void MarkHealthy(string dataPath)
        => Save(dataPath, StartupHealth.Clean);

    public static void RecordAutoInstall(string dataPath, string version)
    {
        StartupHealth previous = Read(dataPath);
        Save(dataPath, previous with { AutoInstalledVersion = version ?? string.Empty });
    }

    private static void Save(string dataPath, StartupHealth health)
    {
        try
        {
            Directory.CreateDirectory(dataPath);

            var payload = new JObject(
                new JProperty("startInProgress", health.StartInProgress),
                new JProperty("consecutiveFailures", health.ConsecutiveFailures),
                new JProperty("autoInstalledVersion", health.AutoInstalledVersion ?? string.Empty));

            // Written straight through to disk: the crash this beacon exists to catch would
            // otherwise take the record of it down with the process.
            using var stream = new FileStream(
                PathFor(dataPath),
                FileMode.Create,
                FileAccess.Write,
                FileShare.None);
            using var writer = new StreamWriter(stream);

            writer.Write(payload.ToString());
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
        }
    }
}
