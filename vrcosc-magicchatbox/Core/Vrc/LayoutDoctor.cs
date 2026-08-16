using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace vrcosc_magicchatbox.Core.Vrc;

public enum LayoutState
{
    Unknown,
    NotInstalled,
    Installed,
    RenamedByVrcFury,
    RenamedByModularAvatar,
}

public sealed record LayoutReport(
    LayoutState State,
    int InstalledVersion,
    string Headline,
    string Detail,
    IReadOnlyList<string> MissingControls);

public static partial class LayoutDoctor
{
    public const string VersionPrefix = "MCB/Version/";

    [GeneratedRegex(@"^(?:VF\d+_)?MCB/Version/(\d+)$")]
    private static partial Regex VersionName();

    [GeneratedRegex(@"^VF\d+_MCB/")]
    private static partial Regex VrcFuryRenamed();

    [GeneratedRegex(@"^MCB/.*(?:\$\$Internal_\d+|\$[0-9A-Fa-f]{8,})$")]
    private static partial Regex ModularAvatarRenamed();

    public static LayoutReport Inspect(AvatarSchemaSnapshot schema, IEnumerable<string> expectedControls)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(expectedControls);

        var expected = expectedControls.ToList();

        if (schema.IsEmpty)
        {
            return new LayoutReport(
                LayoutState.Unknown, 0,
                "Waiting", "Waiting to see your avatar.", Array.Empty<string>());
        }

        var names = schema.Parameters.Select(p => p.Name).ToList();

        int version = 0;

        foreach (string name in names)
        {
            Match match = VersionName().Match(name);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int parsed))
                version = Math.Max(version, parsed);
        }

        var present = names.Select(EcosystemSignature.Normalize).ToHashSet(StringComparer.Ordinal);
        var missing = expected.Where(c => !present.Contains(c)).ToList();

        if (version > 0 && missing.Count == 0)
        {
            return new LayoutReport(
                LayoutState.Installed, version,
                "Ready",
                $"The MagicChatbox controls are on this avatar (version {version}).",
                Array.Empty<string>());
        }

        string? furyRenamed = names.FirstOrDefault(n => VrcFuryRenamed().IsMatch(n));

        if (furyRenamed != null && version == 0)
        {
            return new LayoutReport(
                LayoutState.RenamedByVrcFury, 0,
                "Renamed on install",
                $"VRCFury renamed the controls, so VRChat sees {furyRenamed} instead. "
                + "Set the Full Controller's Global Parameters to MCB/* and upload again.",
                missing);
        }

        string? maRenamed = names.FirstOrDefault(n => ModularAvatarRenamed().IsMatch(n));

        if (maRenamed != null && version == 0)
        {
            return new LayoutReport(
                LayoutState.RenamedByModularAvatar, 0,
                "Renamed on install",
                $"Modular Avatar renamed the controls, so VRChat sees {maRenamed} instead. "
                + "Turn off Auto Rename on the MA Parameters component and upload again.",
                missing);
        }

        if (version > 0)
        {
            return new LayoutReport(
                LayoutState.Installed, version,
                "Partly installed",
                $"Version {version} is on this avatar, but {missing.Count} control(s) are missing.",
                missing);
        }

        return new LayoutReport(
            LayoutState.NotInstalled, 0,
            "Not installed",
            "This avatar has no MagicChatbox controls. Most avatars do not — add them in Unity to control the app from your menu.",
            missing);
    }
}
