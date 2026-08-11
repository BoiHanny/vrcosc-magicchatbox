using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;

namespace vrcosc_magicchatbox.Core.Configuration;

public abstract class VersionedSettings : ObservableObject
{
    [JsonProperty("_schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    [JsonProperty("_appVersion")]
    public string AppVersion { get; set; } = string.Empty;

    [JsonProperty("_migratedAt")]
    public System.DateTime? MigratedAt { get; set; }
}
