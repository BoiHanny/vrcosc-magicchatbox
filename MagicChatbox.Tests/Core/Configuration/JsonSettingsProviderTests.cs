using System;
using System.IO;
using Newtonsoft.Json;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Services;
using Xunit;

namespace MagicChatbox.Tests.Core.Configuration;

public sealed class JsonSettingsProviderTests : IDisposable
{
    private sealed class TempEnvironment : IEnvironmentService
    {
        public TempEnvironment(string root) => DataPath = root;
        public string DataPath { get; }
        public string LogPath => Path.Combine(DataPath, "logs");
        public string VrcPath => DataPath;
        public void SetCustomProfile(int profileNumber) => throw new NotSupportedException();
    }

    private readonly string _dir;
    private readonly TempEnvironment _env;

    private string SettingsFile => Path.Combine(_dir, $"{nameof(PulsoidModuleSettings)}.json");

    public JsonSettingsProviderTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "MagicChatboxTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _env = new TempEnvironment(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void Dispose_WithoutReadingValue_DoesNotCreateFile()
    {
        var provider = new JsonSettingsProvider<PulsoidModuleSettings>(_env);

        provider.Dispose();

        Assert.False(File.Exists(SettingsFile));
    }

    [Fact]
    public void Dispose_WithoutReadingValue_DoesNotClobberExistingFile()
    {
        // Regression test for the Pulsoid token loss bug: at shutdown PulsoidModule saved
        // good settings, then the DI container disposed a JsonSettingsProvider whose Value
        // was never read — which overwrote the file with the literal text "null".
        var saved = new PulsoidModuleSettings { CurrentHeartRateTitle = "Keep me" };
        File.WriteAllText(SettingsFile, JsonConvert.SerializeObject(saved, Formatting.Indented));
        string before = File.ReadAllText(SettingsFile);

        var provider = new JsonSettingsProvider<PulsoidModuleSettings>(_env);
        provider.Dispose();

        Assert.Equal(before, File.ReadAllText(SettingsFile));
    }

    [Fact]
    public void FlushPendingSave_WithoutReadingValue_DoesNotCreateFile()
    {
        var provider = new JsonSettingsProvider<PulsoidModuleSettings>(_env);

        provider.FlushPendingSave();
        provider.Dispose();

        Assert.False(File.Exists(SettingsFile));
    }

    [Fact]
    public void Dispose_AfterReadingValue_PersistsSettings()
    {
        var provider = new JsonSettingsProvider<PulsoidModuleSettings>(_env);
        provider.Value.CurrentHeartRateTitle = "Persisted";

        provider.Dispose();

        var roundTripped = JsonConvert.DeserializeObject<PulsoidModuleSettings>(File.ReadAllText(SettingsFile));
        Assert.NotNull(roundTripped);
        Assert.Equal("Persisted", roundTripped.CurrentHeartRateTitle);
    }

    [Fact]
    public void Value_WhenFileContainsNullLiteral_ReturnsDefaults()
    {
        // Files damaged by the dispose-null bug contain the literal text "null";
        // loading one must fall back to defaults instead of returning null.
        File.WriteAllText(SettingsFile, "null");

        var provider = new JsonSettingsProvider<PulsoidModuleSettings>(_env);

        Assert.NotNull(provider.Value);
        provider.Dispose();
    }

    [Fact]
    public void Value_WhenFileIsNulFilled_ReturnsDefaults()
    {
        // A hard power loss can leave the file NUL-filled (allocated but never flushed).
        File.WriteAllText(SettingsFile, new string('\0', 64));

        var provider = new JsonSettingsProvider<PulsoidModuleSettings>(_env);

        Assert.NotNull(provider.Value);
        provider.Dispose();
    }

    [Fact]
    public void FlushPendingSave_AfterReadingValue_PersistsLatestValues()
    {
        var provider = new JsonSettingsProvider<PulsoidModuleSettings>(_env);
        provider.Value.CurrentHeartRateTitle = "Flushed";

        provider.FlushPendingSave();

        var roundTripped = JsonConvert.DeserializeObject<PulsoidModuleSettings>(File.ReadAllText(SettingsFile));
        Assert.NotNull(roundTripped);
        Assert.Equal("Flushed", roundTripped.CurrentHeartRateTitle);
        provider.Dispose();
    }

    [Fact]
    public void Save_StampsAppVersionAndSchemaVersion()
    {
        var provider = new JsonSettingsProvider<PulsoidModuleSettings>(_env);
        _ = provider.Value;

        provider.FlushPendingSave();
        provider.Dispose();

        var roundTripped = JsonConvert.DeserializeObject<PulsoidModuleSettings>(File.ReadAllText(SettingsFile));
        Assert.NotNull(roundTripped);
        Assert.False(string.IsNullOrEmpty(AppVersion.Current));
        Assert.Equal(AppVersion.Current, roundTripped.AppVersion);
        Assert.Equal(1, roundTripped.SchemaVersion);
    }

    [Fact]
    public void Value_WhenFileIsLockedDuringLoad_DoesNotQuarantineOrOverwriteFile()
    {
        // A transiently locked file (AV scanner, backup/sync tool) is an IO failure,
        // not corruption: the provider must fall back to in-memory defaults without
        // renaming the intact file to .corrupt-* — and must never save those defaults
        // over the intact file, not even at dispose.
        var saved = new PulsoidModuleSettings { CurrentHeartRateTitle = "Keep me" };
        File.WriteAllText(SettingsFile, JsonConvert.SerializeObject(saved, Formatting.Indented));
        string before = File.ReadAllText(SettingsFile);

        var provider = new JsonSettingsProvider<PulsoidModuleSettings>(_env);
        using (new FileStream(SettingsFile, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.NotNull(provider.Value);
        }

        Assert.Empty(Directory.GetFiles(_dir, "*.corrupt-*"));

        provider.Value.CurrentHeartRateTitle = "In-memory only";
        provider.Dispose();

        Assert.Equal(before, File.ReadAllText(SettingsFile));
    }

    [Fact]
    public void Value_WhenFileIsCorruptJson_QuarantinesToCorruptBackup()
    {
        File.WriteAllText(SettingsFile, "{ \"CurrentHeartRateTitle\": ");

        var provider = new JsonSettingsProvider<PulsoidModuleSettings>(_env);

        Assert.NotNull(provider.Value);
        Assert.Single(Directory.GetFiles(_dir, "*.corrupt-*"));
        Assert.False(File.Exists(SettingsFile));

        // Unlike an IO failure, the corrupt path stays persistable: dispose saves defaults.
        provider.Dispose();
        Assert.True(File.Exists(SettingsFile));
    }

    [Fact]
    public void Save_WhenTargetFileIsLocked_FailsWithoutThrowing()
    {
        var provider = new JsonSettingsProvider<PulsoidModuleSettings>(_env);
        provider.Value.CurrentHeartRateTitle = "Original";
        provider.FlushPendingSave();
        string before = File.ReadAllText(SettingsFile);

        provider.Value.CurrentHeartRateTitle = "Blocked write";
        using (new FileStream(SettingsFile, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var ex = Record.Exception(() => provider.FlushPendingSave());
            Assert.Null(ex);
        }

        // The failed save must leave the previous file content intact.
        Assert.Equal(before, File.ReadAllText(SettingsFile));
        provider.Dispose();
    }
}
