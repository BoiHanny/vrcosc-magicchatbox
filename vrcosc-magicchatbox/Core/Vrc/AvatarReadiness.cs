using System;
using System.Collections.Generic;
using System.Linq;

namespace vrcosc_magicchatbox.Core.Vrc;

public enum ReadinessState
{
    NotConnected,
    Faulted,
    RouteOff,
    Waiting,
    Ready,
    FoundOtherPrefab,
    Driving,
}

public sealed record ReadinessInput(
    string Feature,
    bool SourceConnected,
    bool SourceLive,
    bool RouteEnabled,
    string? FaultMessage,
    IReadOnlyList<string> WrittenNames);

public sealed record ReadinessRow(
    string Feature,
    ReadinessState State,
    string Headline,
    string Detail,
    int Matched,
    int Total)
{
    public bool IsFault => State == ReadinessState.Faulted;

    public bool IsLit => State == ReadinessState.Driving;
}

public static class AvatarReadiness
{
    public static ReadinessRow Evaluate(
        ReadinessInput input,
        AvatarSchemaSnapshot schema,
        bool avatarKnown)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(schema);

        int total = input.WrittenNames.Count;

        if (!input.SourceConnected)
        {
            return new ReadinessRow(
                input.Feature, ReadinessState.NotConnected,
                "Not connected", "Connect it in options to start sending.", 0, total);
        }

        if (!string.IsNullOrWhiteSpace(input.FaultMessage))
        {
            return new ReadinessRow(
                input.Feature, ReadinessState.Faulted, "Problem", input.FaultMessage!, 0, total);
        }

        if (!input.SourceLive)
        {
            return new ReadinessRow(
                input.Feature, ReadinessState.NotConnected,
                "No data yet", "Connected, but nothing is coming through.", 0, total);
        }

        if (!input.RouteEnabled)
        {
            return new ReadinessRow(
                input.Feature, ReadinessState.RouteOff,
                "Turned off", "Sending to your avatar is switched off for this one.", 0, total);
        }

        if (!avatarKnown || schema.IsEmpty)
        {
            return new ReadinessRow(
                input.Feature, ReadinessState.Waiting,
                "Waiting", "Waiting to see your avatar.", 0, total);
        }

        var declared = schema.Parameters
            .Where(p => p.Writable)
            .Select(p => EcosystemSignature.Normalize(p.Name))
            .ToHashSet(StringComparer.Ordinal);

        int matched = input.WrittenNames.Count(n => declared.Contains(EcosystemSignature.Normalize(n)));

        if (matched > 0)
        {
            return new ReadinessRow(
                input.Feature, ReadinessState.Driving,
                $"Driving {matched} of {total}", "This avatar reacts to it.", matched, total);
        }

        string? shape = EcosystemSignature.FindHeartRateShape(schema.Parameters.Select(p => p.Name));

        if (shape != null)
        {
            return new ReadinessRow(
                input.Feature, ReadinessState.FoundOtherPrefab,
                "Different names",
                $"This avatar looks like it already has something under {shape} — it just uses different names to ours.",
                0, total);
        }

        return new ReadinessRow(
            input.Feature, ReadinessState.Ready,
            "Ready",
            "This avatar has no parameters for it, which is normal. Add them to make it react.",
            0, total);
    }
}
