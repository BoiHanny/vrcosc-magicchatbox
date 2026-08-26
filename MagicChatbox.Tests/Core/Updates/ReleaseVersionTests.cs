using System;
using vrcosc_magicchatbox.Core.Updates;
using Xunit;

namespace MagicChatbox.Tests.Core.Updates;

public class ReleaseVersionTests
{
    private static readonly string[] Ladder =
    [
        "0.9.222",
        "v0.9.223-rc1",
        "0.9.223-rc2",
        "v0.9.223",
        "0.9.224",
        "0.10.0",
        "1.0.0",
        "1.0.1",
    ];

    private static void AssertAscending(string? lower, string? higher)
    {
        Assert.True(ReleaseVersion.Compare(lower, higher) < 0, $"expected {lower} < {higher}");
        Assert.True(ReleaseVersion.Compare(higher, lower) > 0, $"expected {higher} > {lower}");
        Assert.Equal(0, ReleaseVersion.Compare(lower, lower));
        Assert.Equal(0, ReleaseVersion.Compare(higher, higher));
    }

    private static void AssertEquivalent(string? left, string? right)
    {
        Assert.Equal(0, ReleaseVersion.Compare(left, right));
        Assert.Equal(0, ReleaseVersion.Compare(right, left));
    }

    [Theory]
    [InlineData("v0.9.223", "0.9.223")]
    [InlineData("0.9.223", "0.9.223")]
    [InlineData("v0.9.223-rc1", "0.9.223")]
    [InlineData("0.9.223-rc1", "0.9.223")]
    [InlineData("v0.9.223-rc1+build9", "0.9.223")]
    [InlineData("1.2.3+build9", "1.2.3")]
    [InlineData("  v1.2.3  ", "1.2.3")]
    [InlineData("v1.0", "1.0")]
    [InlineData("v1", "1")]
    [InlineData("1.0.0.0", "1.0.0.0")]
    public void Normalize_keeps_the_numbers_and_drops_everything_else(string tag, string expected)
    {
        Assert.Equal(expected, ReleaseVersion.Normalize(tag));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    [InlineData("not-a-version")]
    [InlineData("v")]
    [InlineData("version 1.2.3")]
    [InlineData("-rc1")]
    [InlineData("beta")]
    public void Normalize_returns_empty_for_anything_without_a_leading_number(string? tag)
    {
        Assert.Equal(string.Empty, ReleaseVersion.Normalize(tag));
    }

    [Fact]
    public void Normalize_is_idempotent()
    {
        string once = ReleaseVersion.Normalize("v0.9.223-rc1");

        Assert.Equal("0.9.223", once);
        Assert.Equal(once, ReleaseVersion.Normalize(once));
    }

    [Theory]
    [InlineData("0.9.223", "0.9.224")]
    [InlineData("0.9.9", "0.9.10")]
    [InlineData("0.9.223", "0.10.0")]
    [InlineData("0.99.99", "1.0.0")]
    [InlineData("1.2.3", "1.3.0")]
    [InlineData("v0.9.223", "v1.0.0")]
    public void Compare_orders_by_numeric_segments(string lower, string higher)
    {
        AssertAscending(lower, higher);
    }

    [Theory]
    [InlineData("1.0", "1.0.0")]
    [InlineData("1", "1.0.0")]
    [InlineData("1.0.0.0", "1.0.0")]
    [InlineData("v1.0", "1.0.0.0")]
    [InlineData("1.02.3", "1.2.3")]
    public void Compare_treats_missing_segments_as_zero(string left, string right)
    {
        AssertEquivalent(left, right);
    }

    [Fact]
    public void Compare_ignores_the_leading_v()
    {
        AssertEquivalent("v0.9.223", "0.9.223");
        AssertAscending("v0.9.223", "0.9.224");
    }

    [Fact]
    public void A_release_candidate_ranks_below_the_release_it_is_a_candidate_for()
    {
        // The old implementation stripped non-digits instead of splitting on the suffix, so
        // "v0.9.223-rc1" became "0.9.2231" and outranked the release it was a candidate for.
        Assert.Equal("0.9.223", ReleaseVersion.Normalize("v0.9.223-rc1"));

        Assert.True(ReleaseVersion.Compare("v0.9.223-rc1", "v0.9.223") < 0);
        Assert.False(ReleaseVersion.Compare("v0.9.223-rc1", "v0.9.223") > 0);
        Assert.True(ReleaseVersion.Compare("v0.9.223", "v0.9.223-rc1") > 0);

        AssertAscending("v0.9.222", "v0.9.223-rc1");
        AssertAscending("v0.9.223-rc1", "v0.9.224");
    }

    [Theory]
    [InlineData("1.0.0-alpha", "1.0.0-beta")]
    [InlineData("1.0.0-rc1", "1.0.0-rc2")]
    [InlineData("1.0.0-rc10", "1.0.0-rc2")]
    [InlineData("1.0.0.rc1", "1.0.0.rc2")]
    public void Two_suffixed_builds_of_the_same_version_compare_ordinally(string lower, string higher)
    {
        // Ordinal, not numeric: "rc10" sorts before "rc2" because '1' precedes '2'.
        AssertAscending(lower, higher);
    }

    [Theory]
    [InlineData("1.0.0-rc1", "1.0.0-rc1")]
    [InlineData("v1.0.0-rc1", "1.0.0-rc1")]
    [InlineData("1.0-rc1", "1.0.0-rc1")]
    public void Identical_suffixes_on_the_same_version_are_equal(string left, string right)
    {
        AssertEquivalent(left, right);
    }

    [Fact]
    public void Build_metadata_after_a_plus_is_ignored()
    {
        AssertEquivalent("1.2.3+build9", "1.2.3");
        AssertEquivalent("1.2.3+build9", "1.2.3+build10");
        AssertEquivalent("1.2.3-rc1+build9", "1.2.3-rc1");
        AssertAscending("1.2.3-rc1+build9", "1.2.3+build9");
    }

    [Fact]
    public void Compare_handles_null_and_empty_on_either_side()
    {
        Assert.Equal(0, ReleaseVersion.Compare(null, null));
        AssertEquivalent(null, string.Empty);
        AssertEquivalent(null, "   ");
        AssertEquivalent(string.Empty, "not-a-version");

        AssertAscending(null, "1.0.0");
        AssertAscending(string.Empty, "0.0.1");
        AssertAscending("not-a-version", "0.0.1");

        // An unreadable version carries no numbers at all, which puts it level with 0.0.0.
        AssertEquivalent(null, "0.0.0");
    }

    [Fact]
    public void Compare_is_antisymmetric_across_every_pairing()
    {
        foreach (string left in Ladder)
        {
            foreach (string right in Ladder)
            {
                int forward = Math.Sign(ReleaseVersion.Compare(left, right));
                int backward = Math.Sign(ReleaseVersion.Compare(right, left));

                Assert.True(forward == -backward, $"{left} vs {right} was not antisymmetric");
            }
        }
    }

    [Fact]
    public void Compare_keeps_the_whole_ladder_in_order()
    {
        for (int i = 0; i < Ladder.Length; i++)
        {
            for (int j = i + 1; j < Ladder.Length; j++)
            {
                AssertAscending(Ladder[i], Ladder[j]);
            }
        }
    }

    [Fact]
    public void Sorting_with_Compare_reproduces_the_ladder()
    {
        string[] shuffled = [.. Ladder];
        Array.Reverse(shuffled);

        Array.Sort(shuffled, ReleaseVersion.Compare);

        Assert.Equal(Ladder, shuffled);
    }
}
