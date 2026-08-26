using System;
using System.IO;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Services;
using Xunit;

namespace MagicChatbox.Tests.Core;

/// <summary>
/// Auto-save listens to every PropertyChanged. Several modules write [JsonIgnore] display properties
/// once a second, which beat the 2s debounce forever and so forced a byte-identical settings file to
/// be serialized and written to disk every 30 seconds for the whole session.
/// </summary>
public partial class NonPersistedPropertyDoesNotSaveTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "mcb-settings-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void Changing_a_JsonIgnore_property_never_writes_the_file()
    {
        var provider = new JsonSettingsProvider<ProbeSettings>(new FixedEnvironment(_dir));
        ProbeSettings settings = provider.Value;

        provider.Save();
        string path = Path.Combine(_dir, nameof(ProbeSettings) + ".json");
        Assert.True(File.Exists(path), "the provider never wrote a baseline file");

        File.Delete(path);

        for (int i = 0; i < 20; i++)
            settings.DisplayOnly = i.ToString();

        // Comfortably past the 2s debounce the writes would otherwise have armed.
        Thread.Sleep(3500);

        Assert.False(
            File.Exists(path),
            "a property that is never serialized triggered a settings save");
    }

    [Fact]
    public void Changing_a_persisted_property_still_writes_the_file()
    {
        var provider = new JsonSettingsProvider<ProbeSettings>(new FixedEnvironment(_dir));
        ProbeSettings settings = provider.Value;

        provider.Save();
        string path = Path.Combine(_dir, nameof(ProbeSettings) + ".json");
        File.Delete(path);

        settings.Persisted = "changed";
        Thread.Sleep(3500);

        Assert.True(File.Exists(path), "a real settings change did not reach disk");
    }

    public partial class ProbeSettings : ObservableObject
    {
        [ObservableProperty]
        private string _persisted = string.Empty;

        [ObservableProperty]
        [property: JsonIgnore]
        private string _displayOnly = string.Empty;
    }

    private sealed class FixedEnvironment : IEnvironmentService
    {
        public FixedEnvironment(string dataPath)
        {
            Directory.CreateDirectory(dataPath);
            DataPath = dataPath;
        }

        public string DataPath { get; }

        public string LogPath => DataPath;

        public string VrcPath => DataPath;

        public void SetCustomProfile(int profileNumber)
        {
        }
    }
}
