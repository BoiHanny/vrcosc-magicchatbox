using System.Collections.Generic;
using System.Linq;

namespace vrcosc_magicchatbox.Classes.Modules.Afk;

public readonly record struct AfkStyleComposition(
    IReadOnlyList<AfkStyle> AllStyles,
    IReadOnlyList<AfkStyle> CustomStyles,
    string ActiveId);

public static class AfkStyleSeed
{
    public const string BuiltInIdPrefix = "builtin-";

    public static bool IsBuiltInId(string? id)
        => id != null && id.StartsWith(BuiltInIdPrefix, System.StringComparison.Ordinal);

    public static AfkStyleComposition Compose(
        IEnumerable<AfkStyle>? customStyles,
        IEnumerable<AfkStyle>? legacyStyles,
        string? activeId,
        string? legacyPrefix,
        bool legacyShowPrefix,
        string? legacyMessageWithTime,
        string? legacyMessageWithoutTime,
        bool legacyShowTime)
    {
        var mine = new List<AfkStyle>();

        foreach (var style in (customStyles ?? Enumerable.Empty<AfkStyle>()).Concat(
                              legacyStyles ?? Enumerable.Empty<AfkStyle>()))
        {
            if (IsBuiltInId(style.Id))
                continue;

            if (mine.Any(s => s.Id == style.Id))
                continue;

            style.IsBuiltIn = false;
            mine.Add(style);
        }

        AfkStyle? migrated = null;

        if (mine.Count == 0 && legacyStyles == null && !MatchesClassic(legacyPrefix, legacyMessageWithTime, legacyMessageWithoutTime))
        {
            migrated = new AfkStyle
            {
                Id = "yours",
                Name = "Yours",
                IsBuiltIn = false,
                ShowPrefix = legacyShowPrefix,
                Prefix = legacyPrefix ?? string.Empty,
                ShowTime = legacyShowTime,
                MessageWithTime = legacyMessageWithTime ?? string.Empty,
                MessageWithoutTime = legacyMessageWithoutTime ?? string.Empty,
            };

            mine.Add(migrated);
        }

        var all = new List<AfkStyle>(AfkStylePresets.Build());

        if (MatchesClassic(legacyPrefix, legacyMessageWithTime, legacyMessageWithoutTime))
        {
            var classic = all.First(s => s.Id == AfkStylePresets.ClassicId);
            classic.ShowPrefix = legacyShowPrefix;
            classic.ShowTime = legacyShowTime;
        }

        all.AddRange(mine);

        string resolved = !string.IsNullOrEmpty(activeId) && all.Any(s => s.Id == activeId)
            ? activeId!
            : migrated?.Id ?? AfkStylePresets.ClassicId;

        return new AfkStyleComposition(all, mine, resolved);
    }

    private static bool MatchesClassic(string? prefix, string? withTime, string? withoutTime)
        => (prefix ?? string.Empty) == AfkStylePresets.ClassicPrefix
           && (withTime ?? string.Empty) == AfkStylePresets.ClassicWithTime
           && (withoutTime ?? string.Empty) == AfkStylePresets.ClassicWithoutTime;

    public static AfkStyle? Resolve(IEnumerable<AfkStyle>? styles, string? activeId)
    {
        if (styles == null)
            return null;

        var list = styles as IList<AfkStyle> ?? styles.ToList();
        if (list.Count == 0)
            return null;

        return list.FirstOrDefault(s => s.Id == activeId)
               ?? list.FirstOrDefault(s => s.Id == AfkStylePresets.ClassicId)
               ?? list[0];
    }
}
