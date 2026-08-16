using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace vrcosc_magicchatbox.Core.Vrc;

public readonly record struct AvatarConfigInfo(string Name, string Hash);

public sealed class AvatarConfigReader
{
    private readonly string _root;

    public AvatarConfigReader(string? root = null) => _root = root ?? DefaultRoot();

    public static string DefaultRoot() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "AppData", "LocalLow", "VRChat", "VRChat", "OSC");

    public AvatarConfigInfo? TryRead(string avatarId)
    {
        if (string.IsNullOrWhiteSpace(avatarId) || !Directory.Exists(_root))
        {
            return null;
        }

        if (avatarId.AsSpan().ContainsAny('/', '\\', ':') || avatarId.Contains(".."))
        {
            return null;
        }

        foreach (var userFolder in SafeEnumerateDirectories(_root))
        {
            var path = Path.Combine(userFolder, "Avatars", avatarId + ".json");
            if (!File.Exists(path))
            {
                continue;
            }

            if (TryParse(path) is { } info)
            {
                return info;
            }
        }

        return null;
    }

    internal static AvatarConfigInfo? TryParse(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);

            var span = bytes.AsSpan();
            if (span.Length >= 3 && span[0] == 0xEF && span[1] == 0xBB && span[2] == 0xBF)
            {
                span = span[3..];
            }

            using var document = JsonDocument.Parse(span.ToArray());
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var name = ReadString(root, "name");
            var hash = ReadString(root, "hash");

            return name.Length == 0 && hash.Length == 0 ? null : new AvatarConfigInfo(name, hash);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static string ReadString(JsonElement root, string property) =>
        root.TryGetProperty(property, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? string.Empty
            : string.Empty;

    private static IEnumerable<string> SafeEnumerateDirectories(string root)
    {
        try
        {
            return Directory.EnumerateDirectories(root, "usr_*");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
