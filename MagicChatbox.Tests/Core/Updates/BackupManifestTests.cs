using System;
using System.IO;
using vrcosc_magicchatbox.Core.Updates;
using Xunit;

namespace MagicChatbox.Tests.Core.Updates;

public class BackupManifestTests : IDisposable
{
    private const string ExecutableName = "MagicChatbox.exe";

    private readonly string _root;

    public BackupManifestTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "mcb-backup-tests-" + Guid.NewGuid().ToString("N"));
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

    private string NewBackup(int extraFiles = 2, bool includeExecutable = true)
    {
        string dir = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        if (includeExecutable)
        {
            File.WriteAllText(Path.Combine(dir, ExecutableName), "exe");
        }

        for (int i = 0; i < extraFiles; i++)
        {
            File.WriteAllText(Path.Combine(dir, $"file{i}.dll"), new string('x', 10 + i));
        }

        Directory.CreateDirectory(Path.Combine(dir, "nested"));
        File.WriteAllText(Path.Combine(dir, "nested", "deep.json"), "{}");

        return dir;
    }

    [Fact]
    public void A_freshly_written_manifest_verifies()
    {
        string dir = NewBackup();
        BackupManifest.Write(dir, "0.9.221", DateTimeOffset.UtcNow);

        BackupCheck check = BackupManifest.Verify(dir, ExecutableName);

        Assert.Equal(BackupIntegrity.Trusted, check.Integrity);
        Assert.True(check.IsTrusted);
        Assert.Contains("0.9.221", check.Description);
    }

    [Fact]
    public void The_manifest_does_not_count_itself()
    {
        string dir = NewBackup();
        (int before, long beforeBytes) = BackupManifest.Measure(dir);

        BackupManifest.Write(dir, "0.9.221", DateTimeOffset.UtcNow);
        (int after, long afterBytes) = BackupManifest.Measure(dir);

        Assert.Equal(before, after);
        Assert.Equal(beforeBytes, afterBytes);
    }

    [Fact]
    public void A_backup_missing_a_file_is_reported_incomplete()
    {
        string dir = NewBackup();
        BackupManifest.Write(dir, "0.9.221", DateTimeOffset.UtcNow);

        File.Delete(Path.Combine(dir, "file0.dll"));

        BackupCheck check = BackupManifest.Verify(dir, ExecutableName);

        Assert.Equal(BackupIntegrity.Incomplete, check.Integrity);
        Assert.False(check.IsTrusted);
    }

    [Fact]
    public void A_backup_whose_files_changed_size_is_reported_incomplete()
    {
        string dir = NewBackup();
        BackupManifest.Write(dir, "0.9.221", DateTimeOffset.UtcNow);

        File.WriteAllText(Path.Combine(dir, "file0.dll"), "truncated");

        BackupCheck check = BackupManifest.Verify(dir, ExecutableName);

        Assert.Equal(BackupIntegrity.Incomplete, check.Integrity);
    }

    [Fact]
    public void An_extra_file_also_counts_as_a_mismatch()
    {
        string dir = NewBackup();
        BackupManifest.Write(dir, "0.9.221", DateTimeOffset.UtcNow);

        File.WriteAllText(Path.Combine(dir, "stowaway.dll"), "surprise");

        Assert.Equal(BackupIntegrity.Incomplete, BackupManifest.Verify(dir, ExecutableName).Integrity);
    }

    [Fact]
    public void A_backup_without_the_executable_is_missing_rather_than_incomplete()
    {
        string dir = NewBackup(includeExecutable: false);
        BackupManifest.Write(dir, "0.9.221", DateTimeOffset.UtcNow);

        Assert.Equal(BackupIntegrity.Missing, BackupManifest.Verify(dir, ExecutableName).Integrity);
    }

    [Fact]
    public void A_directory_that_does_not_exist_is_missing()
    {
        string dir = Path.Combine(_root, "nope");

        Assert.Equal(BackupIntegrity.Missing, BackupManifest.Verify(dir, ExecutableName).Integrity);
    }

    [Fact]
    public void A_backup_from_before_manifests_is_unreadable_not_incomplete()
    {
        // Older versions wrote no manifest. Those backups must stay usable, so this case
        // has to stay distinguishable from a backup that is genuinely damaged.
        string dir = NewBackup();

        BackupCheck check = BackupManifest.Verify(dir, ExecutableName);

        Assert.Equal(BackupIntegrity.Unreadable, check.Integrity);
        Assert.False(check.IsTrusted);
    }

    [Fact]
    public void A_corrupt_manifest_is_unreadable()
    {
        string dir = NewBackup();
        File.WriteAllText(Path.Combine(dir, BackupManifest.FileName), "{ not json");

        Assert.Equal(BackupIntegrity.Unreadable, BackupManifest.Verify(dir, ExecutableName).Integrity);
    }

    [Fact]
    public void A_manifest_without_totals_is_unreadable()
    {
        string dir = NewBackup();
        File.WriteAllText(Path.Combine(dir, BackupManifest.FileName), "{ \"appVersion\": \"0.9.221\" }");

        Assert.Equal(BackupIntegrity.Unreadable, BackupManifest.Verify(dir, ExecutableName).Integrity);
    }

    [Fact]
    public void ReadVersion_returns_the_recorded_version()
    {
        string dir = NewBackup();
        BackupManifest.Write(dir, "0.9.221", DateTimeOffset.UtcNow);

        Assert.Equal("0.9.221", BackupManifest.ReadVersion(dir));
    }

    [Fact]
    public void ReadVersion_returns_null_when_there_is_nothing_recorded()
    {
        string dir = NewBackup();
        Assert.Null(BackupManifest.ReadVersion(dir));

        BackupManifest.Write(dir, null, DateTimeOffset.UtcNow);
        Assert.Null(BackupManifest.ReadVersion(dir));
    }

    [Fact]
    public void Writing_twice_replaces_the_manifest_rather_than_stacking_temp_files()
    {
        string dir = NewBackup();
        BackupManifest.Write(dir, "0.9.221", DateTimeOffset.UtcNow);
        BackupManifest.Write(dir, "0.9.222", DateTimeOffset.UtcNow);

        Assert.Equal("0.9.222", BackupManifest.ReadVersion(dir));
        Assert.False(File.Exists(Path.Combine(dir, BackupManifest.FileName + ".tmp")));
        Assert.Equal(BackupIntegrity.Trusted, BackupManifest.Verify(dir, ExecutableName).Integrity);
    }
}
