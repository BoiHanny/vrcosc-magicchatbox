using System;
using System.Collections.Generic;
using System.IO;
using vrcosc_magicchatbox.Core.Updates;
using Xunit;

namespace MagicChatbox.Tests.Core.Updates;

public class UpdateStateStoreTests : IDisposable
{
    private static readonly DateTimeOffset StagedAt = new(2026, 8, 22, 13, 45, 12, 340, TimeSpan.Zero);

    private readonly string _root;

    public UpdateStateStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "mcb-updatestate-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, true);
        }
        catch (IOException)
        {
        }
    }

    private string NewProfile()
    {
        string dir = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private string MissingProfile() => Path.Combine(_root, "never-created-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(UpdateChannel.Stable)]
    [InlineData(UpdateChannel.PreRelease)]
    public void A_staged_update_reads_back_field_for_field(UpdateChannel channel)
    {
        string dir = NewProfile();
        var written = new PendingUpdateInfo(
            "0.9.231",
            channel,
            Path.Combine(dir, "staged", "MagicChatbox.zip"),
            "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08",
            StagedAt);

        Assert.True(PendingUpdate.Write(dir, written));

        PendingUpdateInfo? read = PendingUpdate.Read(dir);

        Assert.NotNull(read);
        Assert.Equal(written.Version, read.Version);
        Assert.Equal(written.Channel, read.Channel);
        Assert.Equal(written.StagedPath, read.StagedPath);
        Assert.Equal(written.Sha256, read.Sha256);
    }

    [Fact]
    public void The_staging_time_survives_the_round_trip()
    {
        // Newtonsoft turns an ISO timestamp back into a Date token while parsing, so reading it
        // as a string hands back a localised "MM/dd/yyyy HH:mm:ss" rendering instead of what was
        // written. Outside a US-style locale that no longer parses at all and the staging time
        // silently becomes DateTimeOffset.MinValue.
        string dir = NewProfile();
        PendingUpdate.Write(
            dir,
            new PendingUpdateInfo("0.9.231", UpdateChannel.Stable, "staged.zip", "abc", StagedAt));

        PendingUpdateInfo? read = PendingUpdate.Read(dir);

        Assert.NotNull(read);
        Assert.Equal(StagedAt, read.StagedAtUtc);
    }

    [Fact]
    public void Nothing_is_pending_when_no_record_was_ever_written()
    {
        Assert.Null(PendingUpdate.Read(NewProfile()));
        Assert.Null(PendingUpdate.Read(MissingProfile()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ not json")]
    [InlineData("[]")]
    [InlineData("half a file, truncated by a crash mid-w")]
    public void A_damaged_record_reads_as_nothing_pending(string contents)
    {
        string dir = NewProfile();
        File.WriteAllText(PendingUpdate.PathFor(dir), contents);

        Assert.Null(PendingUpdate.Read(dir));
    }

    [Fact]
    public void Clear_removes_the_record()
    {
        string dir = NewProfile();
        PendingUpdate.Write(dir, new PendingUpdateInfo("0.9.231", UpdateChannel.Stable, "staged.zip", "abc", DateTimeOffset.UtcNow));
        Assert.NotNull(PendingUpdate.Read(dir));

        PendingUpdate.Clear(dir);

        Assert.Null(PendingUpdate.Read(dir));
        Assert.False(File.Exists(PendingUpdate.PathFor(dir)));
    }

    [Fact]
    public void Clear_with_nothing_to_clear_is_harmless()
    {
        PendingUpdate.Clear(NewProfile());
        PendingUpdate.Clear(MissingProfile());
    }

    [Theory]
    [InlineData("Nightly")]
    [InlineData("")]
    [InlineData(null)]
    public void An_unknown_channel_falls_back_to_stable(string? channel)
    {
        // The channel is stored by name, so a record written by a build that knew more channels
        // than this one must still be usable rather than dropped.
        string dir = NewProfile();
        string encoded = channel is null ? "null" : "\"" + channel + "\"";
        File.WriteAllText(
            PendingUpdate.PathFor(dir),
            "{ \"version\": \"0.9.231\", \"channel\": " + encoded + ", \"stagedPath\": \"staged.zip\", \"sha256\": \"abc\" }");

        PendingUpdateInfo? read = PendingUpdate.Read(dir);

        Assert.NotNull(read);
        Assert.Equal(UpdateChannel.Stable, read.Channel);
        Assert.Equal("0.9.231", read.Version);
    }

    [Fact]
    public void The_first_start_on_a_clean_profile_reports_no_earlier_failure()
    {
        StartupHealthCheck check = StartupHealthBeacon.MarkStarting(NewProfile());

        Assert.False(check.PreviousStartFailed);
        Assert.Equal(0, check.ConsecutiveFailures);
        Assert.False(check.WasAutoInstalled);
    }

    [Fact]
    public void A_run_that_reached_healthy_resets_the_streak()
    {
        string dir = NewProfile();

        StartupHealthBeacon.MarkStarting(dir);
        StartupHealthBeacon.MarkStarting(dir);
        StartupHealthBeacon.MarkHealthy(dir);

        StartupHealthCheck check = StartupHealthBeacon.MarkStarting(dir);

        Assert.False(check.PreviousStartFailed);
        Assert.Equal(0, check.ConsecutiveFailures);
    }

    [Fact]
    public void Starts_that_never_reach_healthy_count_up_one_at_a_time()
    {
        // The rollback fires at three consecutive failures, so the exact sequence matters:
        // an off-by-one here either rolls a working build back or lets a crash loop run forever.
        string dir = NewProfile();

        StartupHealthCheck first = StartupHealthBeacon.MarkStarting(dir);
        Assert.False(first.PreviousStartFailed);
        Assert.Equal(0, first.ConsecutiveFailures);

        StartupHealthCheck second = StartupHealthBeacon.MarkStarting(dir);
        Assert.True(second.PreviousStartFailed);
        Assert.Equal(1, second.ConsecutiveFailures);

        StartupHealthCheck third = StartupHealthBeacon.MarkStarting(dir);
        Assert.True(third.PreviousStartFailed);
        Assert.Equal(2, third.ConsecutiveFailures);

        StartupHealthCheck fourth = StartupHealthBeacon.MarkStarting(dir);
        Assert.True(fourth.PreviousStartFailed);
        Assert.Equal(3, fourth.ConsecutiveFailures);
    }

    [Fact]
    public void An_auto_installed_version_is_reported_on_the_next_start()
    {
        string dir = NewProfile();

        StartupHealthBeacon.RecordAutoInstall(dir, "0.9.231");
        StartupHealthCheck check = StartupHealthBeacon.MarkStarting(dir);

        Assert.True(check.WasAutoInstalled);
        Assert.Equal("0.9.231", check.AutoInstalledVersion);
    }

    [Fact]
    public void Probation_ends_once_a_build_starts_cleanly()
    {
        string dir = NewProfile();

        StartupHealthBeacon.RecordAutoInstall(dir, "0.9.231");
        Assert.True(StartupHealthBeacon.MarkStarting(dir).WasAutoInstalled);

        StartupHealthBeacon.MarkHealthy(dir);
        StartupHealthCheck check = StartupHealthBeacon.MarkStarting(dir);

        Assert.False(check.WasAutoInstalled);
        Assert.Equal(string.Empty, check.AutoInstalledVersion);
        Assert.False(check.PreviousStartFailed);
    }

    [Fact]
    public void An_auto_install_recorded_mid_streak_keeps_the_streak()
    {
        string dir = NewProfile();

        StartupHealthBeacon.MarkStarting(dir);
        StartupHealthBeacon.RecordAutoInstall(dir, "0.9.231");

        StartupHealthCheck check = StartupHealthBeacon.MarkStarting(dir);

        Assert.True(check.PreviousStartFailed);
        Assert.Equal(1, check.ConsecutiveFailures);
        Assert.Equal("0.9.231", check.AutoInstalledVersion);
    }

    [Fact]
    public void A_beacon_that_was_never_written_reads_clean()
    {
        Assert.Equal(StartupHealth.Clean, StartupHealthBeacon.Read(NewProfile()));
        Assert.Equal(StartupHealth.Clean, StartupHealthBeacon.Read(MissingProfile()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ not json")]
    [InlineData("[]")]
    public void A_damaged_beacon_reads_clean_rather_than_throwing(string contents)
    {
        string dir = NewProfile();
        File.WriteAllText(StartupHealthBeacon.PathFor(dir), contents);

        Assert.Equal(StartupHealth.Clean, StartupHealthBeacon.Read(dir));
    }

    [Fact]
    public void A_clean_profile_blocks_nothing()
    {
        Assert.Empty(UpdateBlocklist.Read(NewProfile()));
        Assert.Empty(UpdateBlocklist.Read(MissingProfile()));
    }

    [Fact]
    public void A_blocked_version_reads_back()
    {
        string dir = NewProfile();

        Assert.Equal(new[] { "0.9.231" }, UpdateBlocklist.Add(dir, "0.9.231"));
        Assert.Equal(new[] { "0.9.231" }, UpdateBlocklist.Read(dir));
    }

    [Theory]
    [InlineData("0.9.231", "0.9.231")]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.2.3", "v1.2.3")]
    public void The_same_release_is_never_blocked_twice(string first, string second)
    {
        // Tags reach this list from several places, some with the leading v and some without,
        // so sameness has to be judged by version rather than by string.
        string dir = NewProfile();

        UpdateBlocklist.Add(dir, first);
        UpdateBlocklist.Add(dir, second);

        Assert.Equal(new[] { first }, UpdateBlocklist.Read(dir));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_version_is_ignored(string? version)
    {
        string dir = NewProfile();

        Assert.Empty(UpdateBlocklist.Add(dir, version!));
        Assert.Empty(UpdateBlocklist.Read(dir));
        Assert.False(File.Exists(UpdateBlocklist.PathFor(dir)));
    }

    [Fact]
    public void The_blocklist_keeps_the_most_recent_entries_and_no_more()
    {
        string dir = NewProfile();

        for (int i = 1; i <= 25; i++)
        {
            UpdateBlocklist.Add(dir, "1.0." + i);
        }

        IReadOnlyList<string> blocked = UpdateBlocklist.Read(dir);

        Assert.Equal(20, blocked.Count);
        Assert.Equal("1.0.6", blocked[0]);
        Assert.Equal("1.0.25", blocked[^1]);
        Assert.DoesNotContain("1.0.5", blocked);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("[ not json")]
    [InlineData("{}")]
    public void A_damaged_blocklist_blocks_nothing(string contents)
    {
        string dir = NewProfile();
        File.WriteAllText(UpdateBlocklist.PathFor(dir), contents);

        Assert.Empty(UpdateBlocklist.Read(dir));
    }
}
