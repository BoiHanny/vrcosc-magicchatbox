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

public sealed record PresetApplyRow(string Name, PresetOutcome Outcome, SignalKind Kind, double Value);

public sealed record PresetApplyPlan(
    IReadOnlyList<PresetApplyRow> Rows,
    int Carried,
    int Refused,
    TimeSpan Estimate)
{
    public bool IsEmpty => Carried == 0;

    public string Summary => Refused == 0
        ? $"{Carried} to restore"
        : $"{Carried} to restore, {Refused} not on this avatar";
}
