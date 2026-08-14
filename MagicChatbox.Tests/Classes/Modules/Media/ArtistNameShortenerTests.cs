using System.Collections.Generic;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules.Media;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules.Media;

public class ArtistNameShortenerTests
{
    [Theory]
    [InlineData("Rick Astley - Topic", "Rick Astley")]
    [InlineData("Rick Astley-Topic", "Rick Astley")]
    [InlineData("  Rick Astley  ", "Rick Astley")]
    [InlineData("Rick Astley", "Rick Astley")]
    public void Clean_strips_the_youtube_topic_suffix(string input, string expected)
        => Assert.Equal(expected, ArtistNameShortener.Clean(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Clean_handles_missing_credits(string? input)
        => Assert.Equal(string.Empty, ArtistNameShortener.Clean(input));

    [Fact]
    public void SplitCredits_splits_the_comma_list_youtube_sends()
    {
        Assert.Equal(
            ["Ariana Grande", "Doja Cat", "Megan Thee Stallion"],
            ArtistNameShortener.SplitCredits("Ariana Grande, Doja Cat, Megan Thee Stallion"));
    }

    [Fact]
    public void SplitCredits_treats_a_trailing_ampersand_as_the_last_comma()
    {
        Assert.Equal(
            ["A", "B", "C"],
            ArtistNameShortener.SplitCredits("A, B & C"));
    }

    [Fact]
    public void SplitCredits_leaves_a_duo_that_owns_its_ampersand_alone()
    {
        Assert.Equal(
            ["Simon & Garfunkel"],
            ArtistNameShortener.SplitCredits("Simon & Garfunkel"));
    }

    [Fact]
    public void Ladder_is_empty_when_there_is_no_credit()
        => Assert.Empty(ArtistNameShortener.Ladder("   "));

    [Fact]
    public void Ladder_of_a_single_artist_is_just_that_artist()
        => Assert.Equal(["Rick Astley"], ArtistNameShortener.Ladder("Rick Astley - Topic"));

    [Fact]
    public void Ladder_sheds_one_credit_at_a_time_and_counts_what_went()
    {
        Assert.Equal(
            [
                "Ariana Grande, Doja Cat, Megan Thee Stallion",
                "Ariana Grande, Doja Cat +1",
                "Ariana Grande +2",
                "Ariana Grande",
            ],
            ArtistNameShortener.Ladder("Ariana Grande, Doja Cat, Megan Thee Stallion"));
    }

    [Fact]
    public void Ladder_drops_the_featured_guest_before_it_cuts_a_credit()
    {
        IReadOnlyList<string> ladder = ArtistNameShortener.Ladder("Calvin Harris feat. Rihanna");

        Assert.Equal("Calvin Harris feat. Rihanna", ladder[0]);
        Assert.Equal("Calvin Harris", ladder[1]);
    }

    [Theory]
    [InlineData("Calvin Harris ft. Rihanna")]
    [InlineData("Calvin Harris (feat. Rihanna)")]
    [InlineData("Calvin Harris [featuring Rihanna]")]
    [InlineData("Calvin Harris FEAT. Rihanna")]
    public void Ladder_recognises_the_usual_ways_of_writing_a_feature(string credit)
        => Assert.Contains("Calvin Harris", ArtistNameShortener.Ladder(credit));

    [Fact]
    public void Ladder_gets_strictly_shorter_so_a_caller_can_stop_at_the_first_fit()
    {
        IReadOnlyList<string> ladder = ArtistNameShortener.Ladder(
            "Ariana Grande, Doja Cat, Megan Thee Stallion feat. Nicki Minaj");

        Assert.True(ladder.Count > 1);
        Assert.Equal(ladder, ladder.Distinct());
        Assert.Equal(ladder[0], ladder.OrderByDescending(r => r.Length).First());
        Assert.True(ladder[^1].Length < ladder[0].Length);
    }

    [Fact]
    public void Ladder_never_repeats_a_rung_when_the_feature_strip_changes_nothing()
    {
        IReadOnlyList<string> ladder = ArtistNameShortener.Ladder("Daft Punk");

        Assert.Single(ladder);
    }

    [Theory]
    [InlineData("Daft Punk")]
    [InlineData("Kraftwerk")]
    [InlineData("Shiftless")]
    [InlineData("Featherstone")]
    public void Ladder_does_not_mistake_letters_inside_a_name_for_a_feature_credit(string band)
        => Assert.Equal([band], ArtistNameShortener.Ladder(band));
}
