using MagicChatbox.Vocabulary;
using System;
using System.Collections.Generic;

namespace vrcosc_magicchatbox.Core.Vrc;

public enum PresetOutcome
{
    Carried,
    NotOnThisAvatar,
    KindChanged,
    NotWritable,
    Denied,
}

public sealed record AvatarPresetValue(string Name, SignalKind Kind, double Value);

public sealed record AvatarPreset(
    string Name,
    string AvatarId,
    string AvatarName,
    DateTime CapturedUtc,
    IReadOnlyList<AvatarPresetValue> Values)
{
    public double? EyeHeight { get; init; }

    public int Count => Values.Count;
}

public sealed record PresetApplyRow(
    string Name,
    PresetOutcome Outcome,
    SignalKind Kind,
    double Value,
    string Target)
{
    public bool WasRenamed => !string.Equals(Name, Target, StringComparison.Ordinal);
}

public sealed record PresetApplyPlan(
    IReadOnlyList<PresetApplyRow> Rows,
    int Carried,
    int Refused,
    TimeSpan Estimate)
{
    public bool IsEmpty => Carried == 0;

    public int CountOf(PresetOutcome outcome)
    {
        int count = 0;

        foreach (PresetApplyRow row in Rows)
        {
            if (row.Outcome == outcome)
                count++;
        }

        return count;
    }

    public string Summary
    {
        get
        {
            if (Rows.Count == 0)
                return "nothing saved in this preset";

            var parts = new List<string> { $"{Carried} to restore" };

            Describe(parts, PresetOutcome.NotOnThisAvatar, "not on this avatar");
            Describe(parts, PresetOutcome.KindChanged, "changed type");
            Describe(parts, PresetOutcome.NotWritable, "read-only now");
            Describe(parts, PresetOutcome.Denied, "left to VRChat");

            return string.Join(", ", parts);
        }
    }

    private void Describe(List<string> parts, PresetOutcome outcome, string wording)
    {
        int count = CountOf(outcome);

        if (count > 0)
            parts.Add($"{count} {wording}");
    }
}
