using System.Collections.Generic;
using System.Linq;

namespace vrcosc_magicchatbox.Core.Osc;

/// <summary>
/// Friendly names for the UiKeys the OSC build reports. The keys are not always what the tile is
/// called - window activity feeds a provider called Window, component stats one called ComponentStat
/// - so this is written out rather than guessed at from the key.
/// </summary>
public static class OscProviderNames
{
    private static readonly IReadOnlyDictionary<string, string> Names =
        new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["Status"] = "Personal status",
            ["Window"] = "Window activity",
            ["HeartRate"] = "Heart rate",
            ["TrackerBattery"] = "VR gear battery",
            ["VrPerformance"] = "VR performance",
            ["ComponentStat"] = "Component stats",
            ["NetworkStatistics"] = "Network stats",
            ["Time"] = "Time",
            ["Weather"] = "Weather",
            ["Twitch"] = "Twitch",
            ["TikTokLive"] = "TikTok",
            ["Discord"] = "Discord",
            ["VrcRadar"] = "VRChat radar",
            ["Soundpad"] = "Soundpad",
            ["Spotify"] = "Spotify",
            ["MediaLink"] = "MediaLink",
            ["Lyrics"] = "Lyrics",
        };

    public static string Describe(string key)
        => Names.TryGetValue(key, out var name) ? name : key;

    /// <summary>Whether a key has a name here. Some names equal their key, so Describe cannot tell you.</summary>
    public static bool IsKnown(string key) => Names.ContainsKey(key);

    /// <summary>
    /// "Discord", "Discord and Weather", "Discord, Weather and Time". Reads as a sentence because it
    /// sits in one.
    /// </summary>
    public static string DescribeList(IEnumerable<string>? keys)
    {
        var names = (keys ?? Enumerable.Empty<string>()).Select(Describe).ToList();

        return names.Count switch
        {
            0 => string.Empty,
            1 => names[0],
            _ => string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1],
        };
    }
}
