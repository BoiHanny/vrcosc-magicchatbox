using MagicChatbox.Vocabulary;
using MagicChatbox.Vrc;
using System;
using System.Linq;
using vrcosc_magicchatbox.Core.Vrc;
using vrcosc_magicchatbox.Core.Vrc.Sharing;
using Xunit;

namespace MagicChatbox.Tests.Core.Vrc;

// Every place that looked a parameter up by name built its dictionary with ToDictionary, and every one
// of them crashed the app on the first avatar that had two names mapping to one key.
//
// That is not an exotic case, it is what Normalize is FOR: it strips VRCFury's VF##_ prefix and
// Modular Avatar's suffixes precisely so a renamed parameter matches its original, which means an
// avatar carrying both the original and a renamed copy produces a duplicate key by design. It was
// reported from a real avatar on the key "superneko.realkiss.contact.mouth".
//
// Collisions are counted rather than thrown on, and the un-renamed name wins, because that is the one
// the avatar author actually declared.
public class AvatarSchemaIndexTests
{
    private static VrcParameterDeclaration P(string name, bool writable = true)
        => new(name, SignalKind.Bool, SignalValue.Bool(false), writable);

    private static AvatarSchemaSnapshot Schema(params string[] names)
        => new("avtr_test", 1, DateTime.UtcNow, names.Select(n => P(n)).ToList());

    [Fact]
    public void Two_names_that_normalise_together_do_not_throw()
    {
        AvatarSchemaLookup lookup = AvatarSchemaIndex.ByNormalizedName(
            [P("superneko.realkiss.contact.mouth"), P("VF12_superneko.realkiss.contact.mouth")]);

        Assert.True(lookup.Contains("superneko.realkiss.contact.mouth"));
        Assert.Equal(1, lookup.Ambiguous);
    }

    [Fact]
    public void The_name_the_author_declared_wins_over_the_renamed_copy()
    {
        // Whichever declaration is kept decides where a write is addressed, so keeping the installer's
        // renamed copy would send to a name the original avatar does not answer to.
        AvatarSchemaLookup lookup = AvatarSchemaIndex.ByNormalizedName(
            [P("VF12_Toggles/Hat"), P("Toggles/Hat")]);

        Assert.True(lookup.TryGet("Toggles/Hat", out VrcParameterDeclaration kept));
        Assert.Equal("Toggles/Hat", kept.Name);
    }

    [Fact]
    public void The_renamed_copy_is_used_when_it_is_the_only_one_there()
    {
        // The case the whole normalisation exists for: the installer renamed it and the original is
        // gone, so the renamed name is the one to drive.
        AvatarSchemaLookup lookup = AvatarSchemaIndex.ByNormalizedName([P("VF88_Toggles/Hat")]);

        Assert.True(lookup.TryGet("Toggles/Hat", out VrcParameterDeclaration kept));
        Assert.Equal("VF88_Toggles/Hat", kept.Name);
        Assert.Equal(0, lookup.Ambiguous);
    }

    [Fact]
    public void Names_differing_only_by_case_do_not_throw_when_looked_up_case_insensitively()
    {
        // Real, and documented on an avatar called Starfisha: Toggles/ring and Toggles/Ring coexist.
        AvatarSchemaLookup lookup = AvatarSchemaIndex.ByExactName(
            [P("Toggles/ring"), P("Toggles/Ring")], StringComparer.OrdinalIgnoreCase);

        Assert.Equal(1, lookup.Ambiguous);
        Assert.True(lookup.Contains("toggles/RING"));
    }

    [Fact]
    public void A_parameter_with_no_name_is_skipped_rather_than_keyed_on_nothing()
    {
        AvatarSchemaLookup lookup = AvatarSchemaIndex.ByExactName([P(string.Empty), P("Toggles/Hat")]);

        Assert.Single(lookup.ByName);
    }

    [Fact]
    public void Planning_a_preset_against_a_colliding_avatar_works_instead_of_crashing()
    {
        // The reported crash, end to end.
        var preset = new AvatarPreset(
            "Test", "avtr_test", "Test", DateTime.UtcNow,
            [new AvatarPresetValue("superneko.realkiss.contact.mouth", SignalKind.Bool, 1)]);

        PresetApplyPlan plan = AvatarPresetPlanner.Plan(
            preset,
            Schema("superneko.realkiss.contact.mouth", "VF12_superneko.realkiss.contact.mouth"));

        Assert.Equal(1, plan.Carried);
        Assert.Equal("superneko.realkiss.contact.mouth", plan.Rows.Single().Target);
    }

    [Fact]
    public void Seeding_config_against_a_colliding_avatar_works_too()
    {
        var applied = new System.Collections.Generic.List<bool>();

        var seeder = new AvatarConfigSeeder(
            [new AvatarConfigBinding("MCB/Cfg/Media", "test", ConfigDirection.OffOnly, applied.Add)],
            () => true,
            TimeSpan.Zero);

        var schema = new AvatarSchemaSnapshot("avtr_test", 1, DateTime.UtcNow,
        [
            new VrcParameterDeclaration("MCB/Cfg/Media", SignalKind.Bool, SignalValue.Bool(false), true),
            new VrcParameterDeclaration("VF3_MCB/Cfg/Media", SignalKind.Bool, SignalValue.Bool(false), true),
        ]);

        seeder.Seed(schema);

        Assert.Single(applied);
    }

    [Fact]
    public void Matching_a_shared_layout_against_a_colliding_avatar_works_too()
    {
        var document = new LayoutDocument { Title = "Test" };
        document.Requires.Add(new LayoutRequirement { Name = "Toggles/Hat", Type = "Bool" });

        LayoutMatchReport report = LayoutCodec.Match(
            document,
            Schema("Toggles/Hat", "VF9_Toggles/Hat"));

        Assert.True(report.Satisfied);
    }

    [Fact]
    public void Building_a_look_from_saved_state_against_a_colliding_avatar_works_too()
    {
        var saved = new LocalAvatarState(
            "avtr_test", 1.3, false,
            [new LocalAvatarValue("Toggles/Hat", 1)],
            DateTime.UtcNow);

        AvatarPreset preset = AvatarPresetPlanner.FromSavedState(
            "From VRChat",
            new AvatarIdentity("avtr_test", "Test", AvatarIdSource.SchemaHarvest),
            saved,
            Schema("Toggles/Hat", "VF4_Toggles/Hat"));

        Assert.Single(preset.Values);
    }
}
