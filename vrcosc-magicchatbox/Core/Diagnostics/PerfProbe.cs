using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace vrcosc_magicchatbox.Core.Diagnostics;

/// <summary>
/// Opt-in performance instrumentation. Enabled by setting MAGICCHATBOX_PERF=1 or passing --perf.
/// Every entry point short-circuits on <see cref="IsEnabled"/> so a normal run pays one static bool read.
/// </summary>
public static class PerfProbe
{
    private const string EnableVariable = "MAGICCHATBOX_PERF";

    public const string EnableArgument = "--perf";
    public const string LegacyEnableArgument = "-perf";

    private static readonly ConcurrentDictionary<string, SampleSet> Samples = new(StringComparer.Ordinal);
    private static readonly ConcurrentQueue<string> Timeline = new();
    private static readonly Stopwatch ProcessClock = Stopwatch.StartNew();

    private static int _timelineCount;

    public static bool IsEnabled { get; } = ResolveEnabled();

    /// <summary>Where <see cref="WriteReport"/> puts its JSON, alongside the NLog output.</summary>
    public static string? ReportDirectory { get; set; }

    private static bool ResolveEnabled()
    {
        try
        {
            string? variable = Environment.GetEnvironmentVariable(EnableVariable);
            if (!string.IsNullOrWhiteSpace(variable) && variable.Trim() is not ("0" or "false" or "False"))
                return true;

            return Environment.GetCommandLineArgs()
                .Any(IsEnableArgument);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsEnableArgument(string? argument)
        => string.Equals(argument, EnableArgument, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(argument, LegacyEnableArgument, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Times a block and records elapsed milliseconds plus bytes allocated on the calling thread.
    /// Returns a no-op token when instrumentation is off, so callers never branch.
    /// </summary>
    public static Scope Measure(string name) => IsEnabled ? new Scope(name) : default;

    public static void Record(string name, double milliseconds, long allocatedBytes = 0)
    {
        if (!IsEnabled)
            return;

        Samples.GetOrAdd(name, _ => new SampleSet()).Add(milliseconds, allocatedBytes);
    }

    public static void Mark(string message)
    {
        if (!IsEnabled)
            return;

        // Bounded so a long session cannot grow the timeline without limit.
        if (Interlocked.Increment(ref _timelineCount) > 4000)
        {
            Timeline.TryDequeue(out _);
            Interlocked.Decrement(ref _timelineCount);
        }

        Timeline.Enqueue($"{ProcessClock.Elapsed.TotalMilliseconds:F1}\t{message}");
    }

    public static IReadOnlyDictionary<string, SampleSet.Snapshot> Snapshot()
        => Samples.ToDictionary(p => p.Key, p => p.Value.Read(), StringComparer.Ordinal);

    public static void Reset()
    {
        Samples.Clear();
        while (Timeline.TryDequeue(out _))
        {
        }

        Interlocked.Exchange(ref _timelineCount, 0);
    }

    /// <summary>Writes a JSON snapshot of every collected metric and returns the path, or null if disabled.</summary>
    public static string? WriteReport(string reason)
    {
        if (!IsEnabled)
            return null;

        string directory = ReportDirectory ?? Path.Combine(Path.GetTempPath(), "MagicChatbox-perf");
        Directory.CreateDirectory(directory);

        string path = Path.Combine(
            directory,
            $"perf-{DateTime.Now:yyyyMMdd-HHmmss}-{Sanitize(reason)}.json");

        var json = new StringBuilder();
        json.AppendLine("{");
        json.Append("  \"reason\": ").Append(Quote(reason)).AppendLine(",");
        json.Append("  \"capturedAt\": ").Append(Quote(DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture))).AppendLine(",");
        json.Append("  \"uptimeMs\": ").Append(ProcessClock.Elapsed.TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture)).AppendLine(",");

        AppendProcess(json);
        AppendSamples(json);
        AppendTimeline(json);

        json.AppendLine("}");

        File.WriteAllText(path, json.ToString());
        return path;
    }

    private static void AppendProcess(StringBuilder json)
    {
        using var process = Process.GetCurrentProcess();

        json.AppendLine("  \"process\": {");
        json.Append("    \"workingSetBytes\": ").Append(process.WorkingSet64).AppendLine(",");
        json.Append("    \"privateBytes\": ").Append(process.PrivateMemorySize64).AppendLine(",");
        json.Append("    \"handles\": ").Append(process.HandleCount).AppendLine(",");
        json.Append("    \"threads\": ").Append(process.Threads.Count).AppendLine(",");
        json.Append("    \"managedHeapBytes\": ").Append(GC.GetTotalMemory(forceFullCollection: false)).AppendLine(",");

        // Separates a real leak from garbage the collector simply has not got to yet; without both numbers
        // a large heap says nothing about whether torn-down pages were actually released.
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        json.Append("    \"managedHeapAfterFullGcBytes\": ").Append(GC.GetTotalMemory(forceFullCollection: false)).AppendLine(",");

        process.Refresh();
        json.Append("    \"workingSetAfterFullGcBytes\": ").Append(process.WorkingSet64).AppendLine(",");
        json.Append("    \"totalAllocatedBytes\": ").Append(GC.GetTotalAllocatedBytes(precise: false)).AppendLine(",");
        json.Append("    \"gen0\": ").Append(GC.CollectionCount(0)).AppendLine(",");
        json.Append("    \"gen1\": ").Append(GC.CollectionCount(1)).AppendLine(",");
        json.Append("    \"gen2\": ").Append(GC.CollectionCount(2)).AppendLine();
        json.AppendLine("  },");
    }

    private static void AppendSamples(StringBuilder json)
    {
        json.AppendLine("  \"samples\": {");

        var ordered = Samples
            .Select(p => (p.Key, Value: p.Value.Read()))
            .OrderByDescending(p => p.Value.TotalMs)
            .ToList();

        for (int i = 0; i < ordered.Count; i++)
        {
            (string key, SampleSet.Snapshot value) = ordered[i];

            json.Append("    ").Append(Quote(key)).Append(": { ")
                .Append("\"count\": ").Append(value.Count)
                .Append(", \"totalMs\": ").Append(Number(value.TotalMs))
                .Append(", \"meanMs\": ").Append(Number(value.MeanMs))
                .Append(", \"maxMs\": ").Append(Number(value.MaxMs))
                .Append(", \"p95Ms\": ").Append(Number(value.P95Ms))
                .Append(", \"allocatedBytes\": ").Append(value.AllocatedBytes)
                .Append(" }")
                .AppendLine(i == ordered.Count - 1 ? string.Empty : ",");
        }

        json.AppendLine("  },");
    }

    private static void AppendTimeline(StringBuilder json)
    {
        json.AppendLine("  \"timeline\": [");

        string[] entries = Timeline.ToArray();
        for (int i = 0; i < entries.Length; i++)
        {
            json.Append("    ").Append(Quote(entries[i]))
                .AppendLine(i == entries.Length - 1 ? string.Empty : ",");
        }

        json.AppendLine("  ]");
    }

    private static string Number(double value)
        => value.ToString("F3", CultureInfo.InvariantCulture);

    private static string Quote(string value)
    {
        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');

        foreach (char c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < ' ')
                        sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else
                        sb.Append(c);
                    break;
            }
        }

        sb.Append('"');
        return sb.ToString();
    }

