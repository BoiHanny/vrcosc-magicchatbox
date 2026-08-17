using System;
using System.Collections.Generic;
using System.Linq;

namespace vrcosc_magicchatbox.Core.Vrc;

public sealed record SharedParameter(string Name, int AvatarCount, double MostCommonValue)
{
    public string Describe(int total) => total > 0
        ? $"on {AvatarCount} of your {total} avatars"
        : $"on {AvatarCount} avatars";
}

public static class AvatarLibraryIndex
{
    public const int DefaultMinimumAvatars = 3;

    public static IReadOnlyList<SharedParameter> Shared(
        IEnumerable<LocalAvatarState> states,
        int minimumAvatars = DefaultMinimumAvatars)
    {
        ArgumentNullException.ThrowIfNull(states);

        var byName = new Dictionary<string, Dictionary<double, int>>(StringComparer.Ordinal);

        foreach (LocalAvatarState state in states)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (LocalAvatarValue value in state.Values)
            {
                if (AvatarControlCatalog.IsVrchatOwned(value.Name))
                    continue;

                if (!seen.Add(value.Name))
                    continue;

                if (!byName.TryGetValue(value.Name, out Dictionary<double, int>? counts))
                {
                    counts = new Dictionary<double, int>();
                    byName[value.Name] = counts;
                }

                counts[value.Value] = counts.GetValueOrDefault(value.Value) + 1;
            }
        }

        var shared = new List<SharedParameter>();

        foreach (KeyValuePair<string, Dictionary<double, int>> entry in byName)
        {
            int avatars = entry.Value.Values.Sum();

            if (avatars < Math.Max(1, minimumAvatars))
                continue;

            double common = entry.Value
                .OrderByDescending(v => v.Value)
                .ThenBy(v => v.Key)
                .First()
                .Key;

            shared.Add(new SharedParameter(entry.Key, avatars, common));
        }

        return shared
            .OrderByDescending(s => s.AvatarCount)
            .ThenBy(s => s.Name, StringComparer.Ordinal)
            .ToList();
    }
}
