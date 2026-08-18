using System.Linq;
using vrcosc_magicchatbox.Classes.Modules;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules;

/// <summary>
/// Weather is on for almost everyone, and its template was the one input nothing bounded: a long
/// one used to push every integration after it off the line instead of shortening itself.
/// </summary>
public class WeatherBudgetTests
{
    [Fact]
    public void A_template_longer_than_the_line_is_capped_where_it_is_stored()
    {
        var settings = new WeatherSettings { WeatherTemplate = new string('x', 400) };

        Assert.Equal(WeatherBudget.MaxTemplateLength, settings.WeatherTemplate.Length);
    }

    [Fact]
    public void A_template_that_fits_is_stored_exactly_as_typed()
    {
        const string template = "{time} {tempWithUnit} {condition}";
        var settings = new WeatherSettings { WeatherTemplate = template };

        Assert.Equal(template, settings.WeatherTemplate);
    }

    [Fact]
    public void Clearing_the_template_is_still_allowed()
    {
        var settings = new WeatherSettings { WeatherTemplate = "{weather}" };
        settings.WeatherTemplate = null!;

        Assert.Equal(string.Empty, settings.WeatherTemplate);
        Assert.True(settings.WeatherTemplateIsEmpty);
    }

    [Fact]
    public void An_edit_past_the_cap_tells_the_editor_to_snap_back()
    {
        var settings = new WeatherSettings { WeatherTemplate = new string('x', 400) };
        bool raised = false;
        settings.PropertyChanged += (_, e) => raised |= e.PropertyName == nameof(WeatherSettings.WeatherTemplate);

        settings.WeatherTemplate = new string('x', 401);

        Assert.True(raised);
        Assert.Equal(WeatherBudget.MaxTemplateLength, settings.WeatherTemplate.Length);
    }

    [Fact]
    public void The_cap_never_stores_half_an_emoji()
    {
        // The 'a' puts every pair on an odd offset, so the cap lands on a high surrogate.
        string overlong = "a" + string.Concat(Enumerable.Repeat("\U0001F324", 200));
        var settings = new WeatherSettings { WeatherTemplate = overlong };

        Assert.True(settings.WeatherTemplate.Length <= WeatherBudget.MaxTemplateLength);
        Assert.False(char.IsHighSurrogate(settings.WeatherTemplate[^1]));
    }

    [Fact]
    public void The_segment_never_takes_more_than_its_share_of_the_line()
    {
        string bounded = WeatherBudget.Bound(new string('w', 200), roomOnTheLine: 144);

        Assert.Equal(WeatherBudget.MaxSegmentLength, bounded.Length);
    }

    [Fact]
    public void The_room_actually_left_wins_when_it_is_the_smaller_of_the_two()
    {
        string bounded = WeatherBudget.Bound(new string('w', 200), roomOnTheLine: 20);

        Assert.Equal(20, bounded.Length);
    }

    [Fact]
    public void Weather_that_already_fits_is_handed_over_untouched()
    {
        Assert.Equal("18ᶜ ᶜˡᵉᵃʳ", WeatherBudget.Bound("18ᶜ ᶜˡᵉᵃʳ", roomOnTheLine: 144));
    }

    [Fact]
    public void The_share_is_wide_enough_for_every_stock_reading_at_once()
    {
        // Every toggle on, decimals on, the coldest Fahrenheit reading and the longest condition -
        // 62 characters. The share exists to stop templates, not to shorten a real forecast.
        const string worstStockLine = "\U0001F327 -76.0ᶠ ᶠʳᵉᵉᶻⁱⁿᵍ ᵈʳⁱᶻᶻˡᵉ ᶠᵉᵉˡˢ -76.0ᶠ ʷⁱⁿᵈ 200.0ᵏᵐ·ʰ ʰᵘᵐ 100";

        Assert.Equal(62, worstStockLine.Length);
        Assert.Equal(worstStockLine, WeatherBudget.Bound(worstStockLine, roomOnTheLine: 144));
    }

    [Fact]
    public void No_room_left_leaves_nothing_rather_than_a_bare_ellipsis()
    {
        Assert.Equal(string.Empty, WeatherBudget.Bound("18ᶜ", roomOnTheLine: 0));
        Assert.Equal(string.Empty, WeatherBudget.Bound("18ᶜ", roomOnTheLine: -5));
    }
}
