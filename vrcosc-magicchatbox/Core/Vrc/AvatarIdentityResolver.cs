using System;

namespace vrcosc_magicchatbox.Core.Vrc;

public enum AvatarIdSource
{
    None,
    AvatarChange,
    SchemaHarvest,
}

public readonly record struct AvatarIdentity(string Id, string Name, AvatarIdSource Source)
{
    public static readonly AvatarIdentity Unknown = new(string.Empty, string.Empty, AvatarIdSource.None);

    public bool IsKnown => Id.Length > 0;

    public string DisplayName => Name.Length > 0 ? Name : (Id.Length > 0 ? Id : "Unknown avatar");
}

public sealed class AvatarIdentityResolver
{
    private const string LocalIdPrefix = "local:";

    private readonly Func<string> _epochId;
    private readonly Func<AvatarSchemaSnapshot> _schema;
    private readonly AvatarConfigReader _configs;
    private readonly object _gate = new();

    private string _cachedId = string.Empty;
    private string _cachedName = string.Empty;

    public AvatarIdentityResolver(
        Func<string> epochId,
        Func<AvatarSchemaSnapshot> schema,
        AvatarConfigReader? configs = null)
    {
        _epochId = epochId ?? throw new ArgumentNullException(nameof(epochId));
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        _configs = configs ?? new AvatarConfigReader();
    }

    public AvatarIdentity Resolve()
    {
        string id = Read(_epochId);
        AvatarIdSource source = AvatarIdSource.AvatarChange;

        if (!IsUsable(id))
        {
            try
            {
                id = _schema().AvatarId ?? string.Empty;
            }
            catch
            {
                id = string.Empty;
            }

            source = AvatarIdSource.SchemaHarvest;
        }

        if (!IsUsable(id))
            return AvatarIdentity.Unknown;

        return new AvatarIdentity(id, NameFor(id), source);
    }

    public static bool IsUsable(string? id)
        => !string.IsNullOrWhiteSpace(id)
           && !id.StartsWith(LocalIdPrefix, StringComparison.OrdinalIgnoreCase);

    private string NameFor(string id)
    {
        lock (_gate)
        {
            if (string.Equals(_cachedId, id, StringComparison.Ordinal))
                return _cachedName;
        }

        string name = string.Empty;

        try
        {
            AvatarConfigInfo? info = _configs.TryRead(id);
            if (info.HasValue)
                name = info.Value.Name ?? string.Empty;
        }
        catch
        {
            name = string.Empty;
        }

        lock (_gate)
        {
            _cachedId = id;
            _cachedName = name;
        }

        return name;
    }

    private static string Read(Func<string> source)
    {
        try
        {
            return source() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
