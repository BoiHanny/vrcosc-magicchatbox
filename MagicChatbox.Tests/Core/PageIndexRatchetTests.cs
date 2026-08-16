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
