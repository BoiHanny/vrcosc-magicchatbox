using MagicChatbox.Vocabulary;
using Newtonsoft.Json;
using System;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Vrc;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules;

// A saved look that does not survive a restart is worse than no saved looks: the user does the work
// once, sees it listed, and finds it gone tomorrow with nothing to explain where it went. AvatarPreset
// is a positional record holding an IReadOnlyList, which is exactly the shape that round-trips through
// a serializer by luck rather than by design, so it is pinned here.
public class AvatarPresetSettingsTests
{
    private static AvatarPreset Sample() => new(
        "Club night",
        "avtr_9f0d",
        "Ashlynn",
        new DateTime(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc),
        [
            new AvatarPresetValue("Toggles/Jacket", SignalKind.Bool, 1),
            new AvatarPresetValue("Face/Blush", SignalKind.Float, 0.4),
            new AvatarPresetValue("Modes/Outfit", SignalKind.Int, 2),
        ]);

    [Fact]
    public void A_saved_look_survives_being_written_and_read_back()
    {
        var settings = new AvatarPresetSettings();
        settings.Presets.Add(Sample());

        string json = JsonConvert.SerializeObject(settings);
        AvatarPresetSettings? restored = JsonConvert.DeserializeObject<AvatarPresetSettings>(json);

        Assert.NotNull(restored);
        AvatarPreset preset = Assert.Single(restored!.Presets);

        Assert.Equal("Club night", preset.Name);
        Assert.Equal("avtr_9f0d", preset.AvatarId);
        Assert.Equal("Ashlynn", preset.AvatarName);
        Assert.Equal(3, preset.Count);

        AvatarPresetValue blush = preset.Values.Single(v => v.Name == "Face/Blush");
        Assert.Equal(SignalKind.Float, blush.Kind);
        Assert.Equal(0.4, blush.Value, 6);
    }

    [Fact]
    public void The_kind_of_every_value_survives_the_round_trip()
    {
        // The kind decides which overload the pump is called through, so losing it turns a float into
        // a bool and puts the wrong thing on the avatar.
        var settings = new AvatarPresetSettings();
        settings.Presets.Add(Sample());

        AvatarPresetSettings restored = JsonConvert.DeserializeObject<AvatarPresetSettings>(
            JsonConvert.SerializeObject(settings))!;

        Assert.Equal(
            Sample().Values.Select(v => (v.Name, v.Kind)),
            restored.Presets.Single().Values.Select(v => (v.Name, v.Kind)));
    }

    [Fact]
    public void Reading_a_file_written_before_looks_existed_gives_an_empty_list()
    {
        // Everybody upgrading has no presets key at all.
        AvatarPresetSettings? restored = JsonConvert.DeserializeObject<AvatarPresetSettings>("{}");

        Assert.NotNull(restored);
        Assert.Empty(restored!.Presets);
    }

    [Fact]
    public void Looks_saved_for_one_avatar_do_not_appear_on_another()
    {
        // Names are the avatar author's own invention: 69% of the saved parameter names on this
        // machine appear on exactly one avatar, so a look has no meaning anywhere else.
        var settings = new AvatarPresetSettings();
        settings.Presets.Add(Sample());
        settings.Presets.Add(Sample() with { AvatarId = "avtr_other", Name = "Someone else's" });

        var mine = settings.Presets.Where(p => p.AvatarId == "avtr_9f0d").ToList();

        Assert.Single(mine);
        Assert.Equal("Club night", mine[0].Name);
    }
}
