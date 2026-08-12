using System.Collections.Generic;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules.Afk;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules.Afk;

public class AfkStyleRenderTests
{
    private static AfkStyle Classic() => new()
    {
        Prefix = "💤",
        ShowPrefix = true,
        ShowTime = true,
        MessageWithTime = "ᶜᵘʳʳᵉⁿᵗˡʸ AFK ᶠᵒʳ ",
        MessageWithoutTime = "ᶜᵘʳʳᵉⁿᵗˡʸ AFK",
    };

    [Fact]
    public void Renders_the_same_line_the_old_code_built()
    {
        Assert.Equal("💤 ᶜᵘʳʳᵉⁿᵗˡʸ AFK ᶠᵒʳ 12ᵐ", Classic().Render("12ᵐ"));
    }

    [Fact]
    public void Falls_back_to_the_no_clock_wording_before_a_duration_exists()
    {
        // The old code checked the duration for null; a blank one has to behave the same way, or the
        // line reads "currently AFK for " with nothing after it.
        Assert.Equal("💤 ᶜᵘʳʳᵉⁿᵗˡʸ AFK", Classic().Render(null));
        Assert.Equal("💤 ᶜᵘʳʳᵉⁿᵗˡʸ AFK", Classic().Render("   "));
    }

    [Fact]
    public void The_clock_switch_picks_the_other_wording()
    {
        var style = Classic();
        style.ShowTime = false;

        Assert.Equal("💤 ᶜᵘʳʳᵉⁿᵗˡʸ AFK", style.Render("12ᵐ"));
    }

    [Fact]
    public void Turning_the_prefix_off_removes_its_space_too()
    {
        var style = Classic();
        style.ShowPrefix = false;

        Assert.Equal("ᶜᵘʳʳᵉⁿᵗˡʸ AFK ᶠᵒʳ 12ᵐ", style.Render("12ᵐ"));
    }

    [Fact]
    public void An_empty_prefix_does_not_leave_a_leading_space()
    {
        var style = Classic();
        style.Prefix = "";

        Assert.Equal("ᶜᵘʳʳᵉⁿᵗˡʸ AFK ᶠᵒʳ 12ᵐ", style.Render("12ᵐ"));
    }

    [Fact]
    public void Both_new_line_escapes_still_work()
    {
        var style = Classic();
        style.ShowPrefix = false;
        style.ShowTime = false;
        style.MessageWithoutTime = @"away\nback soon";

        Assert.Equal("away\nback soon", style.Render(null));

        style.MessageWithoutTime = "away/nback soon";
        Assert.Equal("away\nback soon", style.Render(null));
    }

    [Fact]
    public void Cloning_gives_a_new_identity_and_is_never_built_in()
    {
        var source = Classic();
        source.IsBuiltIn = true;
        var copy = source.Clone("Mine");

        Assert.NotEqual(source.Id, copy.Id);
        Assert.Equal("Mine", copy.Name);
        Assert.False(copy.IsBuiltIn);
        Assert.Equal(source.MessageWithTime, copy.MessageWithTime);
    }
}

public class AfkStyleSeedTests
{
    private static AfkStyleComposition Compose(
        IEnumerable<AfkStyle>? custom = null,
        IEnumerable<AfkStyle>? legacy = null,
        string? activeId = null,
        string prefix = AfkStylePresets.ClassicPrefix,
        bool showPrefix = true,
        string withTime = AfkStylePresets.ClassicWithTime,
        string withoutTime = AfkStylePresets.ClassicWithoutTime,
        bool showTime = true)
        => AfkStyleSeed.Compose(custom, legacy, activeId, prefix, showPrefix, withTime, withoutTime, showTime);

    [Fact]
    public void Untouched_settings_land_on_Classic_with_nothing_invented()
    {
        var seeded = Compose();

        Assert.Equal(AfkStylePresets.ClassicId, seeded.ActiveId);
        Assert.Empty(seeded.CustomStyles);
    }

    [Fact]
    public void Untouched_settings_still_carry_the_prefix_and_clock_switches_over()
    {
        var seeded = Compose(showPrefix: false, showTime: false);

        var active = AfkStyleSeed.Resolve(seeded.AllStyles, seeded.ActiveId);

        Assert.NotNull(active);
        Assert.False(active!.ShowPrefix);
        Assert.False(active.ShowTime);
    }

    [Fact]
    public void Wording_somebody_wrote_themselves_is_kept_and_selected()
    {
        var seeded = Compose(prefix: "🎮", withTime: "gaming, back in ", withoutTime: "gaming");

        var active = AfkStyleSeed.Resolve(seeded.AllStyles, seeded.ActiveId);

        Assert.NotNull(active);
        Assert.Equal("Yours", active!.Name);
        Assert.Equal("gaming, back in ", active.MessageWithTime);
        Assert.Equal("🎮", active.Prefix);
        Assert.False(active.IsBuiltIn);
    }

    [Fact]
    public void The_shipped_styles_are_always_there_to_pick_from()
    {
        var seeded = Compose(prefix: "🎮", withTime: "gaming, back in ", withoutTime: "gaming");

        Assert.Contains(seeded.AllStyles, s => s.Id == AfkStylePresets.ClassicId);
        Assert.True(seeded.AllStyles.Count(s => s.IsBuiltIn) >= 4);
    }

