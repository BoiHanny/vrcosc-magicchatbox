using MagicChatbox.Vocabulary;
using MagicChatbox.Vrc;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace vrcosc_magicchatbox.Core.Vrc;

public readonly record struct AvatarSense(string Key, SignalKind Kind, double Value, string Text, DateTime SeenUtc);

public sealed class AvatarSenseStore : IVrcObservationSink
{
    public const string ParameterKeyPrefix = "avatar.param.";

    private sealed class Cell
    {
        public SignalKind Kind;
        public double Value;
        public string Text = string.Empty;
        public long SeenTicks;
        public long Changes;
    }

    private readonly ConcurrentDictionary<string, Cell> _cells = new(StringComparer.Ordinal);
    private long _observations;

    public long Observations => System.Threading.Interlocked.Read(ref _observations);

    public int Count => _cells.Count;

    public void OnObservation(in VrcObservation observation)
    {
        System.Threading.Interlocked.Increment(ref _observations);

        string key = observation.Key.Value;
        if (key.Length == 0)
            return;

        Cell cell = _cells.GetOrAdd(key, _ => new Cell());

        double value = observation.Value.Kind switch
        {
            SignalKind.Bool => observation.Value.AsBool() ? 1d : 0d,
            SignalKind.Int => observation.Value.AsInt(),
            SignalKind.Float => observation.Value.IsFinite() ? observation.Value.AsFloat() : 0d,
            _ => 0d,
        };

        lock (cell)
        {
            if (cell.SeenTicks != 0 && cell.Kind == observation.Value.Kind && cell.Value == value)
            {
                cell.SeenTicks = DateTime.UtcNow.Ticks;
                return;
            }

            cell.Kind = observation.Value.Kind;
            cell.Value = value;
            cell.Text = observation.Value.Kind == SignalKind.Text ? observation.Value.AsText() : string.Empty;
            cell.SeenTicks = DateTime.UtcNow.Ticks;
            cell.Changes++;
        }
    }

    public bool TryGet(string key, out AvatarSense sense)
    {
        if (_cells.TryGetValue(key, out Cell? cell))
        {
            lock (cell)
            {
                sense = new AvatarSense(key, cell.Kind, cell.Value, cell.Text, new DateTime(cell.SeenTicks, DateTimeKind.Utc));
                return cell.SeenTicks != 0;
            }
        }

        sense = default;
        return false;
    }

    public bool TryGetParameter(string name, out AvatarSense sense)
        => TryGet(ParameterKeyPrefix + name, out sense);

    public IReadOnlyList<AvatarSense> Snapshot()
    {
        var rows = new List<AvatarSense>(_cells.Count);

        foreach (KeyValuePair<string, Cell> entry in _cells)
        {
            Cell cell = entry.Value;

            lock (cell)
            {
                if (cell.SeenTicks == 0)
                    continue;

                rows.Add(new AvatarSense(
                    entry.Key, cell.Kind, cell.Value, cell.Text, new DateTime(cell.SeenTicks, DateTimeKind.Utc)));
            }
        }

        rows.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
        return rows;
    }

    public IReadOnlyList<AvatarSense> MostActive(int take)
    {
        var rows = new List<(AvatarSense Sense, long Changes)>();

        foreach (KeyValuePair<string, Cell> entry in _cells)
        {
            Cell cell = entry.Value;

            lock (cell)
            {
                if (cell.SeenTicks == 0)
                    continue;

                rows.Add((
                    new AvatarSense(entry.Key, cell.Kind, cell.Value, cell.Text, new DateTime(cell.SeenTicks, DateTimeKind.Utc)),
                    cell.Changes));
            }
        }

        return rows
            .OrderByDescending(r => r.Changes)
            .Take(Math.Max(0, take))
            .Select(r => r.Sense)
            .ToList();
    }

    public void Clear()
    {
        _cells.Clear();
    }
}
