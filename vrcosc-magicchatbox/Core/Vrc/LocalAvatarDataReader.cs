using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace vrcosc_magicchatbox.Core.Vrc;

public readonly record struct LocalAvatarValue(string Name, double Value);

public sealed record LocalAvatarState(
    string AvatarId,
    double EyeHeight,
    bool LegacyFingers,
    IReadOnlyList<LocalAvatarValue> Values,
    DateTime SavedUtc)
{
    public int Count => Values.Count;

    public bool HasEyeHeight => EyeHeight > 0;
}

public sealed class LocalAvatarDataReader
{
    public const int MaxValuesPerAvatar = 4096;

    private readonly string _root;

    public LocalAvatarDataReader(string? root = null) => _root = root ?? DefaultRoot();

    public static string DefaultRoot() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "AppData", "LocalLow", "VRChat", "VRChat", "LocalAvatarData");

    public bool Exists => Directory.Exists(_root);

    public LocalAvatarState? TryRead(string avatarId)
    {
        if (string.IsNullOrWhiteSpace(avatarId) || !Directory.Exists(_root))
            return null;

        if (avatarId.AsSpan().ContainsAny('/', '\\', ':') || avatarId.Contains("..", StringComparison.Ordinal))
            return null;

        foreach (string userFolder in SafeEnumerateDirectories(_root))
        {
            string path = Path.Combine(userFolder, avatarId);

            if (File.Exists(path) && TryParse(path) is { } state)
                return state;
        }

        return null;
    }

    public IReadOnlyList<LocalAvatarState> ReadAll(int limit = int.MaxValue)
    {
        var states = new List<LocalAvatarState>();

        if (!Directory.Exists(_root))
            return states;

        foreach (string userFolder in SafeEnumerateDirectories(_root))
        {
            foreach (string path in SafeEnumerateFiles(userFolder))
            {
                if (states.Count >= limit)
                    return states;

                if (TryParse(path) is { } state)
                    states.Add(state);
            }
        }

        return states;
    }

    internal static LocalAvatarState? TryParse(string path)
    {
        try
        {
            string name = Path.GetFileName(path);

            if (!name.StartsWith("avtr_", StringComparison.OrdinalIgnoreCase))
                return null;

            byte[] bytes = File.ReadAllBytes(path);

            if (bytes.Length == 0)
                return null;

            var reader = new Utf8JsonReader(bytes);
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            JsonElement root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return null;

            double eyeHeight = root.TryGetProperty("eyeHeight", out JsonElement height)
                && height.ValueKind == JsonValueKind.Number
                && height.TryGetDouble(out double parsedHeight)
                    ? parsedHeight
                    : 0d;

            bool legacyFingers = root.TryGetProperty("legacyFingers", out JsonElement fingers)
                && fingers.ValueKind == JsonValueKind.True;

            var values = new List<LocalAvatarValue>();

            if (root.TryGetProperty("animationParameters", out JsonElement parameters)
                && parameters.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement entry in parameters.EnumerateArray())
                {
                    if (values.Count >= MaxValuesPerAvatar)
                        break;

                    if (entry.ValueKind != JsonValueKind.Object)
                        continue;

                    if (!entry.TryGetProperty("name", out JsonElement parameterName)
                        || parameterName.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    string? key = parameterName.GetString();

                    if (string.IsNullOrEmpty(key))
                        continue;

                    if (!entry.TryGetProperty("value", out JsonElement value)
                        || value.ValueKind != JsonValueKind.Number
                        || !value.TryGetDouble(out double parsed))
                    {
                        continue;
                    }

                    values.Add(new LocalAvatarValue(key, parsed));
                }
            }

            return new LocalAvatarState(
                name,
                eyeHeight,
                legacyFingers,
                values,
                File.GetLastWriteTimeUtc(path));
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string root)
    {
        try
        {
            return Directory.EnumerateDirectories(root).ToList();
        }
        catch (Exception)
        {
            return Array.Empty<string>();
        }
    }

    private static IEnumerable<string> SafeEnumerateFiles(string folder)
    {
        try
        {
            return Directory.EnumerateFiles(folder).ToList();
        }
        catch (Exception)
        {
            return Array.Empty<string>();
        }
    }
}
