using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using vrcosc_magicchatbox.Services;
using Xunit;

namespace MagicChatbox.Tests.Core;

// The page index is persisted to settings and restored on launch, and the visibility converter
// returns Hidden for every non-match. So an index no page answers to means a window with nothing in
// it and no error anywhere. Nothing pinned this before a fifth page existed.
public class PageIndexRatchetTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "vrcosc-magicchatbox", "Core")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new DirectoryNotFoundException("repo root not found");
    }

    private static string AppFile(params string[] parts)
        => Path.Combine(new[] { RepoRoot(), "vrcosc-magicchatbox" }.Concat(parts).ToArray());

    private static IReadOnlyList<int> PageIndicesInXaml()
    {
        string xaml = File.ReadAllText(AppFile("MainWindow.xaml"));

        return Regex.Matches(xaml, @"<pages:\w+[^>]*?ConverterParameter=(\d+)", RegexOptions.Singleline)
            .Select(m => int.Parse(m.Groups[1].Value))
            .Distinct()
            .OrderBy(i => i)
            .ToList();
    }

    // A tab is a Button carrying TopBarTabStyle, wrapped in a Grid that places it in a top-bar column.
    // CommandParameter is the page it switches to; ConverterParameter is the index it lights up for.
    private static IReadOnlyList<(int Column, int Command, int Converter)> TabsInXaml()
    {
        string xaml = File.ReadAllText(AppFile("MainWindow.xaml"));

        return Regex.Matches(xaml, @"<Grid\s+Grid\.Column=""(?<col>\d+)""[^>]*>\s*<Button\b(?<btn>.*?)/>", RegexOptions.Singleline)
            .Where(m => m.Groups["btn"].Value.Contains("TopBarTabStyle", StringComparison.Ordinal))
            .Select(m => (
                Column: int.Parse(m.Groups["col"].Value),
                Command: int.Parse(Regex.Match(m.Groups["btn"].Value, @"CommandParameter=""(\d+)""").Groups[1].Value),
                Converter: int.Parse(Regex.Match(m.Groups["btn"].Value, @"ConverterParameter=(\d+)").Groups[1].Value)))
            .ToList();
    }

    [Fact]
    public void Every_page_index_has_a_tab_that_reaches_it()
    {
        // The Avatar page shipped hosted, in range, contiguous and behind the ceiling - every test in
        // this file passed - and no button anywhere passed "4" to ChangeMenuCommand, so the only way
        // to reach it was a page the user could not open. Pages and tabs are two lists that have to
        // match, and until now only one of them was pinned.
        Assert.Equal(
            PageIndicesInXaml(),
            TabsInXaml().Select(t => t.Command).Distinct().OrderBy(i => i).ToList());
    }

    [Fact]
    public void Each_tab_marks_itself_active_with_its_own_index()
    {
        // Copy a tab, change CommandParameter, forget ConverterParameter: the page switches while the
        // tab you came from stays lit. Nothing throws.
        var mismatched = TabsInXaml().Where(t => t.Command != t.Converter).ToList();

        Assert.True(
            mismatched.Count == 0,
            "tabs whose command and highlight disagree: "
            + string.Join(", ", mismatched.Select(t => $"command {t.Command} highlights {t.Converter}")));
    }

    [Fact]
    public void Every_tab_sits_in_its_own_top_bar_column()
    {
        // Grid does not clip and does not complain: two things in one cell just draw on top of each
        // other. Checking only that a tab's column exists is not enough - the bar ends in a star
        // column that the right-hand link cluster is right-aligned inside, so a tab that lands there
        // is overdrawn by the GitHub and wiki icons with nothing reported anywhere.
        string xaml = File.ReadAllText(AppFile("MainWindow.xaml"));

        // The window declares several column sets; the tab bar's is the last one opened before the
        // first tab. Matching the first set in the file finds the window's own two-column split.
        int firstTab = xaml.IndexOf("MenuButton_0", StringComparison.Ordinal);
        Assert.True(firstTab > 0, "could not find the first top bar tab");

        Match bar = Regex.Matches(
                xaml[..firstTab],
                @"<Grid\.ColumnDefinitions>.*?</Grid\.ColumnDefinitions>",
                RegexOptions.Singleline)
            .Last();

        var widths = Regex.Matches(bar.Value, @"<ColumnDefinition(?<attrs>[^>]*)/>")
            .Select(m => m.Groups["attrs"].Value)
            .ToList();

        int starColumn = widths.FindIndex(a => !a.Contains("Width=", StringComparison.Ordinal));
        Assert.True(starColumn >= 0, "the top bar has no star column for the right-hand cluster");

        var tabs = TabsInXaml();

        Assert.Equal(tabs.Count, tabs.Select(t => t.Column).Distinct().Count());
        Assert.All(tabs, t => Assert.InRange(t.Column, 0, starColumn - 1));

        // The link cluster, identified by the margin that clears the window controls, has to start
        // after the last tab. Forgetting to shift it when a tab is added puts both in one cell.
        Match links = Regex.Match(xaml, @"<Grid\s+Grid\.Column=""(?<col>\d+)""[^>]*Margin=""0,0,233,0""", RegexOptions.Singleline);
        Assert.True(links.Success, "could not find the right-hand link cluster");
        Assert.True(
            int.Parse(links.Groups["col"].Value) > tabs.Max(t => t.Column),
            $"the link cluster sits in column {links.Groups["col"].Value}, at or before the last tab");
    }

    [Fact]
    public void The_fixed_tab_columns_leave_room_for_the_right_hand_cluster()
    {
        // The bar's last column is a star, so it absorbs whatever the fixed columns do not take and
        // never reports a problem. Overfill the fixed ones and the star shrinks under the width the
        // link cluster reserves; being right-aligned, the cluster is then arranged at a negative
        // offset and slides over the last tab. Necessary, not sufficient - it does not account for
        // the link buttons themselves - but it catches a tab added without checking the arithmetic.
        string xaml = File.ReadAllText(AppFile("MainWindow.xaml"));

        double minWidth = double.Parse(Regex.Match(xaml, @"MinWidth=""(\d+)""").Groups[1].Value);

        int firstTab = xaml.IndexOf("MenuButton_0", StringComparison.Ordinal);
        Match bar = Regex.Matches(
                xaml[..firstTab],
                @"<Grid\.ColumnDefinitions>.*?</Grid\.ColumnDefinitions>",
                RegexOptions.Singleline)
            .Last();

        double fixedWidth = Regex.Matches(bar.Value, @"<ColumnDefinition\s+Width=""(\d+)""\s*/>")
            .Sum(m => double.Parse(m.Groups[1].Value));

        double reserved = double.Parse(
            Regex.Match(xaml, @"Margin=""0,0,(\d+),0""").Groups[1].Value);

        Assert.True(
            fixedWidth + reserved <= minWidth,
            $"the tab columns total {fixedWidth:N0}px and the right-hand cluster reserves {reserved:N0}px, "
            + $"which does not fit the {minWidth:N0}px minimum window");
    }

    [Fact]
    public void Every_page_index_is_inside_the_navigation_ceiling()
    {
        var indices = PageIndicesInXaml();

        Assert.NotEmpty(indices);

        var outOfRange = indices.Where(i => i < 0 || i > MenuNavigationService.MaxPageIndex).ToList();

        Assert.True(
            outOfRange.Count == 0,
            $"pages exist at indices navigation refuses to reach: {string.Join(", ", outOfRange)} "
            + $"(MaxPageIndex is {MenuNavigationService.MaxPageIndex})");
    }

    [Fact]
    public void The_navigation_ceiling_has_a_page_behind_it()
    {
        // The other direction: a ceiling higher than the pages means a restored index can land on
        // nothing. Both guards and the clamp read the same const, so this pins all three.
        var indices = PageIndicesInXaml();

        Assert.Contains(MenuNavigationService.MaxPageIndex, indices);
    }

    [Fact]
    public void The_page_indices_are_contiguous_from_zero()
    {
        var indices = PageIndicesInXaml();

        Assert.Equal(
            Enumerable.Range(0, MenuNavigationService.MaxPageIndex + 1).ToList(),
            indices);
    }

    [Fact]
    public void Both_navigation_guards_use_the_shared_ceiling()
    {
        // Widening only one guard leaves back and forward silently refusing to record the page - a
        // bug with no error, found only by somebody pressing the mouse's fourth button.
        string source = File.ReadAllText(AppFile("Services", "MenuNavigationService.cs"));

        int guards = Regex.Matches(source, @"pageIndex\s*<\s*0\s*\|\|\s*pageIndex\s*>\s*MaxPageIndex").Count;

        Assert.True(guards >= 2, $"expected both navigation guards to read MaxPageIndex, found {guards}");
        Assert.DoesNotContain("pageIndex > 3", source, StringComparison.Ordinal);
    }

    [Fact]
    public void The_restored_page_index_is_clamped()
    {
        // Without this, rolling back to a build with fewer pages restores an index nothing answers to.
        string source = File.ReadAllText(AppFile("MainWindow.xaml.cs"));

        Assert.Contains("Math.Clamp", source, StringComparison.Ordinal);
        Assert.Contains("MaxPageIndex", source, StringComparison.Ordinal);
    }
}
