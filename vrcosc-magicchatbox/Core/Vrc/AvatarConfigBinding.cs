using MagicChatbox.Vocabulary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace vrcosc_magicchatbox.Core.Vrc;

public enum ConfigDirection
{
    OffOnly,
    Both,
}

public sealed record AvatarConfigBinding(
    string Parameter,
    string Description,
    ConfigDirection Direction,
    Action<bool> Apply)
{
    public const string Prefix = "MCB/Cfg/";

    public bool IsOwned => Parameter.StartsWith(Prefix, StringComparison.Ordinal);
}

public enum ConfigSeedOutcome
{
    Applied,
    NotOnThisAvatar,
    NotStableYet,
    RefusedTurningOn,
    Unchanged,
}

public sealed record ConfigSeedRow(string Parameter, ConfigSeedOutcome Outcome, bool Value);

public sealed class AvatarConfigSeeder
{
    public static readonly TimeSpan DefaultStability = TimeSpan.FromSeconds(3);

    private static readonly double TicksPerMs = Stopwatch.Frequency / 1000d;

    private readonly IReadOnlyDictionary<string, AvatarConfigBinding> _bindings;
    private readonly Func<bool> _isEnabled;
    private readonly TimeSpan _stability;
    private readonly object _gate = new();
    private readonly Dictionary<string, bool> _applied = new(StringComparer.Ordinal);

    private long _schemaSeenTicks;
    private string _schemaAvatarId = string.Empty;

    public AvatarConfigSeeder(
        IEnumerable<AvatarConfigBinding> bindings,
        Func<bool> isEnabled,
        TimeSpan? stability = null)
    {
        ArgumentNullException.ThrowIfNull(bindings);

        var map = new Dictionary<string, AvatarConfigBinding>(StringComparer.Ordinal);
        foreach (AvatarConfigBinding binding in bindings)
            map[binding.Parameter] = binding;

        _bindings = map;
        _isEnabled = isEnabled ?? throw new ArgumentNullException(nameof(isEnabled));
        _stability = stability ?? DefaultStability;
    }

    public void NoteSchema(AvatarSchemaSnapshot schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        lock (_gate)
        {
            if (!string.Equals(_schemaAvatarId, schema.AvatarId, StringComparison.Ordinal))
            {
                _schemaAvatarId = schema.AvatarId;
                _schemaSeenTicks = Stopwatch.GetTimestamp();
                _applied.Clear();
            }
        }
    }

    public IReadOnlyList<ConfigSeedRow> Seed(AvatarSchemaSnapshot schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        var rows = new List<ConfigSeedRow>();

        if (!Enabled() || schema.IsEmpty)
            return rows;

        NoteSchema(schema);

        bool stable;
        lock (_gate)
            stable = _schemaSeenTicks != 0 && Elapsed(_schemaSeenTicks) >= _stability;

        var declared = schema.Parameters.ToDictionary(
            p => EcosystemSignature.Normalize(p.Name),
            p => p,
            StringComparer.Ordinal);

        foreach (AvatarConfigBinding binding in _bindings.Values)
        {
            if (!declared.TryGetValue(binding.Parameter, out var declaration))
            {
                rows.Add(new ConfigSeedRow(binding.Parameter, ConfigSeedOutcome.NotOnThisAvatar, false));
                continue;
            }

            if (!stable)
            {
                rows.Add(new ConfigSeedRow(binding.Parameter, ConfigSeedOutcome.NotStableYet, false));
                continue;
            }

            if (!declaration.Value.HasValue || declaration.Kind != SignalKind.Bool)
            {
                rows.Add(new ConfigSeedRow(binding.Parameter, ConfigSeedOutcome.NotOnThisAvatar, false));
                continue;
            }

            bool value = declaration.Value.Value.AsBool();

            lock (_gate)
            {
                if (_applied.TryGetValue(binding.Parameter, out bool previous) && previous == value)
                {
                    rows.Add(new ConfigSeedRow(binding.Parameter, ConfigSeedOutcome.Unchanged, value));
                    continue;
                }
            }

            if (value && binding.Direction == ConfigDirection.OffOnly)
            {
                rows.Add(new ConfigSeedRow(binding.Parameter, ConfigSeedOutcome.RefusedTurningOn, value));
                continue;
            }

            lock (_gate) _applied[binding.Parameter] = value;

            try
            {
                binding.Apply(value);
                rows.Add(new ConfigSeedRow(binding.Parameter, ConfigSeedOutcome.Applied, value));
            }
            catch (Exception ex)
            {
                Classes.DataAndSecurity.Logging.WriteException(ex, MSGBox: false);
            }
        }

        return rows;
    }

    public void Reset()
    {
        lock (_gate)
        {
            _applied.Clear();
            _schemaSeenTicks = 0;
            _schemaAvatarId = string.Empty;
        }
    }

    private bool Enabled()
    {
        try
        {
            return _isEnabled();
        }
        catch
        {
            return false;
        }
    }

    private static TimeSpan Elapsed(long since)
        => TimeSpan.FromMilliseconds((Stopwatch.GetTimestamp() - since) / TicksPerMs);
}
