using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace MagicChatbox.Tests.UI;

/// <summary>
/// Every Window must paint its own background.
/// </summary>
/// <remarks>
/// A Window with no Background falls back to SystemColors.WindowBrush, which is white. Putting the
/// dark colour on an inner Border is not the same thing: the window is on screen before that Border
/// renders, so the user gets a white flash the size of the window every time it opens. It is
/// invisible in a screenshot of the finished window and only shows while it is loading.
/// </remarks>
public class WindowBackgroundTests
{
    private static readonly Regex WindowHead = new(@"^\s*<Window\b(.*?)>", RegexOptions.Singleline);

    public static TheoryData<string> WindowFiles()
    {
        var data = new TheoryData<string>();
        foreach (string file in EnumerateXaml())
        {
            if (WindowHead.IsMatch(File.ReadAllText(file)))
                data.Add(Path.GetFileName(file));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(WindowFiles))]
    public void A_window_paints_its_own_background(string fileName)
    {
        string path = EnumerateXaml().First(f => Path.GetFileName(f) == fileName);
        Match head = WindowHead.Match(File.ReadAllText(path));

        Assert.True(
            head.Groups[1].Value.Contains("Background", StringComparison.Ordinal),
            $"{fileName} has no Background on its Window element, so it will flash white while it loads.");
    }

    [Fact]
    public void The_scan_actually_found_the_windows()
    {
        // A regex that quietly matches nothing would make every case above pass.
        Assert.True(WindowFiles().Count >= 10);
    }

    private static IEnumerable<string> EnumerateXaml()
    {
        string root = RepoRoot();
        return Directory
            .EnumerateFiles(Path.Combine(root, "vrcosc-magicchatbox"), "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "vrcosc-magicchatbox")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
