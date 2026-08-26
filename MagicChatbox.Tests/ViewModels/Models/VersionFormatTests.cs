using System;
using Xunit;
using AppVersion = vrcosc_magicchatbox.ViewModels.Models.Version;

namespace MagicChatbox.Tests.ViewModels.Models;

public class VersionFormatTests
{
    [Theory]
    [InlineData("0.9.223", "0.9.223")]
    [InlineData("0.9.7", "0.9.007")]
    [InlineData("0.9.07", "0.9.007")]
    [InlineData("0.10.99", "0.10.099")]
    public void A_three_segment_version_keeps_its_shape_with_the_build_padded(string input, string expected)
    {
        Assert.Equal(expected, new AppVersion(input).VersionNumber);
    }

    [Theory]
    [InlineData("1.9.223")]
    [InlineData("9.9.223")]
    [InlineData("0.9.223")]
    public void The_leading_segment_is_always_forced_to_zero(string input)
    {
        Assert.Equal("0.9.223", new AppVersion(input).VersionNumber);
    }

    [Theory]
    [InlineData("0.9.223.4", "0.9.223")]
    [InlineData("1.9.223.4.5.6", "0.9.223")]
    public void Anything_past_the_third_segment_is_dropped(string input, string expected)
    {
        Assert.Equal(expected, new AppVersion(input).VersionNumber);
    }

    [Fact]
    public void A_two_segment_version_no_longer_takes_the_update_check_down()
    {
        // A tag is not obliged to carry three segments. The missing one used to reach int.Parse
        // as a null and throw; it now reads as the zero it means.
        var exception = Record.Exception(() => new AppVersion("1.0"));

        Assert.Null(exception);
        Assert.Equal("0.0.000", new AppVersion("1.0").VersionNumber);
    }

    [Theory]
    [InlineData("1", "0.0.000")]
    [InlineData("2", "0.0.000")]
    [InlineData("", "0.0.000")]
    public void A_one_segment_version_and_an_empty_string_survive_too(string input, string expected)
    {
        var exception = Record.Exception(() => new AppVersion(input));

        Assert.Null(exception);
        Assert.Equal(expected, new AppVersion(input).VersionNumber);
    }

    [Fact]
    public void A_null_version_is_read_as_all_zeroes()
    {
        var exception = Record.Exception(() => new AppVersion(null!));

        Assert.Null(exception);
        Assert.Equal("0.0.000", new AppVersion(null!).VersionNumber);
    }

    [Theory]
    [InlineData("0.beta.223", "0.0.223")]
    [InlineData("0.9.beta", "0.9.000")]
    [InlineData("v0.9.223", "0.9.223")]
    [InlineData("0.9.223-rc1", "0.9.000")]
    public void A_segment_that_is_not_a_number_reads_as_zero(string input, string expected)
    {
        // The leading segment is overwritten wholesale, so "v0" never has to parse - but a
        // suffix on the build number is not salvaged, it collapses to zero.
        Assert.Equal(expected, new AppVersion(input).VersionNumber);
    }

    [Fact]
    public void A_build_number_wider_than_three_digits_is_left_alone()
    {
        // PadLeft only ever adds, so a four-digit build is not truncated to three.
        Assert.Equal("0.9.1234", new AppVersion("0.9.1234").VersionNumber);
    }

    [Fact]
    public void Setting_the_property_normalises_exactly_like_the_constructor()
    {
        var version = new AppVersion("0.9.223");

        version.VersionNumber = "1.9.7";

        Assert.Equal("0.9.007", version.VersionNumber);
    }

    [Fact]
    public void Setting_the_property_to_a_short_version_does_not_throw_either()
    {
        var version = new AppVersion("0.9.223");

        var exception = Record.Exception(() => version.VersionNumber = "1.0");

        Assert.Null(exception);
        Assert.Equal("0.0.000", version.VersionNumber);
    }

    [Fact]
    public void The_release_details_start_empty_rather_than_null()
    {
        var version = new AppVersion("0.9.223");

        Assert.Equal(string.Empty, version.ReleaseDate);
        Assert.Equal(string.Empty, version.ReleaseNotes);
    }
}
