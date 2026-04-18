using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;

namespace vrcosc_magicchatbox.Core.Configuration;

/// <summary>
/// Base class for all persisted settings objects.
/// Adds schema version, app version, and migration timestamp fields to every JSON file.
/// Inherits ObservableObject so source-generated [ObservableProperty] keeps working.
/// </summary>
public abstract class VersionedSettings : ObservableObject
{
    /// <summary>
    /// Schema version of the settings file as written.
    /// Compare against [CurrentSchema(N)] on the concrete class to detect stale configs.
    /// </summary>
    [JsonProperty("_schemaVersion")]
    public int SchemaVersion { get; set; } = 0;

    /// <summary>
    /// App version string (e.g. "1.2.3.0") that last wrote this file.
    /// Used by [ResetAfterVersion("x.y.z")] to selectively reset properties.
    /// </summary>
    [JsonProperty("_appVersion")]
    public string AppVersion { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp of the one-time XML→JSON migration, or null if this file
    /// was created fresh (no legacy settings.xml present at first run).
    /// </summary>
    [JsonProperty("_migratedAt")]
    public System.DateTime? MigratedAt { get; set; }
}
