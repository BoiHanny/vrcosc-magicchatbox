using MagicChatbox.Vocabulary;
using MagicChatbox.Vrc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace vrcosc_magicchatbox.Core.Vrc;

public sealed record AvatarSchemaSnapshot(
    string AvatarId,
    long Epoch,
    DateTime ReceivedUtc,
    IReadOnlyList<VrcParameterDeclaration> Parameters)
{
    public static readonly AvatarSchemaSnapshot Empty =
        new(string.Empty, 0, DateTime.MinValue, Array.Empty<VrcParameterDeclaration>());

    public bool IsEmpty => Parameters.Count == 0 && AvatarId.Length == 0;

    public int WritableCount => Parameters.Count(p => p.Writable);

    public int ReadOnlyCount => Parameters.Count - WritableCount;
}

public sealed class AvatarSchemaStore : IVrcSchemaSink
{
    private readonly object _gate = new();
    private readonly Func<long> _currentEpoch;

    private AvatarSchemaSnapshot _current = AvatarSchemaSnapshot.Empty;
    private Dictionary<string, VrcParameterDeclaration> _byName = new(StringComparer.Ordinal);

    private long _harvests;
    private long _staleDropped;

    public AvatarSchemaStore(Func<long>? currentEpoch = null)
    {
        _currentEpoch = currentEpoch ?? (() => long.MinValue);
    }

    public event Action<AvatarSchemaSnapshot>? SchemaChanged;

    public AvatarSchemaSnapshot Current
    {
        get { lock (_gate) return _current; }
    }

    public long Harvests => System.Threading.Interlocked.Read(ref _harvests);

    public long StaleDropped => System.Threading.Interlocked.Read(ref _staleDropped);

    public void OnSchemaHarvested(VrcAvatarSchemaHarvest harvest)
    {
        if (harvest == null)
            return;

        System.Threading.Interlocked.Increment(ref _harvests);

        long now = ReadEpoch();
        if (now != long.MinValue && harvest.Epoch != now)
        {
            System.Threading.Interlocked.Increment(ref _staleDropped);
            return;
        }

        var snapshot = new AvatarSchemaSnapshot(
            harvest.AvatarId ?? string.Empty,
            harvest.Epoch,
            DateTime.UtcNow,
            harvest.Parameters ?? Array.Empty<VrcParameterDeclaration>());

        var index = new Dictionary<string, VrcParameterDeclaration>(StringComparer.Ordinal);
        foreach (VrcParameterDeclaration declaration in snapshot.Parameters)
            index[declaration.Name] = declaration;

        lock (_gate)
        {
            _current = snapshot;
            _byName = index;
        }

        SchemaChanged?.Invoke(snapshot);
    }

    public void Clear()
    {
        lock (_gate)
        {
            _current = AvatarSchemaSnapshot.Empty;
            _byName = new Dictionary<string, VrcParameterDeclaration>(StringComparer.Ordinal);
        }
    }

    public bool TryGet(string name, out VrcParameterDeclaration declaration)
    {
        lock (_gate) return _byName.TryGetValue(name, out declaration);
    }

    public bool CanDrive(string name, SignalKind kind)
    {
        lock (_gate)
        {
            return _byName.TryGetValue(name, out VrcParameterDeclaration declaration)
                   && declaration.Writable
                   && declaration.Kind == kind;
        }
    }

    public IReadOnlyList<string> MatchDrivable(IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);

        var matched = new List<string>();

        lock (_gate)
        {
            foreach (string name in names)
            {
                if (_byName.TryGetValue(name, out VrcParameterDeclaration declaration) && declaration.Writable)
                    matched.Add(name);
            }
        }

        return matched;
    }

    private long ReadEpoch()
    {
        try
        {
            return _currentEpoch();
        }
        catch
        {
            return long.MinValue;
        }
    }
}
