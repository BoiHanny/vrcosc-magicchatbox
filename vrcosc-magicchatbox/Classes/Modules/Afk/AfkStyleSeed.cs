using System.Collections.Generic;
using System.Linq;

namespace vrcosc_magicchatbox.Classes.Modules.Afk;

public readonly record struct AfkStyleSeedResult(IReadOnlyList<AfkStyle> Styles, string ActiveId);

/// <summary>
/// Turns the four loose AFK fields that existed before styles into a style list, once, on load.
/// </summary>
public static class AfkStyleSeed
{
    /// <summary>
    /// Someone who never touched the AFK wording gets the shipped styles with Classic selected, since
    /// Classic *is* what they were already seeing. Someone who did customise it keeps their exact
    /// wording as a style of their own, selected, sitting alongside the presets - upgrading must not
    /// quietly replace text somebody wrote.
    /// </summary>
    public static AfkStyleSeedResult Build(
        string? prefix,
        bool showPrefix,
        string? messageWithTime,
        string? messageWithoutTime,
        bool showTime)
    {
        var styles = AfkStylePresets.Build().ToList();

        bool isUntouched =
            (prefix ?? string.Empty) == AfkStylePresets.ClassicPrefix
            && (messageWithTime ?? string.Empty) == AfkStylePresets.ClassicWithTime
            && (messageWithoutTime ?? string.Empty) == AfkStylePresets.ClassicWithoutTime;

        if (isUntouched)
        {
            // Their prefix and time switches still carry over onto the style they will be using.
            var classic = styles.First(s => s.Id == AfkStylePresets.ClassicId);
            classic.ShowPrefix = showPrefix;
            classic.ShowTime = showTime;

            return new AfkStyleSeedResult(styles, AfkStylePresets.ClassicId);
        }

        var mine = new AfkStyle
        {
            Id = "yours",
            Name = "Yours",
            IsBuiltIn = false,
            ShowPrefix = showPrefix,
            Prefix = prefix ?? string.Empty,
            ShowTime = showTime,
            MessageWithTime = messageWithTime ?? string.Empty,
            MessageWithoutTime = messageWithoutTime ?? string.Empty,
        };

        styles.Insert(0, mine);
        return new AfkStyleSeedResult(styles, mine.Id);
    }

    /// <summary>
    /// Picks the style to use, tolerating a stored id that no longer exists because the style behind
    /// it was deleted. Never returns null while any style exists - a missing style must not silently
    /// stop the AFK line from being sent.
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
