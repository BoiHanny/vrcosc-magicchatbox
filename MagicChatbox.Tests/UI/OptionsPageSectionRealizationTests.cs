using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using vrcosc_magicchatbox.UI.Pages;
using vrcosc_magicchatbox.UI.Pages.Options;
using Xunit;

namespace MagicChatbox.Tests.UI;

/// <summary>
/// The options page shows every section's title immediately, with a "Loading…" placeholder standing
/// in for the eighteen sections it builds in background chunks, so opening it does not hitch. This
/// checks the placeholders get replaced and the deep link map is fully populated once they do.
/// </summary>
/// <remarks>
/// The only thing that reads the realized section references is the deep link from the tray menu -
/// which fails silently, scrolling nowhere, and no other test would notice.
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
    public void The_deferred_chunks_all_replace_their_placeholders()
    {
        Type? spotifyContentBefore = null;
        Type? eggDevContentBefore = null;
        Type? spotifyContentAfter = null;
        Type? eggDevContentAfter = null;

        Exception? failure = WpfHost.RunInWindow(
            () => new OptionsPage(),
            page =>
            {
                var spotifyWrapper = (ContentControl)page.FindName("OptionsWrapper_Spotify")!;
                var eggDevWrapper = (ContentControl)page.FindName("OptionsWrapper_EggDev")!;

                spotifyContentBefore = spotifyWrapper.Content.GetType();
                eggDevContentBefore = eggDevWrapper.Content.GetType();

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

                spotifyContentAfter = spotifyWrapper.Content.GetType();
                eggDevContentAfter = eggDevWrapper.Content.GetType();
            });

        Assert.True(failure == null, "the options page did not build: " + failure);
        Assert.Equal(typeof(StackPanel), spotifyContentBefore);
        Assert.Equal(typeof(StackPanel), eggDevContentBefore);
        Assert.Equal(typeof(SpotifySection), spotifyContentAfter);
        Assert.Equal(typeof(EggDevSection), eggDevContentAfter);
    }

}
