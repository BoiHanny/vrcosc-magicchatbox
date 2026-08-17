using System;
using System.Collections.Generic;
using System.Linq;

namespace vrcosc_magicchatbox.Core.Vrc;

public sealed record AvatarFeature(
    string Key,
    string DisplayName,
    IReadOnlyList<string> Gates)
{
    public IReadOnlyList<string> WrittenNames { get; } = AvatarParameterContract.Parameters
        .Where(p => p.Flow == AvatarParameterFlow.AppToAvatar && Gates.Contains(p.Gate, StringComparer.Ordinal))
        .Select(p => p.Name)
        .ToList();
}

public static class AvatarFeatureCatalog
{
    public const string HeartRateKey = "HeartRate";
    public const string DiscordKey = "Discord";
    public const string CameraFlashKey = "CameraFlash";

    public static readonly IReadOnlyList<AvatarFeature> Features = new AvatarFeature[]
    {
        new(HeartRateKey, "Heart rate",
        [
            "IntgrHeartRate_OSC",
            "IntgrHeartRate_OSC and not DisableLegacySupport",
            "SentMCBHeartrateInfo",
            "SentMCBHeartrateInfo and not SentMCBHeartrateInfoLegacy",
            "SentMCBHeartrateInfoLegacy",
            "BroadPrefabCompatibility",
        ]),

        new(DiscordKey, "Discord",
        [
            "DiscordSettings.SendMuteDeafenOsc",
            "DiscordSettings.SendVoiceStateOsc",
        ]),

        new(CameraFlashKey, "Camera flash",
        [
            "VrcLogSettings.SendCameraFlashOsc",
        ]),
    };

    public static IReadOnlyList<string> NamesFor(string key)
    {
        foreach (AvatarFeature feature in Features)
        {
            if (string.Equals(feature.Key, key, StringComparison.Ordinal))
                return feature.WrittenNames;
        }

        return Array.Empty<string>();
    }

    public static IReadOnlyList<string> UnclaimedGates()
    {
        var claimed = Features
            .SelectMany(f => f.Gates)
            .ToHashSet(StringComparer.Ordinal);

        return AvatarParameterContract.Parameters
            .Where(p => p.Flow == AvatarParameterFlow.AppToAvatar)
            .Select(p => p.Gate)
            .Where(g => !claimed.Contains(g))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(g => g, StringComparer.Ordinal)
            .ToList();
    }
}
