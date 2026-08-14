using vrcosc_magicchatbox.ViewModels.Sections;
using Xunit;
using static vrcosc_magicchatbox.Classes.Modules.MediaLinkModule;

namespace MagicChatbox.Tests.ViewModels.Sections;

public class SeekbarPreviewBuilderTests
{
    private static MediaLinkStyle Drawable() => new()
    {
        FilledCharacter = "=",
        MiddleCharacter = "O",
        NonFilledCharacter = "-",
        ProgressBarLength = 20,
        DisplayTime = false,
        ShowTimeInSuperscript = false,
        SpaceAgainObjects = false,
        TimePrefix = string.Empty,
        TimeSuffix = string.Empty,
    };

    [Fact]
    public void No_style_selected_draws_nothing_rather_than_throwing()
    {
        Assert.Equal(string.Empty, SeekbarPreviewBuilder.Build(null));
    }

    [Fact]
    public void A_blank_character_box_draws_nothing_and_the_caption_says_why()
    {
        // Half-typed styles are the normal state while somebody edits one, so the empty result is
        // expected - it just must not be presented as a finished bar.
        var half = Drawable();
        half.MiddleCharacter = string.Empty;

        string bar = SeekbarPreviewBuilder.Build(half);

        Assert.Equal(string.Empty, bar);
        Assert.Contains("Fill in all three characters", SeekbarPreviewBuilder.Caption(bar));
    }

    [Fact]
    public void The_bar_is_as_wide_as_the_style_asks_for()
    {
        // No times means the whole width is bar: filled + the one middle character + empty.
        string bar = SeekbarPreviewBuilder.Build(Drawable());

        Assert.Equal(21, bar.Length);
        Assert.Contains("O", bar);
    }

    [Fact]
    public void The_sample_position_is_part_way_through_so_both_halves_show()
    {
        string bar = SeekbarPreviewBuilder.Build(Drawable());

        Assert.Contains("=", bar);
        Assert.Contains("-", bar);
    }

    [Fact]
    public void Turning_the_times_on_puts_them_either_side_of_the_bar()
    {
        var withTimes = Drawable();
        withTimes.DisplayTime = true;

        string bar = SeekbarPreviewBuilder.Build(withTimes);

        Assert.StartsWith("1:23", bar);
        Assert.EndsWith("3:45", bar);
    }

    [Fact]
    public void The_caption_names_the_moment_it_drew()
    {
        string bar = SeekbarPreviewBuilder.Build(Drawable());

        Assert.Equal("At 1:23 of a 3:45 song", SeekbarPreviewBuilder.Caption(bar));
    }
}
