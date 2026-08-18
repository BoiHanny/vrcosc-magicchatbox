using vrcosc_magicchatbox.Core.Osc;
using Xunit;

namespace MagicChatbox.Tests.Core.Osc;

/// <summary>
/// Providers size themselves against this, so an answer that is too generous shows up as a segment
/// the builder then has to clip or drop.
/// </summary>
public class OscBuildContextTests
{
    private static OscBuildContext Context(
        string prefix = "",
        string suffix = "",
        string separator = " ┆ ",
        params string[] collected)
        => new()
        {
            Prefix = prefix,
            Suffix = suffix,
            Separator = separator,
            CurrentSegments = collected,
        };

    [Fact]
    public void The_prefix_and_suffix_cost_even_when_nothing_has_been_collected()
    {
        // They are always sent, so they always cost. Skipping them while the line was still empty
        // told the first provider it had the whole 144.
        var context = Context(prefix: "[[", suffix: "]]");

        Assert.Equal(OscBuildContext.MaxOscLength - 4, context.RemainingCharsIf(string.Empty));
    }

    [Fact]
    public void An_empty_line_with_no_wrapper_still_reports_the_whole_budget()
        => Assert.Equal(OscBuildContext.MaxOscLength, Context().RemainingCharsIf(string.Empty));

    [Fact]
    public void The_first_segment_is_not_charged_for_a_separator()
    {
        var context = Context();

        Assert.Equal(OscBuildContext.MaxOscLength - 5, context.RemainingCharsIf("abcde"));
    }

    [Fact]
    public void A_later_segment_is_charged_for_the_separator_that_joins_it()
    {
        var context = Context(collected: "abc");

        // "abc" + separator + "de"
        Assert.Equal(OscBuildContext.MaxOscLength - (3 + 3 + 2), context.RemainingCharsIf("de"));
    }

    [Fact]
    public void Asking_about_an_empty_candidate_already_includes_the_separator()
    {
        // A provider that then subtracts the separator itself is charging for it twice.
        var context = Context(collected: "abc");

        Assert.Equal(OscBuildContext.MaxOscLength - (3 + 3), context.RemainingCharsIf(string.Empty));
    }

    [Fact]
    public void What_fits_agrees_with_what_is_left()
    {
        var context = Context(prefix: ">", suffix: "<", collected: "abc");

        int room = context.RemainingCharsIf(string.Empty);
        string exact = new('x', room);

        Assert.True(context.WouldFit(exact));
        Assert.False(context.WouldFit(exact + "x"));
    }
}
