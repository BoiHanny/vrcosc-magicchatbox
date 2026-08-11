using System;

namespace vrcosc_magicchatbox.Core.Configuration;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class CurrentSchemaAttribute : Attribute
{
    public int Version { get; }
    public CurrentSchemaAttribute(int version) => Version = version;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class ResetAfterVersionAttribute : Attribute
{
    public string MinVersion { get; }
    public ResetAfterVersionAttribute(string minVersion) => MinVersion = minVersion;
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ResetModuleAfterSchemaAttribute : Attribute
{
    public int SchemaVersion { get; }
    public ResetModuleAfterSchemaAttribute(int schemaVersion) => SchemaVersion = schemaVersion;
}
