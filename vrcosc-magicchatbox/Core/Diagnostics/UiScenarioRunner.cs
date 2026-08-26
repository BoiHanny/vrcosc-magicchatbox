using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using vrcosc_magicchatbox.Classes.Modules;

namespace vrcosc_magicchatbox.Core.Diagnostics;

/// <summary>
/// Drives the whole app the way a user would - every page, every Options section expanded, every
/// integration switched on - and records what each step cost. Measuring the app at rest with the
/// integrations off says nothing about what it does while it is actually working.
/// </summary>
/// <remarks>
/// Run this against a throwaway profile (<c>-profile=9</c>), never a real one: it toggles integrations
/// and expands sections, and those are persisted settings.
/// </remarks>
public sealed class UiScenarioRunner
{
    private readonly Window _window;
    private readonly IntegrationSettings _integrations;
    private readonly Action<int> _selectPage;
    private readonly StringBuilder _report = new();

    private readonly Dictionary<string, bool> _originalToggles = new(StringComparer.Ordinal);

    public UiScenarioRunner(Window window, IntegrationSettings integrations, Action<int> selectPage)
    {
        _window = window;
        _integrations = integrations;
        _selectPage = selectPage;
    }

    public async Task<string> RunAsync()
    {
        if (!PerfProbe.IsEnabled)
            return "[Scenario] needs --perf.\n";

        Line($"Scenario start. Profile data path: {AppDomain.CurrentDomain.FriendlyName}");

        await VisitEveryPageAsync();
        await ExpandEveryOptionsSectionAsync();
        await EnableEveryIntegrationAsync();
        await SoakAsync(TimeSpan.FromSeconds(30), "integrations on");
        await VisitEveryPageAsync("with integrations on");
        await RestoreIntegrationsAsync();

        return _report.ToString();
    }

    private async Task VisitEveryPageAsync(string label = "cold")
    {
        Line($"-- pages ({label}) --");

        for (int page = 0; page < 4; page++)
        {
            long start = Stopwatch.GetTimestamp();
            _selectPage(page);
            await UiDriver.SettleAsync(_window.Dispatcher);

            double ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            PerfProbe.Record($"scenario.page{page}.{Slug(label)}", ms);

            // Past the host's 3s teardown delay, or the census still counts the page we just left and
            // every page reads as the sum of the ones before it.
            await Task.Delay(TimeSpan.FromSeconds(4));

            VisualTreeCensus.Result census = VisualTreeCensus.Take(_window);
            Line($"page {page} ({label}): {ms:F0}ms, {census.Total} elements, "
                + $"{census.Effects} effects, {census.UnfrozenBrushes} unfrozen brushes");

            ScrollViewer? scroll = UiDriver.FindAll<ScrollViewer>(_window).FirstOrDefault(s => s.ScrollableHeight > 0);
            if (scroll != null)
                await UiDriver.ScrollThroughAsync(scroll, steps: 8);

            await Task.Delay(400);
        }
    }

