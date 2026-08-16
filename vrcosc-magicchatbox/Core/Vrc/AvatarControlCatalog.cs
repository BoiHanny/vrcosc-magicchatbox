using MagicChatbox.Vocabulary;
using MagicChatbox.Vrc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace vrcosc_magicchatbox.Core.Vrc;

public enum AvatarWidget
{
    Toggle,
    StateWord,
    Stepper,
    Emote,
    Slider,
    Meter,
}

public sealed record AvatarControlRow(
    string Name,
    string Leaf,
    SignalKind Kind,
    bool Writable,
    AvatarWidget Widget,
    double Value,
    bool HasValue,
    bool IsBuiltIn);

public sealed record AvatarControlGroup(string Name, string DisplayName, IReadOnlyList<AvatarControlRow> Rows)
{
    public bool IsUngrouped => Name.Length == 0;
}

public sealed record AvatarControlView(
    IReadOnlyList<AvatarControlGroup> Groups,
    int CustomCount,
    int BuiltInCount,
    int HiddenGroupCount,
    int HiddenRowCount);

public static class AvatarControlCatalog
{
    public const string EmoteParameter = "VRCEmote";

    private static readonly string[] AdultGroupNames =
    [
        "anal", "blowjob", "handjob", "footjob", "thighjob", "assjob",
        "frotting", "frot", "pussy", "penis", "vagina", "breast", "boob",
        "nipple", "genital", "orifice", "orf", "pen", "cum", "nsfw", "lewd",
    ];

    private static readonly string[] AdultPrefixes = ["OGB/", "TPS_Internal/", "DPS/"];

    private static readonly HashSet<string> BuiltIns = new(StringComparer.Ordinal)
    {
        "IsLocal", "PreviewMode", "Viseme", "Voice", "GestureLeft", "GestureRight",
        "GestureLeftWeight", "GestureRightWeight", "AngularY", "VelocityX", "VelocityY",
        "VelocityZ", "VelocityMagnitude", "Upright", "Grounded", "Seated", "AFK",
        "TrackingType", "VRMode", "MuteSelf", "InStation", "Earmuffs", "IsOnFriendsList",
        "AvatarVersion", "ScaleModified", "ScaleFactor", "ScaleFactorInverse",
        "EyeHeightAsMeters", "EyeHeightAsPercent",
    };

    public static bool IsBuiltIn(string name) => BuiltIns.Contains(name);

    public static AvatarWidget WidgetFor(SignalKind kind, bool writable, string name)
    {
        if (!writable)
            return kind == SignalKind.Float ? AvatarWidget.Meter : AvatarWidget.StateWord;

        return kind switch
        {
            SignalKind.Bool => AvatarWidget.Toggle,
            SignalKind.Int => string.Equals(name, EmoteParameter, StringComparison.Ordinal)
                ? AvatarWidget.Emote
                : AvatarWidget.Stepper,
            SignalKind.Float => AvatarWidget.Slider,
            _ => AvatarWidget.StateWord,
        };
    }

    public static string GroupOf(string name)
    {
        int slash = name.IndexOf('/');
        return slash <= 0 ? string.Empty : name[..slash];
    }

    public static string LeafOf(string name)
    {
        int slash = name.LastIndexOf('/');
        return slash < 0 || slash == name.Length - 1 ? name : name[(slash + 1)..];
    }

    public static bool IsAdultGroup(string group)
    {
        if (group.Length == 0)
            return false;

        foreach (string prefix in AdultPrefixes)
        {
            if (group.StartsWith(prefix.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return MatchesAdultTerm(group);
    }

    public static bool IsAdultName(string name)
    {
        foreach (string prefix in AdultPrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        foreach (string segment in name.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (MatchesAdultTerm(segment))
                return true;
        }

        return false;
    }

    public static AvatarControlView Build(
        AvatarSchemaSnapshot schema,
        AvatarSenseStore? senses = null,
        string? search = null,
        bool hideAdult = true,
        bool writableOnly = false)
    {
        ArgumentNullException.ThrowIfNull(schema);

        string filter = (search ?? string.Empty).Trim();

        var kept = new List<AvatarControlRow>();
        var hiddenGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int hiddenRows = 0;
        int custom = 0;
        int builtIn = 0;

        foreach (VrcParameterDeclaration declaration in schema.Parameters)
        {
            string name = declaration.Name ?? string.Empty;
            if (name.Length == 0)
                continue;

            bool isBuiltIn = IsBuiltIn(name);

            if (isBuiltIn) builtIn++;
            else custom++;

            if (hideAdult && IsAdultName(name))
            {
                hiddenRows++;
                string adultGroup = GroupOf(name);
                hiddenGroups.Add(adultGroup.Length == 0 ? name : adultGroup);
                continue;
            }

            if (writableOnly && !declaration.Writable)
                continue;

            if (filter.Length > 0 && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            double value = 0;
            bool hasValue = false;

            if (declaration.Value.HasValue)
            {
                value = ToDouble(declaration.Value.Value);
                hasValue = true;
            }

            if (senses != null && senses.TryGetParameter(name, out AvatarSense sense))
            {
                value = sense.Value;
                hasValue = true;
            }

            kept.Add(new AvatarControlRow(
                name,
                LeafOf(name),
                declaration.Kind,
                declaration.Writable,
                WidgetFor(declaration.Kind, declaration.Writable, name),
                value,
                hasValue,
                isBuiltIn));
        }

        var groups = kept
            .GroupBy(r => GroupOf(r.Name), StringComparer.Ordinal)
            .Select(g => new AvatarControlGroup(
                g.Key,
                g.Key.Length == 0 ? "Ungrouped" : g.Key,
                g.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList()))
            .OrderBy(g => g.IsUngrouped ? 1 : 0)
            .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new AvatarControlView(groups, custom, builtIn, hiddenGroups.Count, hiddenRows);
    }

    private static bool MatchesAdultTerm(string segment)
    {
        foreach (string term in AdultGroupNames)
        {
            if (segment.Equals(term, StringComparison.OrdinalIgnoreCase))
                return true;

            if (segment.Length > term.Length
                && segment.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0
                && term.Length >= 4)
            {
                return true;
            }
        }

        return false;
    }

    private static double ToDouble(SignalValue value) => value.Kind switch
    {
        SignalKind.Bool => value.AsBool() ? 1d : 0d,
        SignalKind.Int => value.AsInt(),
        SignalKind.Float => value.IsFinite() ? value.AsFloat() : 0d,
        _ => 0d,
    };
}
