using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.State;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules;

public sealed class IntegrationSettingsPersistenceTests
{
    private sealed class TempEnvironment : IEnvironmentService
    {
        public TempEnvironment(string root) => DataPath = root;
        public string DataPath { get; }
        public string LogPath => Path.Combine(DataPath, "logs");
        public string VrcPath => DataPath;
        public void SetCustomProfile(int profileNumber) => throw new NotSupportedException();
    }

    private static string[] CustomOrder => IntegrationDisplayState.DefaultSortOrder.Reverse().ToArray();

    [Fact]
    public void SavedSortOrder_RoundTripWithProviderDeserializerSettings_PreservesCustomOrder()
    {
        string[] customOrder = CustomOrder;
        var settings = new IntegrationSettings
        {
            SavedSortOrder = new ObservableCollection<string>(customOrder)
        };
        string json = JsonConvert.SerializeObject(settings, Formatting.Indented);

        var loaded = JsonConvert.DeserializeObject<IntegrationSettings>(json, JsonSettingsSerialization.DeserializerSettings);

        Assert.NotNull(loaded);
        Assert.Equal(customOrder.Length, loaded.SavedSortOrder.Count);
        Assert.Equal(customOrder, loaded.SavedSortOrder);
        Assert.Equal(loaded.SavedSortOrder.Count, loaded.SavedSortOrder.Distinct().Count());
    }

    [Fact]
    public void SavedSortOrder_RoundTripWithDefaultSerializer_PreservesCustomOrderViaPropertyAttribute()
    {
        string[] customOrder = CustomOrder;
        var settings = new IntegrationSettings
        {
            SavedSortOrder = new ObservableCollection<string>(customOrder)
        };
        string json = JsonConvert.SerializeObject(settings, Formatting.Indented);

        var loaded = JsonConvert.DeserializeObject<IntegrationSettings>(json);

        Assert.NotNull(loaded);
        Assert.Equal(customOrder, loaded.SavedSortOrder);
    }

    [Fact]
    public void SavedSortOrder_MissingFromJson_KeepsDefaultOrder()
    {
        var loaded = JsonConvert.DeserializeObject<IntegrationSettings>("{}", JsonSettingsSerialization.DeserializerSettings);

        Assert.NotNull(loaded);
        Assert.Equal(IntegrationDisplayState.DefaultSortOrder, loaded.SavedSortOrder);
    }

    [Fact]
    public void Value_LoadedFromDisk_PreservesCustomSortOrder()
    {
        string dir = Path.Combine(Path.GetTempPath(), "MagicChatboxTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string[] customOrder = CustomOrder;
            var saved = new IntegrationSettings
            {
                SavedSortOrder = new ObservableCollection<string>(customOrder)
            };
            File.WriteAllText(
                Path.Combine(dir, $"{nameof(IntegrationSettings)}.json"),
                JsonConvert.SerializeObject(saved, Formatting.Indented));

            using var provider = new JsonSettingsProvider<IntegrationSettings>(new TempEnvironment(dir));

            Assert.Equal(customOrder, provider.Value.SavedSortOrder);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }
}