    private static string Sanitize(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (char c in value)
            sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-');

        return sb.ToString().Trim('-');
    }

    public readonly struct Scope : IDisposable
    {
        private readonly string? _name;
        private readonly long _startTicks;
        private readonly long _startAllocated;

        internal Scope(string name)
        {
            _name = name;
            _startTicks = Stopwatch.GetTimestamp();
            _startAllocated = GC.GetAllocatedBytesForCurrentThread();
        }

        public void Dispose()
        {
            if (_name == null)
                return;

            double elapsedMs = Stopwatch.GetElapsedTime(_startTicks).TotalMilliseconds;
            long allocated = GC.GetAllocatedBytesForCurrentThread() - _startAllocated;

            Record(_name, elapsedMs, allocated);
            Mark($"{_name} {elapsedMs:F1}ms {allocated / 1024.0:F0}KB");
        }
    }

    public sealed class SampleSet
    {
        private readonly object _gate = new();
        private readonly List<double> _values = new();

        private long _allocatedBytes;

        public void Add(double milliseconds, long allocatedBytes)
        {
            lock (_gate)
            {
                // Keep the newest window rather than every sample; percentiles stay meaningful and memory does not grow.
                if (_values.Count == 2000)
                    _values.RemoveAt(0);

                _values.Add(milliseconds);
                _allocatedBytes += allocatedBytes;
            }
        }

        public Snapshot Read()
        {
            lock (_gate)
            {
                if (_values.Count == 0)
                    return new Snapshot(0, 0, 0, 0, 0, _allocatedBytes);

                double[] sorted = _values.ToArray();
                Array.Sort(sorted);

                double total = 0;
                foreach (double v in sorted)
                    total += v;

                int p95Index = Math.Min(sorted.Length - 1, (int)Math.Ceiling(sorted.Length * 0.95) - 1);

                return new Snapshot(
                    sorted.Length,
                    total,
                    total / sorted.Length,
                    sorted[^1],
                    sorted[Math.Max(0, p95Index)],
                    _allocatedBytes);
            }
        }

        public readonly record struct Snapshot(
            int Count,
            double TotalMs,
            double MeanMs,
            double MaxMs,
            double P95Ms,
            long AllocatedBytes);
    }
}
