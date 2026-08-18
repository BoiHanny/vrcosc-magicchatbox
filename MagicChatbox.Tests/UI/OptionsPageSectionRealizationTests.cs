using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using vrcosc_magicchatbox.UI.Pages;
using Xunit;

namespace MagicChatbox.Tests.UI;

/// <summary>
/// The options page builds three sections up front and the remaining nineteen in background chunks,
/// so that opening it does not hitch. This checks the sections all arrive.
/// </summary>
/// <remarks>
/// Deferring them moved nineteen x:Names out of the generated fields and into ones the page recovers
/// by hand after each chunk is loaded. A name that no longer matches leaves a null behind, and the
/// only thing that reads those fields is the deep link from the tray menu - which fails silently,
/// scrolling nowhere, and no other test would notice.
/// </remarks>
public class OptionsPageSectionRealizationTests
{
    [Fact]
    public void Every_section_the_deep_link_can_target_is_realized_and_found()
    {
        Dictionary<string, FrameworkElement>? map = null;

        Exception? failure = WpfHost.RunInWindow(
            () => new OptionsPage(),
            page =>
            {
                var ensure = typeof(OptionsPage).GetMethod(
                    "EnsureSectionMap", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(ensure);
                ensure!.Invoke(page, null);

                var field = typeof(OptionsPage).GetField(
                    "_sectionMap", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(field);
                map = (Dictionary<string, FrameworkElement>?)field!.GetValue(page);
            });

        Assert.True(failure == null, "the options page did not build: " + failure);
        Assert.NotNull(map);

        var missing = new List<string>();
        foreach (KeyValuePair<string, FrameworkElement> entry in map!)
        {
            if (entry.Value == null)
                missing.Add(entry.Key);
        }

        Assert.True(missing.Count == 0, "deep link targets nothing: " + string.Join(", ", missing));
        Assert.True(map.Count >= 23, "expected every section to be mapped, got " + map.Count);
    }

    [Fact]
    public void The_deferred_chunks_all_reach_the_panel()
    {
        int? childCount = null;

        Exception? failure = WpfHost.RunInWindow(
            () => new OptionsPage(),
            page =>
            {
                var realize = typeof(OptionsPage).GetMethod(
                    "EnsureSectionsRealized", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(realize);
                realize!.Invoke(page, null);

                var pending = typeof(OptionsPage).GetField(
                    "_pendingChunkKeys", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(pending);
                var queue = (Queue<string>?)pending!.GetValue(page);
                Assert.NotNull(queue);
                Assert.Empty(queue!);

                childCount = ((System.Windows.Controls.Panel)page.FindName("SectionsPanel")!).Children.Count;
            });

        Assert.True(failure == null, "the options page did not build: " + failure);
        Assert.NotNull(childCount);
        Assert.True(childCount >= 7, "expected the deferred chunks to be appended, got " + childCount);
    }

}
