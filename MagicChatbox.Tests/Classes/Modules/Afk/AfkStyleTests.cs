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
    [Fact]
    public void Untouched_settings_land_on_Classic_with_nothing_invented()
    {
        var seeded = AfkStyleSeed.Build(
            AfkStylePresets.ClassicPrefix, true,
            AfkStylePresets.ClassicWithTime, AfkStylePresets.ClassicWithoutTime, true);

        Assert.Equal(AfkStylePresets.ClassicId, seeded.ActiveId);
        Assert.DoesNotContain(seeded.Styles, s => s.Name == "Yours");
    }

    [Fact]
    public void Untouched_settings_still_carry_the_prefix_and_clock_switches_over()
    {
        var seeded = AfkStyleSeed.Build(
            AfkStylePresets.ClassicPrefix, showPrefix: false,
            AfkStylePresets.ClassicWithTime, AfkStylePresets.ClassicWithoutTime, showTime: false);

        var active = AfkStyleSeed.Resolve(seeded.Styles, seeded.ActiveId);

        Assert.NotNull(active);
        Assert.False(active!.ShowPrefix);
        Assert.False(active.ShowTime);
    }

    [Fact]
    public void Wording_somebody_wrote_themselves_is_kept_and_selected()
    {
        var seeded = AfkStyleSeed.Build("🎮", true, "gaming, back in ", "gaming", true);

        var active = AfkStyleSeed.Resolve(seeded.Styles, seeded.ActiveId);

        Assert.NotNull(active);
        Assert.Equal("Yours", active!.Name);
        Assert.Equal("gaming, back in ", active.MessageWithTime);
        Assert.Equal("🎮", active.Prefix);
        Assert.False(active.IsBuiltIn);
    }

    [Fact]
    public void The_shipped_styles_are_always_there_to_pick_from()
    {
        var seeded = AfkStyleSeed.Build("🎮", true, "gaming, back in ", "gaming", true);

        Assert.Contains(seeded.Styles, s => s.Id == AfkStylePresets.ClassicId);
        Assert.True(seeded.Styles.Count(s => s.IsBuiltIn) >= 4);
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
