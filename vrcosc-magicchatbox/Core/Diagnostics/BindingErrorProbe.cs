using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Diagnostics;

namespace vrcosc_magicchatbox.Core.Diagnostics;

/// <summary>
/// Counts WPF data-binding failures at runtime. A failed binding is re-evaluated and re-reported every
/// time its source changes, so a broken path in a hot template is a running cost, not a one-off warning.
/// </summary>
public static class BindingErrorProbe
{
    private static readonly ConcurrentDictionary<string, int> Errors = new(StringComparer.Ordinal);

    private static CountingListener? _listener;
    private static SourceLevels _previousLevel;

    public static int TotalErrors => Errors.Values.Sum();

    public static int DistinctErrors => Errors.Count;

    public static void Start()
    {
        if (!PerfProbe.IsEnabled || _listener != null)
            return;

        PresentationTraceSources.Refresh();

        _listener = new CountingListener();
        _previousLevel = PresentationTraceSources.DataBindingSource.Switch.Level;

        PresentationTraceSources.DataBindingSource.Listeners.Add(_listener);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error | SourceLevels.Warning;
    }

    public static void Stop()
    {
        if (_listener == null)
            return;

        PresentationTraceSources.DataBindingSource.Listeners.Remove(_listener);
        PresentationTraceSources.DataBindingSource.Switch.Level = _previousLevel;
        _listener = null;
    }

    public static string Describe(int top = 25)
    {
        var sb = new StringBuilder();
        sb.Append("[Perf] Binding failures: ").Append(TotalErrors)
            .Append(" total across ").Append(DistinctErrors).AppendLine(" distinct paths");

        foreach (KeyValuePair<string, int> entry in Errors.OrderByDescending(e => e.Value).Take(top))
            sb.Append("  ").Append(entry.Value).Append("x  ").AppendLine(entry.Key);

        return sb.ToString();
    }

    private static void Report(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        // Collapse the per-instance tail (hash codes, item indices) so repeats of one broken path group.
        string key = Normalize(message);
        Errors.AddOrUpdate(key, 1, (_, count) => count + 1);
    }

    private static string Normalize(string message)
    {
        int detailStart = message.IndexOf("; target element is", StringComparison.Ordinal);
        if (detailStart > 0)
            message = message[..detailStart];

        var sb = new StringBuilder(message.Length);
        bool inNumber = false;

        foreach (char c in message)
        {
            if (char.IsDigit(c))
            {
                if (!inNumber)
                {
                    sb.Append('#');
                    inNumber = true;
                }

                continue;
            }

            inNumber = false;
            sb.Append(c);
        }

        return sb.ToString().Trim();
    }

    private sealed class CountingListener : TraceListener
    {
        private readonly StringBuilder _pending = new();

        public override void Write(string? message)
        {
            if (message != null)
                _pending.Append(message);
        }

        public override void WriteLine(string? message)
        {
            _pending.Append(message);
            Report(_pending.ToString());
            _pending.Clear();
        }
    }
}