    [Fact]
    public void Shipped_styles_are_never_written_to_disk()
    {
        // The whole point: they are code. Persisting a copy is what stopped new presets from ever
        // reaching anyone who had already run the app once.
        var seeded = Compose(prefix: "🎮", withTime: "mine ", withoutTime: "mine");

        Assert.All(seeded.CustomStyles, s => Assert.False(s.IsBuiltIn));
        Assert.DoesNotContain(seeded.CustomStyles, s => AfkStyleSeed.IsBuiltInId(s.Id));
    }

    [Fact]
    public void Presets_frozen_into_an_older_settings_file_are_discarded_for_the_code_ones()
    {
        // Exactly the upgrade case: a stale "Back soon" saved to disk must not shadow the real one.
        var stale = new List<AfkStyle>
        {
            new() { Id = "builtin-backsoon", Name = "Back soon", MessageWithoutTime = "old wording", IsBuiltIn = true },
            new() { Id = "builtin-classic", Name = "Classic", IsBuiltIn = true },
        };

        var seeded = Compose(legacy: stale);

        Assert.Empty(seeded.CustomStyles);

        var backSoon = seeded.AllStyles.Single(s => s.Id == "builtin-backsoon");
        Assert.NotEqual("old wording", backSoon.MessageWithoutTime);
    }

    [Fact]
    public void Styles_somebody_made_survive_that_same_upgrade()
    {
        var stored = new List<AfkStyle>
        {
            new() { Id = "builtin-classic", Name = "Classic", IsBuiltIn = true },
            new() { Id = "mine", Name = "Raiding", MessageWithoutTime = "gone", IsBuiltIn = false },
        };

        var seeded = Compose(legacy: stored, activeId: "mine");

        var mine = Assert.Single(seeded.CustomStyles);
        Assert.Equal("mine", mine.Id);
        Assert.Equal("gone", mine.MessageWithoutTime);
        Assert.Equal("mine", seeded.ActiveId);
        Assert.Contains(seeded.AllStyles, s => s.Id == "mine");
    }

    [Fact]
    public void A_new_preset_added_in_a_later_version_shows_up_for_existing_users()
    {
        var seeded = Compose(custom: new List<AfkStyle> { new() { Id = "mine", Name = "Mine" } });

        foreach (var shipped in AfkStylePresets.Build())
            Assert.Contains(seeded.AllStyles, s => s.Id == shipped.Id);
    }

    [Fact]
    public void Composing_twice_does_not_duplicate_anything()
    {
        // It runs on every load, and the second pass must be a no-op.
        var once = Compose(custom: new List<AfkStyle> { new() { Id = "mine", Name = "Mine" } });
        var twice = AfkStyleSeed.Compose(once.CustomStyles, null, once.ActiveId,
            AfkStylePresets.ClassicPrefix, true, AfkStylePresets.ClassicWithTime,
            AfkStylePresets.ClassicWithoutTime, true);

        Assert.Equal(once.AllStyles.Count, twice.AllStyles.Count);
        Assert.Single(twice.CustomStyles);
    }

    [Fact]
    public void A_shipped_style_is_never_recreated_as_yours_when_one_already_exists()
    {
        // Custom styles present means this is not a first run, so the legacy fields are history and
        // must not be resurrected as a duplicate "Yours".
        var seeded = Compose(
            custom: new List<AfkStyle> { new() { Id = "mine", Name = "Mine" } },
            prefix: "🎮", withTime: "gaming ", withoutTime: "gaming");

        Assert.Single(seeded.CustomStyles);
        Assert.DoesNotContain(seeded.CustomStyles, s => s.Name == "Yours");
    }

    [Fact]
    public void Shipped_styles_cannot_be_deleted()
    {
        Assert.All(AfkStylePresets.Build(), s => Assert.True(s.IsBuiltIn));
    }

    [Fact]
    public void Every_shipped_style_fits_the_line_with_room_to_spare()
    {
        // These share 144 characters with every other integration, so a preset that eats the budget
        // on its own would be a bad default.
        foreach (var style in AfkStylePresets.Build())
            Assert.True(style.Render("12ᵐ 04ˢ").Length < 60, $"{style.Name} is {style.Render("12ᵐ 04ˢ").Length} chars");
    }

    [Fact]
    public void A_stored_id_pointing_at_a_deleted_style_still_resolves_to_something()
    {
        var styles = AfkStylePresets.Build();

        var resolved = AfkStyleSeed.Resolve(styles, "a-style-that-was-deleted");

        Assert.NotNull(resolved);
        Assert.Equal(AfkStylePresets.ClassicId, resolved!.Id);
    }

    [Fact]
    public void Resolving_falls_back_to_the_first_style_when_even_Classic_is_gone()
    {
        var styles = new List<AfkStyle> { new() { Id = "only", Name = "Only" } };

        Assert.Equal("only", AfkStyleSeed.Resolve(styles, "missing")!.Id);
    }

    [Fact]
    public void Resolving_an_empty_list_returns_nothing_rather_than_throwing()
    {
        Assert.Null(AfkStyleSeed.Resolve(new List<AfkStyle>(), "anything"));
        Assert.Null(AfkStyleSeed.Resolve(null, "anything"));
    }
}
