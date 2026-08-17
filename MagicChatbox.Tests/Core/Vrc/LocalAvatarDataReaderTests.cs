using MagicChatbox.Vocabulary;
using MagicChatbox.Vrc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using vrcosc_magicchatbox.Core.Vrc;
using Xunit;

namespace MagicChatbox.Tests.Core.Vrc;

// VRChat keeps the saved state of every avatar you have worn in LocalAvatarData, and nothing in this
// ecosystem reads it. Measured on this machine: 467 files, 10,839 saved parameters, a median of 16 per
// avatar. It is the only place the app can learn what an avatar is wearing without the avatar being
// worn, and 92.8% of what is in there is drivable back over OSC.
//
// The files have no extension and no byte-order mark, unlike the OSC configs beside them, and VRChat
// rewrites them while it runs - so the reader has to be unbothered by a file that is missing, locked,
// half-written or not JSON at all.
public sealed class LocalAvatarDataReaderTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "mcb-lad-" + Guid.NewGuid().ToString("N"));

    private string WriteAvatar(string avatarId, string json, string user = "usr_test")
    {
        string folder = Path.Combine(_root, user);
        Directory.CreateDirectory(folder);

        string path = Path.Combine(folder, avatarId);

        // Deliberately without a BOM: that is how VRChat writes these, and writing them any other way
        // would be testing a file VRChat never produces.
        File.WriteAllText(path, json, new UTF8Encoding(false));

        return path;
    }

    private const string Sample =
        """
        {"eyeHeight":1.32, "legacyFingers":false, "animationParameters":[
          {"name":"(f)Weight", "value":1},
          {"name":"Toggle/(s-b)AltEars", "value":0},
          {"name":"Face/Blush", "value":0.4}
        ]}
        """;

    [Fact]
    public void The_saved_state_of_an_avatar_can_be_read_without_wearing_it()
    {
        WriteAvatar("avtr_9f0d", Sample);

        LocalAvatarState? state = new LocalAvatarDataReader(_root).TryRead("avtr_9f0d");

        Assert.NotNull(state);
        Assert.Equal(1.32, state!.EyeHeight, 4);
        Assert.False(state.LegacyFingers);
        Assert.Equal(3, state.Count);
        Assert.Equal(0.4, state.Values.Single(v => v.Name == "Face/Blush").Value, 4);
    }

    [Fact]
    public void Parameter_names_keep_the_punctuation_their_authors_used()
    {
        // Real names on this machine include "(f)Weight" and "Toggle/(s-b)AltEars". Anything that
        // sanitises them stops matching the OSC config, which is what makes them drivable.
        WriteAvatar("avtr_9f0d", Sample);

        var names = new LocalAvatarDataReader(_root).TryRead("avtr_9f0d")!.Values.Select(v => v.Name);

        Assert.Contains("(f)Weight", names);
        Assert.Contains("Toggle/(s-b)AltEars", names);
    }

    [Fact]
    public void Every_avatar_the_user_owns_is_found_across_every_account_folder()
    {
        WriteAvatar("avtr_one", Sample, "usr_a");
        WriteAvatar("avtr_two", Sample, "usr_b");

        var all = new LocalAvatarDataReader(_root).ReadAll();

        Assert.Equal(2, all.Count);
        Assert.Contains(all, a => a.AvatarId == "avtr_one");
        Assert.Contains(all, a => a.AvatarId == "avtr_two");
    }

    [Fact]
    public void A_file_being_rewritten_underneath_does_not_take_the_others_with_it()
    {
        WriteAvatar("avtr_good", Sample);
        WriteAvatar("avtr_torn", "{\"eyeHeight\":1.3, \"animationParam");

        var all = new LocalAvatarDataReader(_root).ReadAll();

        Assert.Single(all);
        Assert.Equal("avtr_good", all[0].AvatarId);
    }

    [Fact]
    public void Files_that_are_not_avatars_are_left_alone()
    {
        WriteAvatar("avtr_good", Sample);
        WriteAvatar("something-else.txt", Sample);

        Assert.Single(new LocalAvatarDataReader(_root).ReadAll());
    }

    [Fact]
    public void A_missing_folder_is_an_empty_answer_rather_than_a_crash()
    {
        var reader = new LocalAvatarDataReader(Path.Combine(_root, "not-there"));

        Assert.False(reader.Exists);
        Assert.Empty(reader.ReadAll());
        Assert.Null(reader.TryRead("avtr_9f0d"));
    }

    [Theory]
    [InlineData("../../secrets")]
    [InlineData("usr_a/avtr_one")]
    [InlineData(@"usr_a\avtr_one")]
    [InlineData("C:/Windows/win.ini")]
    public void An_avatar_id_cannot_be_used_to_reach_out_of_the_folder(string id)
    {
        WriteAvatar("avtr_good", Sample);

        Assert.Null(new LocalAvatarDataReader(_root).TryRead(id));
    }

    [Fact]
    public void Saved_state_becomes_a_look_only_where_the_avatar_agrees()
    {
        // The saved file knows a name and a number and nothing else. Which kind it is, and whether it
        // can be written at all, are the live schema's to say - so the schema decides what carries.
        WriteAvatar("avtr_9f0d", Sample);

        LocalAvatarState saved = new LocalAvatarDataReader(_root).TryRead("avtr_9f0d")!;

        var schema = new AvatarSchemaSnapshot("avtr_9f0d", 1, DateTime.UtcNow,
        [
            new VrcParameterDeclaration("(f)Weight", SignalKind.Float, SignalValue.Float(0), true),
            new VrcParameterDeclaration("Face/Blush", SignalKind.Float, SignalValue.Float(0), false),
        ]);

        AvatarPreset preset = AvatarPresetPlanner.FromSavedState(
            "From VRChat",
            new AvatarIdentity("avtr_9f0d", "Ashlynn", AvatarIdSource.SchemaHarvest),
            saved,
            schema);

        AvatarPresetValue only = Assert.Single(preset.Values);
        Assert.Equal("(f)Weight", only.Name);
        Assert.Equal(SignalKind.Float, only.Kind);
        Assert.Equal(1.32, preset.EyeHeight!.Value, 4);
    }

    [Fact]
    public void A_look_built_from_saved_state_still_refuses_VRChat_s_own_parameters()
    {
        WriteAvatar("avtr_9f0d", """{"animationParameters":[{"name":"VRCEmote", "value":3}]}""");

        LocalAvatarState saved = new LocalAvatarDataReader(_root).TryRead("avtr_9f0d")!;

        var schema = new AvatarSchemaSnapshot("avtr_9f0d", 1, DateTime.UtcNow,
            [new VrcParameterDeclaration("VRCEmote", SignalKind.Int, SignalValue.Int(0), true)]);

        AvatarPreset preset = AvatarPresetPlanner.FromSavedState(
            "From VRChat",
            new AvatarIdentity("avtr_9f0d", "Ashlynn", AvatarIdSource.SchemaHarvest),
            saved,
            schema);

        Assert.Empty(preset.Values);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
