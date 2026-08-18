using System;
using System.Diagnostics;
using System.Windows;
using Xunit;
using Xunit.Abstractions;

namespace MagicChatbox.Tests.UI;

/// <summary>
/// What it costs to build each page, now that leaving one destroys it.
/// </summary>
/// <remarks>
/// Tearing pages down trades a steady cost - every binding on three pages nobody is looking at,
/// firing on every notification from the singleton view models behind them - for a one-off rebuild
/// per visit. That trade is only worth making while the rebuild is small, so this measures it rather
/// than assuming it, and prints the numbers.
/// </remarks>
public class PageBuildCostTests
{
    private readonly ITestOutputHelper _out;

    public PageBuildCostTests(ITestOutputHelper output) => _out = output;

    [Theory]
    [InlineData("Options")]
    [InlineData("Integrations")]
    [InlineData("Status")]
    [InlineData("Chatting")]
    public void Rebuilding_a_page_stays_inside_a_navigation(string page)
    {
        double first = 0;
        double warm = 0;
        long bytes = 0;

        Exception? failure = WpfHost.Run(() =>
        {
            var sw = Stopwatch.StartNew();
            _ = Make(page);
            first = sw.Elapsed.TotalMilliseconds;

            // The first build pays for BAML load and JIT; what a navigation actually costs is the
            // steady-state one.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            long before = GC.GetTotalMemory(true);

            sw.Restart();
            FrameworkElement kept = Make(page);
            warm = sw.Elapsed.TotalMilliseconds;

            bytes = GC.GetTotalMemory(false) - before;
            GC.KeepAlive(kept);
        });

        Assert.True(failure == null, page + " did not build: " + failure);

        _out.WriteLine($"{page,-13} first={first,8:F1}ms  warm={warm,8:F1}ms  ~{bytes / 1024,6} KB");

        // Pages are torn down when you navigate away, so this cost is paid again on every visit.
        // Measured at 8-27ms; the bound is loose enough not to be flaky and tight enough to catch a
        // page becoming an order of magnitude heavier to build.
        Assert.True(warm < 250, $"{page} takes {warm:F0}ms to rebuild, which a navigation would show");
    }

    private static FrameworkElement Make(string page) => page switch
    {
        "Options" => new vrcosc_magicchatbox.UI.Pages.OptionsPage(),
        "Integrations" => new vrcosc_magicchatbox.UI.Pages.IntegrationsPage(),
        "Status" => new vrcosc_magicchatbox.UI.Pages.StatusPage(),
        "Chatting" => new vrcosc_magicchatbox.UI.Pages.ChattingPage(),
        _ => throw new ArgumentOutOfRangeException(nameof(page)),
    };
}
