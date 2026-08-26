using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace vrcosc_magicchatbox.Classes.Modules;

public sealed record IntegrationTile(
    string Key,
    string ElementName,
    string DisplayName,
    string MasterProperty,
    bool MasterLivesOnWeatherSettings,
    Func<IntegrationSettings, WeatherSettings, bool> IsMasterOn);

public static class IntegrationTileCatalog
{
    public static readonly IReadOnlyList<IntegrationTile> Tiles = new IntegrationTile[]
    {
        new("Status", "StatusItem", "Personal status",
            nameof(IntegrationSettings.IntgrStatus), false, (i, _) => i.IntgrStatus),

        new("Window", "WindowActivityItem", "Window activity",
            nameof(IntegrationSettings.IntgrScanWindowActivity), false, (i, _) => i.IntgrScanWindowActivity),

        new("HeartRate", "HeartRateItem", "Heart rate",
            nameof(IntegrationSettings.IntgrHeartRate), false, (i, _) => i.IntgrHeartRate),

        new("TrackerBattery", "TrackerBatteryItem", "VR gear battery",
            nameof(IntegrationSettings.IntgrTrackerBattery), false, (i, _) => i.IntgrTrackerBattery),

        new("VrPerformance", "VrPerformanceItem", "VR performance",
            nameof(IntegrationSettings.IntgrVrPerformance), false, (i, _) => i.IntgrVrPerformance),

        new("Component", "ComponentStatsItem", "Component stats",
            nameof(IntegrationSettings.IntgrComponentStats), false, (i, _) => i.IntgrComponentStats),

        new("Network", "NetworkStatsItem", "Network stats",
            nameof(IntegrationSettings.IntgrNetworkStatistics), false, (i, _) => i.IntgrNetworkStatistics),

        new("Time", "TimeItem", "Time",
            nameof(IntegrationSettings.IntgrScanWindowTime), false, (i, _) => i.IntgrScanWindowTime),

        new("Weather", "WeatherItem", "Weather",
            nameof(WeatherSettings.ShowWeatherInTime), true, (_, w) => w?.ShowWeatherInTime == true),

        new("Twitch", "TwitchItem", "Twitch",
            nameof(IntegrationSettings.IntgrTwitch), false, (i, _) => i.IntgrTwitch),

        new("TikTokLive", "TikTokLiveItem", "TikTok",
            nameof(IntegrationSettings.IntgrTikTokLive), false, (i, _) => i.IntgrTikTokLive),

        new("Discord", "DiscordItem", "Discord",
            nameof(IntegrationSettings.IntgrDiscord), false, (i, _) => i.IntgrDiscord),

        new("VrcRadar", "VrcRadarItem", "VRChat Radar",
            nameof(IntegrationSettings.IntgrVrcRadar), false, (i, _) => i.IntgrVrcRadar),

        new("Soundpad", "SoundpadItem", "Soundpad",
            nameof(IntegrationSettings.IntgrSoundpad), false, (i, _) => i.IntgrSoundpad),

        new("Voicemod", "VoicemodItem", "Voicemod",
            nameof(IntegrationSettings.IntgrVoicemod), false, (i, _) => i.IntgrVoicemod),

        new("Spotify", "SpotifyItem", "Spotify",
            nameof(IntegrationSettings.IntgrSpotify), false, (i, _) => i.IntgrSpotify),

        new("MediaLink", "MediaLinkItem", "Media link",
            nameof(IntegrationSettings.IntgrScanMediaLink), false, (i, _) => i.IntgrScanMediaLink),
    };

    public static IReadOnlyList<string> Keys { get; } = Tiles.Select(t => t.Key).ToList();

    private static readonly Dictionary<string, IntegrationTile> ByKey =
        Tiles.ToDictionary(t => t.Key, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, IntegrationTile> ByMasterProperty =
        Tiles.ToDictionary(t => t.MasterProperty, StringComparer.Ordinal);

    public static bool TryGet(string? key, [NotNullWhen(true)] out IntegrationTile? tile)
    {
        tile = null;
        return !string.IsNullOrWhiteSpace(key) && ByKey.TryGetValue(key.Trim(), out tile);
    }

    public static string DisplayNameFor(string key)
        => TryGet(key, out var tile) ? tile.DisplayName : key;

    public static bool TryKeyForMasterProperty(string propertyName, [NotNullWhen(true)] out string? key)
    {
        key = null;
        if (string.IsNullOrEmpty(propertyName)) return false;
        if (!ByMasterProperty.TryGetValue(propertyName, out var tile)) return false;
        key = tile.Key;
        return true;
    }

    public static HashSet<string> ResolveHidden(IEnumerable<string?>? stored)
    {
        var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (stored == null) return resolved;

        foreach (var raw in stored)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            if (TryGet(raw, out var tile))
                resolved.Add(tile.Key);
        }

        return resolved;
    }

    public static List<string> VisibleKeysInOrder(IEnumerable<string> orderedKeys, IEnumerable<string> hidden)
    {
        var hiddenSet = ResolveHidden(hidden);
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (var key in orderedKeys ?? Enumerable.Empty<string>())
        {
            if (!TryGet(key, out var tile)) continue;

            if (!used.Add(tile.Key)) continue;

            if (!hiddenSet.Contains(tile.Key))
                result.Add(tile.Key);
        }

        foreach (var tile in Tiles)
        {
            if (used.Contains(tile.Key)) continue;
            if (hiddenSet.Contains(tile.Key)) continue;
            result.Add(tile.Key);
        }

        return result;
    }

    public static bool IsMasterOn(string key, IntegrationSettings integrations, WeatherSettings weather)
        => TryGet(key, out var tile) && tile.IsMasterOn(integrations, weather);

    public static List<string> KeysWithMasterOff(
        IEnumerable<string> candidates, IntegrationSettings integrations, WeatherSettings weather)
    {
        var result = new List<string>();
        foreach (var key in candidates ?? Enumerable.Empty<string>())
        {
            if (!TryGet(key, out var tile)) continue;
            if (!tile.IsMasterOn(integrations, weather))
                result.Add(tile.Key);
        }
        return result;
    }
}
