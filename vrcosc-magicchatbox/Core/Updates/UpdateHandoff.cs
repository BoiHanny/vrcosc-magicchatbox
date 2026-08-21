using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace vrcosc_magicchatbox.Core.Updates;

public readonly record struct UpdateHandoffInfo(
    string TargetVersion,
    DigestVerificationStatus Integrity,
    string Sha256,
    bool IsRollback)
{
    public string ShortHash =>
        Sha256.Length >= 12 ? Sha256[..12] : Sha256;

    public string IntegrityLine => Integrity switch
    {
        DigestVerificationStatus.Match => "Valid package from the developer",
        DigestVerificationStatus.NotPublished => "No checksum published for this release",
        _ => "Integrity check failed"
    };
}

public static class UpdateHandoff
{
    public const string FileName = "update_handoff.json";

    public static void Write(string directory, UpdateHandoffInfo info)
    {
        Directory.CreateDirectory(directory);

        var payload = new JObject(
            new JProperty("targetVersion", info.TargetVersion),
            new JProperty("integrity", info.Integrity.ToString()),
            new JProperty("sha256", info.Sha256),
            new JProperty("isRollback", info.IsRollback));

        string path = Path.Combine(directory, FileName);
        string tempPath = path + ".tmp";
        File.WriteAllText(tempPath, payload.ToString());
        File.Move(tempPath, path, overwrite: true);
    }

    public static UpdateHandoffInfo? Read(string directory)
    {
        string path = Path.Combine(directory, FileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            JObject payload = JObject.Parse(File.ReadAllText(path));

            if (!Enum.TryParse(payload.Value<string>("integrity"), out DigestVerificationStatus integrity))
            {
                integrity = DigestVerificationStatus.NotPublished;
            }

            return new UpdateHandoffInfo(
                payload.Value<string>("targetVersion") ?? string.Empty,
                integrity,
                payload.Value<string>("sha256") ?? string.Empty,
                payload.Value<bool?>("isRollback") ?? false);
        }
        catch (Exception ex) when (ex is Newtonsoft.Json.JsonReaderException || ex is IOException)
        {
            return null;
        }
    }

    public static void Clear(string directory)
    {
        try
        {
            string path = Path.Combine(directory, FileName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
        }
    }
}
