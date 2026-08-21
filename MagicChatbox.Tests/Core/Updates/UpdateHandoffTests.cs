using System;
using System.IO;
using vrcosc_magicchatbox.Core.Updates;
using Xunit;

namespace MagicChatbox.Tests.Core.Updates;

public class UpdateHandoffTests : IDisposable
{
    private readonly string _dir;

    public UpdateHandoffTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "mcb-handoff-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, true);
        }
        catch (IOException)
        {
        }
    }

    private const string Sha =
        "772388f6727f65de18e2ad939fdf8b5f0029547284b537d2eb6783793057ee26";

    [Fact]
    public void A_verified_update_survives_the_trip_to_the_other_process()
    {
        var written = new UpdateHandoffInfo("0.9.222", DigestVerificationStatus.Match, Sha, IsRollback: false);
        UpdateHandoff.Write(_dir, written);

        UpdateHandoffInfo? read = UpdateHandoff.Read(_dir);

        Assert.NotNull(read);
        Assert.Equal(written, read!.Value);
    }

    [Fact]
    public void A_rollback_survives_the_trip_too()
    {
        var written = new UpdateHandoffInfo("0.9.220", DigestVerificationStatus.NotPublished, string.Empty, IsRollback: true);
        UpdateHandoff.Write(_dir, written);

        Assert.Equal(written, UpdateHandoff.Read(_dir)!.Value);
    }

    [Fact]
    public void Reading_a_directory_with_no_handoff_returns_nothing()
    {
        Assert.Null(UpdateHandoff.Read(_dir));
    }

    [Fact]
    public void A_corrupt_handoff_is_ignored_rather_than_throwing_during_startup()
    {
        File.WriteAllText(Path.Combine(_dir, UpdateHandoff.FileName), "{ not json");

        Assert.Null(UpdateHandoff.Read(_dir));
    }

    [Fact]
    public void An_unrecognised_integrity_value_degrades_to_unverified()
    {
        File.WriteAllText(
            Path.Combine(_dir, UpdateHandoff.FileName),
            "{ \"targetVersion\": \"0.9.222\", \"integrity\": \"Sideways\", \"sha256\": \"\" }");

        UpdateHandoffInfo? read = UpdateHandoff.Read(_dir);

        Assert.NotNull(read);
        Assert.Equal(DigestVerificationStatus.NotPublished, read!.Value.Integrity);
    }

    [Fact]
    public void Clear_removes_the_file_and_is_safe_to_call_twice()
    {
        UpdateHandoff.Write(_dir, new UpdateHandoffInfo("0.9.222", DigestVerificationStatus.Match, Sha, false));

        UpdateHandoff.Clear(_dir);
        UpdateHandoff.Clear(_dir);

        Assert.Null(UpdateHandoff.Read(_dir));
    }

    [Fact]
    public void The_short_hash_is_what_the_card_shows()
    {
        var info = new UpdateHandoffInfo("0.9.222", DigestVerificationStatus.Match, Sha, false);

        Assert.Equal("772388f6727f", info.ShortHash);
    }

    [Fact]
    public void The_short_hash_copes_with_nothing_to_shorten()
    {
        var info = new UpdateHandoffInfo("0.9.222", DigestVerificationStatus.NotPublished, string.Empty, false);

        Assert.Equal(string.Empty, info.ShortHash);
    }

    [Theory]
    [InlineData(DigestVerificationStatus.Match, "Valid package from the developer")]
    [InlineData(DigestVerificationStatus.NotPublished, "No checksum published for this release")]
    [InlineData(DigestVerificationStatus.Mismatch, "Integrity check failed")]
    public void The_integrity_line_never_overstates_what_was_checked(
        DigestVerificationStatus status,
        string expected)
    {
        var info = new UpdateHandoffInfo("0.9.222", status, Sha, false);

        Assert.Equal(expected, info.IntegrityLine);
    }
}
