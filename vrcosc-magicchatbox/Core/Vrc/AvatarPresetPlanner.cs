using MagicChatbox.Vocabulary;
using MagicChatbox.Vrc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace vrcosc_magicchatbox.Core.Vrc;

public static class AvatarPresetPlanner
{
    public static AvatarPreset Capture(
        string name,
        AvatarIdentity identity,
        AvatarSchemaSnapshot schema,
        AvatarSenseStore? senses = null)
    {
        ArgumentNullException.ThrowIfNull(schema);

        var values = new List<AvatarPresetValue>();

        foreach (VrcParameterDeclaration declaration in schema.Parameters)
        {
            if (!declaration.Writable)
                continue;

            if (AvatarControlCatalog.IsVrchatOwned(declaration.Name))
                continue;

            double value;

            if (senses != null && senses.TryGetParameter(declaration.Name, out AvatarSense sense))
            {
                value = sense.Value;
            }
            else if (declaration.Value.HasValue)
            {
                value = ToDouble(declaration.Value.Value);
            }
            else
            {
                continue;
            }

            values.Add(new AvatarPresetValue(declaration.Name, declaration.Kind, value));
        }

        return new AvatarPreset(
            name,
            identity.Id,
            identity.Name,
            DateTime.UtcNow,
            values);
    }

    public static AvatarPreset FromSavedState(
        string name,
        AvatarIdentity identity,
        LocalAvatarState saved,
        AvatarSchemaSnapshot schema)
    {
        ArgumentNullException.ThrowIfNull(saved);
        ArgumentNullException.ThrowIfNull(schema);

        var declared = schema.Parameters.ToDictionary(
            p => EcosystemSignature.Normalize(p.Name),
            p => p,
            StringComparer.Ordinal);

        var values = new List<AvatarPresetValue>();

        foreach (LocalAvatarValue value in saved.Values)
        {
            if (AvatarControlCatalog.IsVrchatOwned(value.Name))
                continue;

            if (!declared.TryGetValue(EcosystemSignature.Normalize(value.Name), out VrcParameterDeclaration declaration))
                continue;

            if (!declaration.Writable)
                continue;

            values.Add(new AvatarPresetValue(value.Name, declaration.Kind, value.Value));
        }

        return new AvatarPreset(name, identity.Id, identity.Name, DateTime.UtcNow, values)
        {
            EyeHeight = saved.HasEyeHeight ? saved.EyeHeight : null,
        };
    }

    public static PresetApplyPlan Plan(
        AvatarPreset preset,
        AvatarSchemaSnapshot schema,
        AvatarParameterPumpOptions? pumpOptions = null)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(schema);

        var declared = schema.Parameters.ToDictionary(
            p => EcosystemSignature.Normalize(p.Name),
            p => p,
            StringComparer.Ordinal);

        var rows = new List<PresetApplyRow>(preset.Values.Count);
        int carried = 0;

        foreach (AvatarPresetValue value in preset.Values)
        {
            string key = EcosystemSignature.Normalize(value.Name);

            if (AvatarControlCatalog.IsVrchatOwned(value.Name))
            {
                rows.Add(new PresetApplyRow(value.Name, PresetOutcome.Denied, value.Kind, value.Value, value.Name));
                continue;
            }

            if (!declared.TryGetValue(key, out VrcParameterDeclaration declaration))
            {
                rows.Add(new PresetApplyRow(value.Name, PresetOutcome.NotOnThisAvatar, value.Kind, value.Value, value.Name));
                continue;
            }

            if (declaration.Kind != value.Kind)
            {
                rows.Add(new PresetApplyRow(value.Name, PresetOutcome.KindChanged, value.Kind, value.Value, declaration.Name));
                continue;
            }

            if (!declaration.Writable)
            {
                rows.Add(new PresetApplyRow(value.Name, PresetOutcome.NotWritable, value.Kind, value.Value, declaration.Name));
                continue;
            }

            rows.Add(new PresetApplyRow(value.Name, PresetOutcome.Carried, value.Kind, value.Value, declaration.Name));
            carried++;
        }

        return new PresetApplyPlan(rows, carried, rows.Count - carried, Estimate(carried, pumpOptions));
    }

    public static TimeSpan Estimate(int writes, AvatarParameterPumpOptions? options = null)
    {
        AvatarParameterPumpOptions resolved = options ?? new AvatarParameterPumpOptions();

        double perSecond = resolved.MaxSendsPerTick * (1000d / Math.Max(1, resolved.TickInterval.TotalMilliseconds));

        if (perSecond <= 0 || writes <= 0)
            return TimeSpan.Zero;

        return TimeSpan.FromSeconds(writes / perSecond);
    }

    public static int Publish(PresetApplyPlan plan, AvatarParameterPump pump)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(pump);

        int published = 0;

        foreach (PresetApplyRow row in plan.Rows)
        {
            if (row.Outcome != PresetOutcome.Carried)
                continue;

            switch (row.Kind)
            {
                case SignalKind.Bool:
                    pump.Publish(row.Target, row.Value != 0);
                    break;

                case SignalKind.Int:
                    pump.Publish(row.Target, (int)row.Value);
                    break;

                case SignalKind.Float:
                    pump.Publish(row.Target, (float)row.Value);
                    break;

                default:
                    continue;
            }

            published++;
        }

        return published;
    }

    private static double ToDouble(SignalValue value) => value.Kind switch
    {
        SignalKind.Bool => value.AsBool() ? 1d : 0d,
        SignalKind.Int => value.AsInt(),
        SignalKind.Float => value.IsFinite() ? value.AsFloat() : 0d,
        _ => 0d,
    };
}
