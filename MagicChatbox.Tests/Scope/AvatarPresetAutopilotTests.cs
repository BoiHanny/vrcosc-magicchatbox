using MagicChatbox.Vocabulary;
using MagicChatbox.Vrc;
using System;
using System.Collections.Generic;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Vrc;
using vrcosc_magicchatbox.Services.Vrc;
using Xunit;

namespace MagicChatbox.Tests.Scope;

// "Wear this look automatically" used to be driven by a timer that only ran while the Avatar page was on
// screen, so it worked exactly when nobody needed it to. It now runs off the schema arriving, which
// happens whether or not anybody is looking.
public class AvatarPresetAutopilotTests
{
    private sealed class Provider<T> : ISettingsProvider<T> where T : class, new()
    {
        public Provider(T value) => Value = value;

        public T Value { get; }

        public event EventHandler SettingsChanged;

        public void Save() => SettingsChanged?.Invoke(this, EventArgs.Empty);

        public void FlushPendingSave() { }

        public void Reload() { }
    }

    private static AvatarSchemaSnapshot Schema(string avatarId, params string[] names) =>
        new(avatarId, 1, DateTime.UtcNow,
            names.Select(n => new VrcParameterDeclaration(n, SignalKind.Bool, SignalValue.Bool(false), true)).ToList());

    private static AvatarPreset Look(string avatarId, bool automatic, params string[] names) =>
        new("Date night", avatarId, "Kobold", DateTime.UtcNow,
            names.Select(n => new AvatarPresetValue(n, SignalKind.Bool, 1)).ToList())
        { Automatic = automatic };

    private static (AvatarPresetAutopilot Pilot, AvatarPresetSettings Settings, AvatarParameterPump Pump) Build()
    {
        var settings = new AvatarPresetSettings();
        return (new AvatarPresetAutopilot(new Provider<AvatarPresetSettings>(settings)), settings, new AvatarParameterPump());
    }

    [Fact]
    public void A_look_marked_automatic_goes_on_when_the_schema_lands()
    {
        var (pilot, settings, pump) = Build();
        settings.Presets.Add(Look("avtr_one", automatic: true, "Toggles/Hat"));

        AutopilotOutcome outcome = pilot.OnSchema("avtr_one", Schema("avtr_one", "Toggles/Hat"), pump);

        Assert.Contains("Date night", outcome.PresetStatus);
        Assert.Contains("1 to restore", outcome.PresetStatus);
        Assert.Same(outcome, pilot.Last);
    }

    [Fact]
    public void It_runs_once_per_avatar_rather_than_once_per_schema()
    {
        // The schema arrives again on every re-harvest. Re-applying would fight anybody who moved a
        // control by hand afterwards.
        var (pilot, settings, pump) = Build();
        settings.Presets.Add(Look("avtr_one", automatic: true, "Toggles/Hat"));

        pilot.OnSchema("avtr_one", Schema("avtr_one", "Toggles/Hat"), pump);
        AutopilotOutcome again = pilot.OnSchema("avtr_one", Schema("avtr_one", "Toggles/Hat"), pump);

        Assert.False(again.DidAnything);
    }

    [Fact]
    public void Putting_the_avatar_back_on_applies_it_again()
    {
        var (pilot, settings, pump) = Build();
        settings.Presets.Add(Look("avtr_one", automatic: true, "Toggles/Hat"));

        pilot.OnSchema("avtr_one", Schema("avtr_one", "Toggles/Hat"), pump);
        pilot.OnSchema("avtr_two", Schema("avtr_two", "Toggles/Hat"), pump);
        AutopilotOutcome back = pilot.OnSchema("avtr_one", Schema("avtr_one", "Toggles/Hat"), pump);

        Assert.Contains("Date night", back.PresetStatus);
    }

    [Fact]
    public void A_look_belonging_to_another_avatar_is_left_alone()
    {
        var (pilot, settings, pump) = Build();
        settings.Presets.Add(Look("avtr_other", automatic: true, "Toggles/Hat"));

        Assert.False(pilot.OnSchema("avtr_one", Schema("avtr_one", "Toggles/Hat"), pump).DidAnything);
    }

    [Fact]
    public void A_schema_describing_a_different_avatar_is_refused()
    {
        // The guard against acting on a harvest that arrived after the wearer moved on.
        var (pilot, settings, pump) = Build();
        settings.Presets.Add(Look("avtr_one", automatic: true, "Toggles/Hat"));

        Assert.False(pilot.OnSchema("avtr_one", Schema("avtr_previous", "Toggles/Hat"), pump).DidAnything);
    }

    [Fact]
    public void An_empty_schema_is_refused_rather_than_reported_as_nothing_to_restore()
    {
        var (pilot, settings, pump) = Build();
        settings.Presets.Add(Look("avtr_one", automatic: true, "Toggles/Hat"));

        Assert.False(pilot.OnSchema("avtr_one", AvatarSchemaSnapshot.Empty, pump).DidAnything);
    }

    [Fact]
    public void Shared_defaults_go_on_only_when_that_is_switched_on()
    {
        var (pilot, settings, pump) = Build();
        settings.Globals.Add(new AvatarPresetValue("EyeTrackingActive", SignalKind.Bool, 1));
        settings.ApplyGlobalsOnAvatarChange = false;

        Assert.False(pilot.OnSchema("avtr_one", Schema("avtr_one", "EyeTrackingActive"), pump).DidAnything);

        pilot.ForgetAvatar();
        settings.ApplyGlobalsOnAvatarChange = true;

        AutopilotOutcome outcome = pilot.OnSchema("avtr_one", Schema("avtr_one", "EyeTrackingActive"), pump);

        Assert.Equal("Set 1 of your 1 defaults on this avatar.", outcome.GlobalsStatus);
    }

    [Fact]
    public void A_look_naming_nothing_this_avatar_has_reports_nothing_rather_than_a_failure()
    {
        var (pilot, settings, pump) = Build();
        settings.Presets.Add(Look("avtr_one", automatic: true, "Toggles/Gone"));

        Assert.False(pilot.OnSchema("avtr_one", Schema("avtr_one", "Toggles/Hat"), pump).DidAnything);
    }
}
