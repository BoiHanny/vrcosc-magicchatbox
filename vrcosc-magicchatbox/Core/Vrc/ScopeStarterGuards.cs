using MagicChatbox.Scope;
using System;
using System.Collections.Generic;

namespace vrcosc_magicchatbox.Core.Vrc;

public sealed record ScopeStarterGuard(string Name, string Note, ScopeTarget Target, ScopeGroup When)
{
    public ScopeRule Adopt(string id) =>
        ScopeRule.For(id, Name, Target, When) with { Note = Note, Enabled = false };
}

public static class ScopeStarterGuards
{
    public const string ConfigPrefix = "MCB/Cfg/";

    public static readonly IReadOnlyList<ScopeStarterGuard> All = new ScopeStarterGuard[]
    {
        new(
            "Only while the avatar allows sending",
            "Wear an avatar carrying MCB/Cfg/Sending and it decides whether this app sends anything at all.",
            ScopeTarget.Sending,
            AllowedBy("Sending")),

        new(
            "Only while the avatar allows heart rate",
            "Wear an avatar carrying MCB/Cfg/HeartRate and it decides whether your heart rate reaches the chatbox.",
            ScopeTarget.Integration("HeartRate"),
            AllowedBy("HeartRate")),

        new(
            "Only while the avatar allows media",
            "Wear an avatar carrying MCB/Cfg/Media and it decides whether what you are listening to reaches the chatbox.",
            ScopeTarget.Integration("MediaLink"),
            AllowedBy("Media")),

        new(
            "Only while the avatar allows window activity",
            "Wear an avatar carrying MCB/Cfg/WindowActivity and it decides whether the app you have open reaches the chatbox.",
            ScopeTarget.Integration("Window"),
            AllowedBy("WindowActivity")),

        new(
            "Only while the avatar allows your status",
            "Wear an avatar carrying MCB/Cfg/Status and it decides whether your personal status reaches the chatbox.",
            ScopeTarget.Integration("Status"),
            AllowedBy("Status")),

        new(
            "Not in public instances",
            "Keeps this quiet anywhere a stranger could walk in.",
            ScopeTarget.Sending,
            ScopeGroup.All(ScopePredicate.IsNot(ScopeFactKey.InstanceType, "Public"))),

        new(
            "Not in a packed room",
            "Keeps this quiet once the instance fills up.",
            ScopeTarget.Sending,
            ScopeGroup.All(ScopePredicate.IsNot(ScopeFactKey.InstanceCrowd, "Packed"))),
    };

    private static ScopeGroup AllowedBy(string configName) =>
        ScopeGroup.Any(
            new ScopePredicate(ScopeFactKey.Parameter(ConfigPrefix + configName), ScopeOperator.IsNotLive, default),
            ScopePredicate.IsOn(ConfigPrefix + configName));

    public static string NextId(IEnumerable<ScopeRule> existing)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (ScopeRule rule in existing)
        {
            if (rule?.Id is { Length: > 0 } id)
                used.Add(id);
        }

        for (int n = 1; ; n++)
        {
            string candidate = "rule-" + n.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (used.Add(candidate))
                return candidate;
        }
    }
}
