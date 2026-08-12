using System.Collections.Generic;
using System.Linq;

namespace vrcosc_magicchatbox.Classes.Modules.Afk;

public readonly record struct AfkStyleComposition(
    IReadOnlyList<AfkStyle> AllStyles,
    IReadOnlyList<AfkStyle> CustomStyles,
    string ActiveId);

/// <summary>
/// Decides what the style list contains on every load.
///
/// The shipped styles are built from code each time and never saved, which is the same arrangement
/// the media link seekbar presets use. The alternative - writing them into the settings file once -
/// is what the first version did, and it means a preset can never be improved, renamed or added to
/// again: the copy in the file wins forever and a new version's work is invisible.
/// </summary>
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

        // Anything carrying a shipped id is dropped rather than kept: it is a stale copy of something
        // that now lives in code. Everything else was written by hand and has to survive.
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

        // First run on a settings file older than styles: the four loose fields become a style, but
        // only if they were actually customised. Matching the old defaults means Classic already says
        // it, and nobody needs a duplicate of a preset in their own list.
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
            // Their prefix and clock switches still belong to whatever they were using.
            var classic = all.First(s => s.Id == AfkStylePresets.ClassicId);
            classic.ShowPrefix = legacyShowPrefix;
            classic.ShowTime = legacyShowTime;
        }

        all.AddRange(mine);

        // A stored choice wins. Failing that, wording just rescued from the old settings fields is
        // what this person was actually looking at a moment ago, so it is the honest default.
        string resolved = !string.IsNullOrEmpty(activeId) && all.Any(s => s.Id == activeId)
            ? activeId!
            : migrated?.Id ?? AfkStylePresets.ClassicId;

        return new AfkStyleComposition(all, mine, resolved);
    }

    private static bool MatchesClassic(string? prefix, string? withTime, string? withoutTime)
        => (prefix ?? string.Empty) == AfkStylePresets.ClassicPrefix
           && (withTime ?? string.Empty) == AfkStylePresets.ClassicWithTime
           && (withoutTime ?? string.Empty) == AfkStylePresets.ClassicWithoutTime;

    /// <summary>
    /// Picks the style to use, tolerating a stored id that no longer exists because the style behind
    /// it was deleted, or because a preset was renamed between versions. Never returns null while any
    /// style exists - a missing style must not silently stop the AFK line from being sent.
    /// </summary>
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
