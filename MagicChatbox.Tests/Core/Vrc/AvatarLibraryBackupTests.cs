using MagicChatbox.Vocabulary;
using MagicChatbox.Vrc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using vrcosc_magicchatbox.Core.Vrc;
using Xunit;

namespace MagicChatbox.Tests.Core.Vrc;

// VRChat keeps an avatar's saved settings per machine, not per account, so a second PC starts every
// avatar from scratch and there is nothing in the game or in this ecosystem that moves them.
//
// Restoring deliberately does NOT write into VRChat's own files. They belong to a running game that
// rewrites them on its own schedule, and a backup is not worth corrupting somebody's avatars for. The
// values go to the avatar over OSC, the same path every other control on the page uses.
public class AvatarLibraryBackupTests
{
    private static LocalAvatarState State(string id, params (string Name, double Value)[] values)
        => new(
            id,
            1.32,
            false,
            values.Select(v => new LocalAvatarValue(v.Name, v.Value)).ToList(),
            DateTime.UtcNow);

    [Fact]
    public void A_backup_survives_being_written_and_read_back()
    {
        AvatarLibraryBackupFile built = AvatarLibraryBackup.Build(
            [State("avtr_a", ("Toggles/Hat", 1), ("Face/Blush", 0.4))],
            new DateTime(2026, 8, 17, 9, 0, 0, DateTimeKind.Utc));

        var restored = JsonConvert.DeserializeObject<AvatarLibraryBackupFile>(
            JsonConvert.SerializeObject(built));

        Assert.True(AvatarLibraryBackup.IsUsable(restored, out _));

        AvatarLibraryBackupEntry entry = Assert.Single(restored!.Avatars);
        Assert.Equal("avtr_a", entry.AvatarId);
        Assert.Equal(0.4, entry.Values["Face/Blush"], 4);
        Assert.Equal(1.32, entry.EyeHeight, 4);
    }

    [Fact]
    public void An_avatar_with_nothing_saved_is_left_out()
    {
        AvatarLibraryBackupFile file = AvatarLibraryBackup.Build([State("avtr_empty")], DateTime.UtcNow);

        Assert.Empty(file.Avatars);
    }

    [Fact]
    public void VRChat_s_own_parameters_are_not_backed_up()
    {
        AvatarLibraryBackupFile file = AvatarLibraryBackup.Build(
            [State("avtr_a", ("VRCEmote", 3), ("Toggles/Hat", 1))],
            DateTime.UtcNow);

        Assert.DoesNotContain("VRCEmote", file.Avatars.Single().Values.Keys);
        Assert.Contains("Toggles/Hat", file.Avatars.Single().Values.Keys);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"kind":"something.else","schema":1,"avatars":[{"avatarId":"a"}]}""")]
    [InlineData("""{"kind":"mcb.avatarlibrary","schema":99,"avatars":[{"avatarId":"a"}]}""")]
    [InlineData("""{"kind":"mcb.avatarlibrary","schema":1,"avatars":[]}""")]
    public void A_file_that_is_not_a_usable_backup_is_refused_with_a_reason(string json)
    {
        var file = JsonConvert.DeserializeObject<AvatarLibraryBackupFile>(json);

        Assert.False(AvatarLibraryBackup.IsUsable(file, out string detail));
        Assert.False(string.IsNullOrWhiteSpace(detail));
    }

    [Fact]
    public void Restoring_takes_the_kind_from_the_avatar_rather_than_guessing_it_from_the_number()
    {
        // The backup holds a name and a number and nothing else - VRChat's own file has no types in
        // it. Guessing from the value would read Modes/Outfit = 1 as a bool and the write would be
        // refused as a changed type, which is a silent failure dressed up as a safety check.
        AvatarLibraryBackupFile file = AvatarLibraryBackup.Build(
            [State("avtr_a", ("Modes/Outfit", 1))],
            DateTime.UtcNow);

        LocalAvatarState? saved = AvatarLibraryBackup.StateFor(file, "avtr_a");
        Assert.NotNull(saved);

        var schema = new AvatarSchemaSnapshot("avtr_a", 1, DateTime.UtcNow,
            [new VrcParameterDeclaration("Modes/Outfit", SignalKind.Int, SignalValue.Int(0), true)]);

        AvatarPreset preset = AvatarPresetPlanner.FromSavedState(
            "From backup",
            new AvatarIdentity("avtr_a", "Test", AvatarIdSource.SchemaHarvest),
            saved!,
            schema);

        AvatarPresetValue only = Assert.Single(preset.Values);
        Assert.Equal(SignalKind.Int, only.Kind);

        PresetApplyPlan plan = AvatarPresetPlanner.Plan(preset, schema);
        Assert.Equal(1, plan.Carried);
    }

    [Fact]
    public void A_backup_for_a_different_avatar_is_not_offered_for_this_one()
    {
        AvatarLibraryBackupFile file = AvatarLibraryBackup.Build(
            [State("avtr_a", ("Toggles/Hat", 1))],
            DateTime.UtcNow);

        Assert.Null(AvatarLibraryBackup.StateFor(file, "avtr_b"));
        Assert.Null(AvatarLibraryBackup.StateFor(file, string.Empty));
    }

    [Fact]
    public void A_huge_library_is_capped_rather_than_written_without_a_limit()
    {
        var many = Enumerable.Range(0, AvatarLibraryBackup.MaxAvatars + 50)
            .Select(i => State($"avtr_{i}", ("Toggles/Hat", 1)))
            .ToList();

        Assert.Equal(AvatarLibraryBackup.MaxAvatars, AvatarLibraryBackup.Build(many, DateTime.UtcNow).Avatars.Count);
    }
}
