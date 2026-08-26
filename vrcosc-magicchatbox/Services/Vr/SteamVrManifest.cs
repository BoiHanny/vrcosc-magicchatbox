using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace vrcosc_magicchatbox.Services.Vr;

/// <summary>
/// The application manifest SteamVR reads to offer MagicChatbox as a startup app.
/// </summary>
/// <remarks>
/// SteamVR records the manifest by its absolute path and keys removal on that same path, and
/// registering one key from a second path leaves both entries in place. The file therefore has
/// a fixed home of its own and the executable path inside it is rewritten on every launch,
/// which keeps a portable copy that moves — or an update that replaces the installation — from
/// leaving SteamVR pointed at an executable that is no longer there.
/// </remarks>
public static class SteamVrManifest
{
    public const string AppKey = "boihanny.magicchatbox";
    public const string FileName = "magicchatbox.vrmanifest";

    public static string DirectoryFor(string localAppDataPath)
        => Path.Combine(localAppDataPath, "Vrcosc-MagicChatbox", "steamvr");

    public static string PathFor(string localAppDataPath)
        => Path.Combine(DirectoryFor(localAppDataPath), FileName);

    public static string Build(string executablePath, string launchArgument)
    {
        var strings = new JObject(
            new JProperty("en_us", new JObject(
                new JProperty("name", "MagicChatbox"),
                new JProperty("description", "Sends your status, music and stats to the VRChat chatbox."))));

        var application = new JObject(
            new JProperty("app_key", AppKey),
            new JProperty("launch_type", "binary"),
            new JProperty("binary_path_windows", executablePath ?? string.Empty),
            new JProperty("arguments", launchArgument ?? string.Empty),
            // Only overlay applications are eligible for auto-launch. The flag buys a place in
            // SteamVR's startup list; the process itself still attaches as a background app.
            new JProperty("is_dashboard_overlay", true),
            new JProperty("strings", strings));

        var manifest = new JObject(
            new JProperty("source", "builtin"),
            new JProperty("applications", new JArray(application)));

        return manifest.ToString();
    }

    public static bool TryWrite(string manifestPath, string executablePath, string launchArgument)
    {
        try
        {
            string? directory = Path.GetDirectoryName(manifestPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string contents = Build(executablePath, launchArgument);

            if (File.Exists(manifestPath) &&
                string.Equals(File.ReadAllText(manifestPath), contents, StringComparison.Ordinal))
                return true;

            string temporary = manifestPath + ".tmp";
            File.WriteAllText(temporary, contents);
            File.Move(temporary, manifestPath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static void Delete(string manifestPath)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(manifestPath) && File.Exists(manifestPath))
                File.Delete(manifestPath);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
        }
    }
}
