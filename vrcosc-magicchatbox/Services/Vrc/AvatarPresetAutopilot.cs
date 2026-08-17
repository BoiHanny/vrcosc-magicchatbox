using System;
using System.Collections.Generic;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Vrc;

namespace vrcosc_magicchatbox.Services.Vrc;

public sealed record AutopilotOutcome(string PresetStatus, string GlobalsStatus)
{
    public static readonly AutopilotOutcome Nothing = new(string.Empty, string.Empty);

    public bool DidAnything => PresetStatus.Length > 0 || GlobalsStatus.Length > 0;
}

public sealed class AvatarPresetAutopilot
{
    private readonly ISettingsProvider<AvatarPresetSettings> _presets;
    private readonly object _gate = new();

    private string _presetAppliedTo = string.Empty;
    private string _globalsAppliedTo = string.Empty;

    public AvatarPresetAutopilot(ISettingsProvider<AvatarPresetSettings> presets)
    {
        _presets = presets ?? throw new ArgumentNullException(nameof(presets));
    }

    public AutopilotOutcome Last { get; private set; } = AutopilotOutcome.Nothing;

    public event Action<AutopilotOutcome>? Applied;

    public void ForgetAvatar()
    {
        lock (_gate)
        {
            _presetAppliedTo = string.Empty;
            _globalsAppliedTo = string.Empty;
        }
    }

    public AutopilotOutcome OnSchema(string avatarId, AvatarSchemaSnapshot schema, AvatarParameterPump pump)
    {
        if (schema == null || pump == null || schema.IsEmpty || string.IsNullOrEmpty(avatarId))
            return AutopilotOutcome.Nothing;

        if (!string.Equals(schema.AvatarId, avatarId, StringComparison.Ordinal))
            return AutopilotOutcome.Nothing;

        bool doPreset;
        bool doGlobals;

        lock (_gate)
        {
            doPreset = !string.Equals(_presetAppliedTo, avatarId, StringComparison.Ordinal);
            doGlobals = !string.Equals(_globalsAppliedTo, avatarId, StringComparison.Ordinal);

            if (doPreset)
                _presetAppliedTo = avatarId;

            if (doGlobals)
                _globalsAppliedTo = avatarId;
        }

        string presetStatus = doPreset ? ApplyAutomaticPreset(avatarId, schema, pump) : string.Empty;
        string globalsStatus = doGlobals ? ApplyGlobals(schema, pump) : string.Empty;

        var outcome = new AutopilotOutcome(presetStatus, globalsStatus);

        if (outcome.DidAnything)
        {
            Last = outcome;
            Applied?.Invoke(outcome);
        }

        return outcome;
    }

    private string ApplyAutomaticPreset(string avatarId, AvatarSchemaSnapshot schema, AvatarParameterPump pump)
    {
        AvatarPreset? automatic = _presets.Value.Presets
            .FirstOrDefault(p => p != null
                && p.Automatic
                && string.Equals(p.AvatarId, avatarId, StringComparison.Ordinal));

        if (automatic == null)
            return string.Empty;

        PresetApplyPlan plan = AvatarPresetPlanner.Plan(automatic, schema);

        if (plan.IsEmpty)
            return string.Empty;

        AvatarPresetPlanner.Publish(plan, pump);

        return $"Put \"{automatic.Name}\" on for you: {plan.Summary}.";
    }

    private string ApplyGlobals(AvatarSchemaSnapshot schema, AvatarParameterPump pump)
    {
        AvatarPresetSettings settings = _presets.Value;

        if (!settings.ApplyGlobalsOnAvatarChange)
            return string.Empty;

        List<AvatarPresetValue> globals = settings.Globals.Where(g => g != null).ToList();

        if (globals.Count == 0)
            return string.Empty;

        var asPreset = new AvatarPreset(
            "Shared defaults",
            schema.AvatarId,
            string.Empty,
            DateTime.UtcNow,
            globals);

        PresetApplyPlan plan = AvatarPresetPlanner.Plan(asPreset, schema);

        if (plan.IsEmpty)
            return string.Empty;

        AvatarPresetPlanner.Publish(plan, pump);

        return $"Set {plan.Carried} of your {globals.Count} defaults on this avatar.";
    }
}
