using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace vrcosc_magicchatbox.Core.Vrc;

public sealed record EcosystemMarker(string Prefix, string DisplayName, string Description);

public static partial class EcosystemSignature
{
    public static readonly IReadOnlyList<EcosystemMarker> Markers = new EcosystemMarker[]
    {
        new("Go/", "GoGo Loco", "Locomotion and poses."),
        new("FT/", "Face tracking", "Unified expressions from a face tracker."),
        new("OGB/", "OscGoesBrrr", "Contact senders and receivers."),
        new("OSCm/", "OSCmooth", "Smoothed copies of other parameters."),
        new("TPS_Internal/", "Poiyomi TPS", "Shader-driven contacts."),
        new("HBG/", "Heart rate prefab", "A third-party heart rate display."),
        new("VRCOSC/", "VRCOSC", "Parameters written by VRCOSC."),
        new("MCB/", "MagicChatbox", "Parameters this app owns."),
    };

    private static readonly IReadOnlyList<string> HeartRateHints =
    [
        "HBG/", "HR", "Heartrate", "HeartRate", "hr_",
    ];

    [GeneratedRegex(@"^VF\d+_")]
    private static partial Regex VrcFuryRename();

    [GeneratedRegex(@"\$\$Internal_\d+$|\$[0-9A-Fa-f]{8,}$")]
    private static partial Regex ModularAvatarRename();

    public static string Normalize(string name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        string stripped = VrcFuryRename().Replace(name, string.Empty);
        stripped = ModularAvatarRename().Replace(stripped, string.Empty);

        const string lastSynced = "/LastSynced";
        if (stripped.EndsWith(lastSynced, StringComparison.Ordinal))
            stripped = stripped[..^lastSynced.Length];

        return stripped;
    }

    public static bool WasRenamedByVrcFury(string name) => VrcFuryRename().IsMatch(name ?? string.Empty);

    public static bool WasRenamedByModularAvatar(string name) => ModularAvatarRename().IsMatch(name ?? string.Empty);

    public static IReadOnlyList<EcosystemMarker> Detect(IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);

        var found = new List<EcosystemMarker>();

        var normalized = names.Select(Normalize).ToList();

        foreach (EcosystemMarker marker in Markers)
        {
            if (normalized.Any(n => n.StartsWith(marker.Prefix, StringComparison.OrdinalIgnoreCase)))
                found.Add(marker);
        }

        return found;
    }

    public static string? FindHeartRateShape(IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);

        foreach (string raw in names)
        {
            string name = Normalize(raw);

            foreach (string hint in HeartRateHints)
            {
                if (!name.StartsWith(hint, StringComparison.OrdinalIgnoreCase))
                    continue;

                int slash = name.IndexOf('/');
                return slash > 0 ? name[..(slash + 1)] : name;
            }
        }

        return null;
    }
}
