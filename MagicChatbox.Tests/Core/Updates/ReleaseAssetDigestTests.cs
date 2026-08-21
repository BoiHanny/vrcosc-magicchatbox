using System;
using System.IO;
using System.Text;
using vrcosc_magicchatbox.Core.Updates;
using Xunit;

namespace MagicChatbox.Tests.Core.Updates;

public class ReleaseAssetDigestTests : IDisposable
{
    private readonly string _tempDirectory;

    public ReleaseAssetDigestTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "mcb-digest-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDirectory, true);
        }
        catch (IOException)
        {
        }
    }

    private string WriteFile(string name, string content)
    {
        string path = Path.Combine(_tempDirectory, name);
        File.WriteAllText(path, content, new UTF8Encoding(false));
        return path;
    }

    // The GitHub releases API returns the asset checksum in this exact shape.
    private const string GitHubStyleDigest =
        "sha256:772388f6727f65de18e2ad939fdf8b5f0029547284b537d2eb6783793057ee26";

    private const string BareHex =
        "772388f6727f65de18e2ad939fdf8b5f0029547284b537d2eb6783793057ee26";

    [Fact]
    public void TryParse_accepts_the_prefixed_form_the_api_returns()
    {
        Assert.True(ReleaseAssetDigest.TryParse(GitHubStyleDigest, out var digest));
        Assert.Equal(BareHex, digest.Sha256Hex);
    }

    [Fact]
    public void TryParse_accepts_a_bare_hex_digest()
    {
        Assert.True(ReleaseAssetDigest.TryParse(BareHex, out var digest));
        Assert.Equal(BareHex, digest.Sha256Hex);
    }

    [Fact]
    public void TryParse_normalizes_case_and_surrounding_whitespace()
    {
        Assert.True(ReleaseAssetDigest.TryParse("  SHA256:" + BareHex.ToUpperInvariant() + "  ", out var digest));
        Assert.Equal(BareHex, digest.Sha256Hex);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sha256:")]
    [InlineData("sha512:772388f6727f65de18e2ad939fdf8b5f0029547284b537d2eb6783793057ee26")]
    [InlineData("772388f6727f65de18e2ad939fdf8b5f0029547284b537d2eb6783793057ee")]
    [InlineData("772388f6727f65de18e2ad939fdf8b5f0029547284b537d2eb6783793057ee26aa")]
    [InlineData("772388f6727f65de18e2ad939fdf8b5f0029547284b537d2eb6783793057eeZZ")]
    public void TryParse_rejects_anything_that_is_not_a_sha256(string? value)
    {
        Assert.False(ReleaseAssetDigest.TryParse(value, out var digest));
        Assert.Equal(default, digest);
    }

    [Fact]
    public void TryParse_rejects_a_sha512_digest_even_though_it_is_valid_hex()
    {
        string sha512 = new string('a', 128);
        Assert.False(ReleaseAssetDigest.TryParse("sha512:" + sha512, out _));
    }

    [Fact]
    public void ComputeSha256_matches_a_known_vector()
    {
        // SHA-256 of the empty input.
        using var empty = new MemoryStream(Array.Empty<byte>());
        Assert.Equal(
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            ReleaseAssetDigest.ComputeSha256(empty));
    }

    [Fact]
    public void Verify_reports_a_match_for_an_untouched_file()
    {
        string path = WriteFile("payload.zip", "the real release");
        string actual = ReleaseAssetDigest.ComputeSha256File(path);

        var result = ReleaseAssetDigest.Verify("sha256:" + actual, path);

        Assert.Equal(DigestVerificationStatus.Match, result.Status);
        Assert.False(result.IsRejected);
        Assert.Equal(actual, result.Actual);
    }

    [Fact]
    public void Verify_rejects_a_file_whose_bytes_changed()
    {
        string good = WriteFile("good.zip", "the real release");
        string tampered = WriteFile("tampered.zip", "the real release ");
        string expected = ReleaseAssetDigest.ComputeSha256File(good);

        var result = ReleaseAssetDigest.Verify("sha256:" + expected, tampered);

        Assert.Equal(DigestVerificationStatus.Mismatch, result.Status);
        Assert.True(result.IsRejected);
        Assert.Equal(expected, result.Expected);
        Assert.NotEqual(expected, result.Actual);
    }

    [Fact]
    public void Verify_falls_back_to_not_published_when_no_digest_is_supplied()
    {
        string path = WriteFile("payload.zip", "an older release predating asset digests");

        var result = ReleaseAssetDigest.Verify(null, path);

        Assert.Equal(DigestVerificationStatus.NotPublished, result.Status);
        Assert.False(result.IsRejected);
    }

    [Fact]
    public void Verify_treats_an_unparseable_digest_as_not_published_rather_than_a_mismatch()
    {
        string path = WriteFile("payload.zip", "content");

        var result = ReleaseAssetDigest.Verify("md5:d41d8cd98f00b204e9800998ecf8427e", path);

        Assert.Equal(DigestVerificationStatus.NotPublished, result.Status);
    }

    [Fact]
    public void Verify_does_not_read_the_file_when_there_is_nothing_to_verify_against()
    {
        string missing = Path.Combine(_tempDirectory, "does-not-exist.zip");

        var result = ReleaseAssetDigest.Verify(string.Empty, missing);

        Assert.Equal(DigestVerificationStatus.NotPublished, result.Status);
    }

    [Fact]
    public void Matches_is_case_insensitive()
    {
        Assert.True(ReleaseAssetDigest.TryParse(GitHubStyleDigest, out var digest));

        Assert.True(digest.Matches(BareHex.ToUpperInvariant()));
        Assert.False(digest.Matches(null));
        Assert.False(digest.Matches("  "));
    }

    [Fact]
    public void ToString_round_trips_through_TryParse()
    {
        Assert.True(ReleaseAssetDigest.TryParse(GitHubStyleDigest, out var digest));
        Assert.True(ReleaseAssetDigest.TryParse(digest.ToString(), out var again));

        Assert.Equal(digest, again);
        Assert.Equal(GitHubStyleDigest, digest.ToString());
    }
}
