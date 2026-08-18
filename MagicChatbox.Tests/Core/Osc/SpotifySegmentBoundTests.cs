using vrcosc_magicchatbox.Core.Osc;
using vrcosc_magicchatbox.Core.Osc.Providers;
using Xunit;

namespace MagicChatbox.Tests.Core.Osc;

/// <summary>
/// Spotify used to hand the builder whatever the template produced. Its own ladder stops with the
/// title still whole, so one long track name crowded every other integration off the chatbox before
/// the survivor was clipped. These pin the bound, and the tidy-up that runs before it.
/// </summary>
public class SpotifySegmentBoundTests
{
    private const string LongTitle =
        "Fear Inoculum Extended Mix Live From The Hollywood Bowl With The Full Orchestra " +
        "And Choir Remastered Deluxe Anniversary Edition Bonus Track Instrumental Reprise " +
        "Final Movement Encore";

    [Fact]
    public void A_track_name_longer_than_the_chatbox_no_longer_takes_the_whole_line()
    {
        string raw = $"▶ - {LongTitle} ♥";
        Assert.True(raw.Length > OscBuildContext.MaxOscLength, "the fixture has to overflow to be worth testing");

        string text = SpotifyOscProvider.ToSegmentText(raw, OscBuildContext.MaxOscLength);

        Assert.True(text.Length <= OscBuildContext.MaxOscLength,
            $"expected the segment to be bounded, but it was {text.Length} characters");
        Assert.EndsWith("…", text);

        // Cut at the last whole word, so it stops just short rather than anywhere near short.
        Assert.True(text.Length > OscBuildContext.MaxOscLength - 20,
            $"expected the cut to still use the line, but it kept only {text.Length} characters");
    }

    [Fact]
    public void The_bound_is_the_room_left_not_the_whole_line()
    {
        // Two integrations already on the line and the separator between them: whatever they leave
        // is all Spotify may spend.
        var context = new OscBuildContext
        {
            CurrentSegments = ["12:34", "72♥"],
            Separator = " ┆ ",
            Prefix = string.Empty,
            Suffix = string.Empty
        };

        int budget = context.RemainingCharsIf(string.Empty);

        // No word boundary worth cutting at, so this one spends the budget to the character.
        string text = SpotifyOscProvider.ToSegmentText("▶ " + new string('T', 300), budget);

        Assert.Equal(budget, text.Length);
        Assert.True(context.WouldFit(text));
    }

    [Fact]
    public void A_line_that_already_fits_is_handed_over_untouched()
    {
        const string line = "▶ Blinding Lights - The Weeknd ♥";

        Assert.Equal(line, SpotifyOscProvider.ToSegmentText(line, OscBuildContext.MaxOscLength));
    }

    [Fact]
    public void The_paused_line_loses_the_dash_the_empty_fields_left_behind()
    {
        // The stock template with nothing playing renders every field but the status empty.
        Assert.Equal("Spotify paused", SpotifyOscProvider.ToSegmentText("- Spotify paused", OscBuildContext.MaxOscLength));
    }

    [Fact]
    public void A_prefix_that_fills_the_line_on_its_own_leaves_nothing_to_send()
    {
        var context = new OscBuildContext
        {
            Separator = " ┆ ",
            Prefix = new string('P', OscBuildContext.MaxOscLength),
            Suffix = string.Empty
        };

        Assert.Equal(
            string.Empty,
            SpotifyOscProvider.ToSegmentText("▶ Blinding Lights", context.RemainingCharsIf(string.Empty)));
    }
}
