using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace vrcosc_magicchatbox.Core.Diagnostics;

/// <summary>
/// Drives the main menu through every page repeatedly and reports what each switch costs. Clicking by
/// hand cannot separate build time from the transition animation or produce a stable average.
/// </summary>
public static class NavigationBenchmark
{
    public static async Task<string> RunAsync(
        Window window,
        Action<int> selectPage,
        Func<int> readPage,
        int pageCount = 4,
        int rounds = 3)
    {
        if (!PerfProbe.IsEnabled)
            return "[Perf] Navigation benchmark needs --perf.";

        int startingPage = readPage();
        var results = new List<(int Page, double SwitchMs, long WorkingSetDeltaKb)>();
        var censuses = new Dictionary<int, string>();

        PerfProbe.Mark($"Navigation benchmark starting: {rounds} rounds over {pageCount} pages");

        for (int round = 0; round < rounds; round++)
        {
            for (int page = 0; page < pageCount; page++)
            {
                using var process = Process.GetCurrentProcess();
                long beforeWorkingSet = process.WorkingSet64;

                long startTicks = Stopwatch.GetTimestamp();
                selectPage(page);

                // Returning at Loaded priority means every build, bind and layout pass queued by the
                // switch has already run, so the number covers the whole switch and not just the setter.
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);

                double switchMs = Stopwatch.GetElapsedTime(startTicks).TotalMilliseconds;

                process.Refresh();
                results.Add((page, switchMs, (process.WorkingSet64 - beforeWorkingSet) / 1024));

                PerfProbe.Record($"nav.switch.page{page}", switchMs);

                // Let the host's teardown timer fire so the next round measures a cold build.
                await Task.Delay(TimeSpan.FromSeconds(4));

                // Census only after teardown, or the count still includes the page we just left.
                if (!censuses.ContainsKey(page))
                    censuses[page] = VisualTreeCensus.Take(window).Describe($"page {page}");
            }
        }

        selectPage(startingPage);

        return Format(results, rounds) + string.Concat(censuses.OrderBy(c => c.Key).Select(c => c.Value));
    }

    /// <summary>
    /// Scrolls a viewer top to bottom in viewport-sized steps, recording how long each step takes to
    /// settle. This is what tells you whether lazily built content keeps up with a real scroll.
    /// </summary>
    public static async Task<string> SweepScrollAsync(ScrollViewer scroll, string label, int steps = 12)
    {
        if (!PerfProbe.IsEnabled || scroll == null)
            return string.Empty;

        double startOffset = scroll.VerticalOffset;
        var stepTimes = new List<double>();

        for (int i = 0; i <= steps; i++)
        {
            double target = scroll.ScrollableHeight * i / steps;

            long startTicks = Stopwatch.GetTimestamp();
            scroll.ScrollToVerticalOffset(target);

            await scroll.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            await scroll.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

            double ms = Stopwatch.GetElapsedTime(startTicks).TotalMilliseconds;
            stepTimes.Add(ms);
            PerfProbe.Record($"scroll.step.{label}", ms);

            await Task.Delay(120);
        }

        scroll.ScrollToVerticalOffset(startOffset);

        stepTimes.Sort();
        return $"[Perf] Scroll sweep '{label}': {steps + 1} steps, median {stepTimes[stepTimes.Count / 2]:F1}ms, "
            + $"worst {stepTimes[^1]:F1}ms, extent {scroll.ExtentHeight:F0}px\n";
    }

    private static string Format(List<(int Page, double SwitchMs, long WorkingSetDeltaKb)> results, int rounds)
    {
        var sb = new StringBuilder();
        sb.Append("[Perf] Navigation benchmark, ").Append(rounds).AppendLine(" rounds:");

        foreach (var group in results.GroupBy(r => r.Page).OrderBy(g => g.Key))
        {
            double mean = 0;
            double max = 0;
            long workingSet = 0;
            int count = 0;

            foreach ((int _, double switchMs, long deltaKb) in group)
            {
                mean += switchMs;
                max = Math.Max(max, switchMs);
                workingSet += deltaKb;
                count++;
            }

            sb.Append("  page ").Append(group.Key)
                .Append(": mean ").Append((mean / count).ToString("F1"))
                .Append("ms, max ").Append(max.ToString("F1"))
                .Append("ms, working set ").Append(workingSet / count)
                .AppendLine("KB/switch");
        }

        return sb.ToString();
    }
}
