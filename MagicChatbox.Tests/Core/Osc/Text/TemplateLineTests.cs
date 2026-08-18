using vrcosc_magicchatbox.Core.Osc.Text;
using Xunit;

namespace MagicChatbox.Tests.Core.Osc.Text;

/// <summary>
/// A template writes its own glue, so an empty field leaves the punctuation behind. These pin what
/// can be recovered from the finished line - and, just as importantly, what cannot.
/// </summary>
public class TemplateLineTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Nothing_in_gives_nothing_back(string? input)
    {
        Assert.Equal(string.Empty, TemplateLine.DropStrandedJoiners(input));
    }

    [Fact]
    public void A_line_no_longer_starts_with_a_bare_dash()
    {
        // What the stock template renders with nothing playing: every field but the status is empty.
        Assert.Equal("Spotify paused", TemplateLine.DropStrandedJoiners("- Spotify paused"));
    }

    [Fact]
    public void Glue_left_hanging_off_the_end_goes_too()
    {
        Assert.Equal("The Weeknd", TemplateLine.DropStrandedJoiners("The Weeknd -"));
    }

    [Fact]
    public void A_field_that_vanished_between_two_joiners_does_not_leave_both_behind()
    {
        Assert.Equal("The Weeknd - After Hours", TemplateLine.DropStrandedJoiners("The Weeknd - - After Hours"));
    }

    [Fact]
    public void Glue_doing_its_job_is_left_alone()
    {
        const string line = "▶ Blinding Lights - The Weeknd";
        Assert.Equal(line, TemplateLine.DropStrandedJoiners(line));
    }

    [Fact]
    public void Punctuation_inside_a_word_is_not_glue()
    {
        const string line = "▶ Thunderstruck - AC/DC";
        Assert.Equal(line, TemplateLine.DropStrandedJoiners(line));
    }

    [Fact]
    public void A_label_ending_in_a_colon_survives()
    {
        const string line = "▶ DJ: Blinding Lights";
        Assert.Equal(line, TemplateLine.DropStrandedJoiners(line));
    }

    [Fact]
    public void Every_line_is_cleaned_not_just_the_first()
    {
        Assert.Equal(
            "Blinding Lights\nThe Weeknd",
            TemplateLine.DropStrandedJoiners("Blinding Lights\nThe Weeknd -"));
    }

    [Fact]
    public void A_line_that_was_only_glue_disappears_entirely()
    {
        Assert.Equal("Blinding Lights", TemplateLine.DropStrandedJoiners("Blinding Lights\n-"));
    }

    [Fact]
    public void A_gap_the_user_typed_in_the_middle_is_theirs_to_keep()
    {
        Assert.Equal("Blinding Lights\n\nThe Weeknd", TemplateLine.DropStrandedJoiners("Blinding Lights\n\nThe Weeknd"));
    }

    [Fact]
    public void Glue_stranded_between_two_real_words_cannot_be_recovered()
    {
        // The honest limit: once {artist} is gone the dash sits between the icon and the title, and
        // the finished line no longer says whether it was ever joining anything. Fixing this one
        // means the template engine dropping the separator while it still knows the field was empty.
        Assert.Equal("▶ - Blinding Lights", TemplateLine.DropStrandedJoiners("▶ - Blinding Lights"));
    }
}
