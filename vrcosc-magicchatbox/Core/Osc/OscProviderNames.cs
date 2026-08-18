using System.Collections.Generic;
using System.Linq;

namespace vrcosc_magicchatbox.Core.Osc;

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

    public static bool IsKnown(string key) => Names.ContainsKey(key);

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