    private async Task ExpandEveryOptionsSectionAsync()
    {
        Line("-- expanding every Options section --");

        _selectPage(3);
        await UiDriver.SettleAsync(_window.Dispatcher, 400);

        // Realizing every section first: a collapsed placeholder has no toggle to click.
        var page = UiDriver.FindAll<UI.Pages.OptionsPage>(_window).FirstOrDefault();
        if (page == null)
        {
            Line("Options page not found; skipping.");
            return;
        }

        page.RealizeAllSectionsForDiagnostics();
        await UiDriver.SettleAsync(_window.Dispatcher, 500);

        // Only the section headers. Every CheckBox is also a ToggleButton, so matching on the type alone
        // clicks 250-odd real settings instead of expanding 24 sections.
        List<ToggleButton> toggles = UiDriver.FindAll<ToggleButton>(page)
            .Where(t => t.IsEnabled && t.IsChecked == false && IsSectionHeader(t))
            .ToList();

        Line($"found {toggles.Count} collapsed section headers");

        int opened = 0;
        foreach (ToggleButton toggle in toggles)
        {
            long start = Stopwatch.GetTimestamp();
            if (!UiDriver.SetToggle(toggle, true))
                continue;

            // Settle only - no dwell - or the dwell is what gets measured.
            await UiDriver.SettleAsync(_window.Dispatcher);
            PerfProbe.Record("scenario.section.expand", Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            opened++;
        }

        VisualTreeCensus.Result census = VisualTreeCensus.Take(_window);
        Line($"expanded {opened} toggles -> {census.Total} elements, depth {census.MaxDepth}, "
            + $"{census.Effects} effects, {census.PartialOpacity} at partial opacity");

        ScrollViewer? scroll = UiDriver.FindAll<ScrollViewer>(page).FirstOrDefault();
        if (scroll != null)
        {
            Line($"extent with everything open: {scroll.ExtentHeight:F0}px");
            await UiDriver.ScrollThroughAsync(scroll, steps: 16);
        }

        foreach (ToggleButton toggle in toggles)
            UiDriver.SetToggle(toggle, false);

        await UiDriver.SettleAsync(_window.Dispatcher, 300);
    }

    private async Task EnableEveryIntegrationAsync()
    {
        Line("-- switching every integration on --");

        foreach ((string name, Action<bool> set, Func<bool> get) in IntegrationToggles())
        {
            _originalToggles[name] = get();

            long start = Stopwatch.GetTimestamp();
            set(true);

            // Settle only. Timing a dwell measures the dwell.
            await UiDriver.SettleAsync(_window.Dispatcher);
            double ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds;

            PerfProbe.Record($"scenario.enable.{name}", ms);
            Line($"enable {name}: {ms:F0}ms");

            // Now give the module time to actually start before enabling the next one.
            await Task.Delay(300);
        }
    }

    private async Task RestoreIntegrationsAsync()
    {
        Line("-- restoring integration toggles --");

        foreach ((string name, Action<bool> set, Func<bool> _) in IntegrationToggles())
        {
            if (_originalToggles.TryGetValue(name, out bool original))
                set(original);
        }

        await UiDriver.SettleAsync(_window.Dispatcher, 500);
    }

    private async Task SoakAsync(TimeSpan duration, string label)
    {
        Line($"-- soak {duration.TotalSeconds:F0}s ({label}) --");

        using var process = Process.GetCurrentProcess();
        long startAllocated = GC.GetTotalAllocatedBytes(precise: false);
        int gen0 = GC.CollectionCount(0);
        process.Refresh();
        long startWorkingSet = process.WorkingSet64;
        int startHandles = process.HandleCount;

        await Task.Delay(duration);

        process.Refresh();
        long allocated = GC.GetTotalAllocatedBytes(precise: false) - startAllocated;

        Line($"soak: {allocated / 1024 / 1024} MB allocated, {GC.CollectionCount(0) - gen0} gen0 collections, "
            + $"working set {(process.WorkingSet64 - startWorkingSet) / 1024 / 1024:+0;-0} MB, "
            + $"handles {process.HandleCount - startHandles:+0;-0}, threads {process.Threads.Count}");
    }

    private static bool IsSectionHeader(ToggleButton toggle)
        => toggle.Style is { } style
        && ReferenceEquals(style, toggle.TryFindResource("ExpandCollapseToggleButtonStyle"));

    private IEnumerable<(string Name, Action<bool> Set, Func<bool> Get)> IntegrationToggles()
    {
        yield return ("Status", v => _integrations.IntgrStatus = v, () => _integrations.IntgrStatus);
        yield return ("WindowActivity", v => _integrations.IntgrScanWindowActivity = v, () => _integrations.IntgrScanWindowActivity);
        yield return ("Time", v => _integrations.IntgrScanWindowTime = v, () => _integrations.IntgrScanWindowTime);
        yield return ("HeartRate", v => _integrations.IntgrHeartRate = v, () => _integrations.IntgrHeartRate);
        yield return ("NetworkStatistics", v => _integrations.IntgrNetworkStatistics = v, () => _integrations.IntgrNetworkStatistics);
        yield return ("MediaLink", v => _integrations.IntgrScanMediaLink = v, () => _integrations.IntgrScanMediaLink);
        yield return ("ComponentStats", v => _integrations.IntgrComponentStats = v, () => _integrations.IntgrComponentStats);
        yield return ("Soundpad", v => _integrations.IntgrSoundpad = v, () => _integrations.IntgrSoundpad);
        yield return ("Voicemod", v => _integrations.IntgrVoicemod = v, () => _integrations.IntgrVoicemod);
        yield return ("Twitch", v => _integrations.IntgrTwitch = v, () => _integrations.IntgrTwitch);
        yield return ("TikTokLive", v => _integrations.IntgrTikTokLive = v, () => _integrations.IntgrTikTokLive);
        yield return ("Discord", v => _integrations.IntgrDiscord = v, () => _integrations.IntgrDiscord);
        yield return ("Spotify", v => _integrations.IntgrSpotify = v, () => _integrations.IntgrSpotify);
        yield return ("VrcRadar", v => _integrations.IntgrVrcRadar = v, () => _integrations.IntgrVrcRadar);
        yield return ("TrackerBattery", v => _integrations.IntgrTrackerBattery = v, () => _integrations.IntgrTrackerBattery);
        yield return ("VrPerformance", v => _integrations.IntgrVrPerformance = v, () => _integrations.IntgrVrPerformance);
        yield return ("Lyrics", v => _integrations.IntgrLyrics = v, () => _integrations.IntgrLyrics);
    }

    private void Line(string text)
    {
        _report.Append("[Scenario] ").AppendLine(text);
        PerfProbe.Mark(text);
    }

    private static string Slug(string text)
        => new(text.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray());
}
