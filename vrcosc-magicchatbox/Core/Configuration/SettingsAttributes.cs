using System;

namespace vrcosc_magicchatbox.Core.Configuration;

/// <summary>
/// Declares the current schema version for a settings class.
/// Bump this integer whenever you make a breaking config change.
/// Apply [ResetModuleAfterSchema] alongside a bump to force a full reset for affected users.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class CurrentSchemaAttribute : Attribute
{
    public int Version { get; }
    public CurrentSchemaAttribute(int version) => Version = version;
}

/// <summary>
/// Reset this property to its default value when the loaded file was written by
/// an app version OLDER than <paramref name="minVersion"/>.
/// Apply to generated properties using the [property: ResetAfterVersion("x.y.z")] syntax
/// on the backing field, or directly to hand-written properties.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class ResetAfterVersionAttribute : Attribute
{
    /// <summary>Minimum app version that is safe to load this property as-is.</summary>
    public string MinVersion { get; }
    public ResetAfterVersionAttribute(string minVersion) => MinVersion = minVersion;
}

/// <summary>
/// Reset ALL properties in the settings class to defaults when the loaded file's
/// schema version is less than <paramref name="schemaVersion"/>.
/// This is a nuclear option — use it when a format change is fundamentally incompatible.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ResetModuleAfterSchemaAttribute : Attribute
{
    public int SchemaVersion { get; }
    public ResetModuleAfterSchemaAttribute(int schemaVersion) => SchemaVersion = schemaVersion;
}
