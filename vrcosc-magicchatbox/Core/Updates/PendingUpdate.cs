using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.IO;

namespace vrcosc_magicchatbox.Core.Updates;

public sealed record PendingUpdateInfo(
    string Version,
    UpdateChannel Channel,
    string StagedPath,
    string Sha256,
    DateTimeOffset StagedAtUtc);

public static class PendingUpdate
{
    private const string FileName = "pending_update.json";

    public static string PathFor(string dataPath) => Path.Combine(dataPath, FileName);

    public static bool Write(string dataPath, PendingUpdateInfo info)
    {
        try
        {
            Directory.CreateDirectory(dataPath);

            var payload = new JObject(
                new JProperty("version", info.Version ?? string.Empty),
                new JProperty("channel", info.Channel.ToString()),
                new JProperty("stagedPath", info.StagedPath ?? string.Empty),
                new JProperty("sha256", info.Sha256 ?? string.Empty),
                new JProperty("stagedAtUtc", info.StagedAtUtc.ToString("o")));

            string path = PathFor(dataPath);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, payload.ToString());
            File.Move(temporary, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static PendingUpdateInfo? Read(string dataPath)
    {
        try
        {
            string path = PathFor(dataPath);
            if (!File.Exists(path))
                return null;

            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            // Date recognition is turned off so the timestamp survives as the text that was
            // written. Left on, it is converted to a local DateTime while parsing and comes back
            // out in whatever format the machine's culture prefers.
            using var reader = new JsonTextReader(new StringReader(json))
            {
                DateParseHandling = DateParseHandling.None,
            };

            JObject payload = JObject.Load(reader);

            if (!Enum.TryParse(payload.Value<string>("channel"), out UpdateChannel channel))
                channel = UpdateChannel.Stable;

            DateTimeOffset.TryParse(
                payload.Value<string>("stagedAtUtc"),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTimeOffset stagedAt);

            return new PendingUpdateInfo(
                payload.Value<string>("version") ?? string.Empty,
                channel,
                payload.Value<string>("stagedPath") ?? string.Empty,
                payload.Value<string>("sha256") ?? string.Empty,
                stagedAt);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
        {
            return null;
        }
    }

    public static void Clear(string dataPath)
    {
        try
        {
            string path = PathFor(dataPath);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
        }
    }
}
