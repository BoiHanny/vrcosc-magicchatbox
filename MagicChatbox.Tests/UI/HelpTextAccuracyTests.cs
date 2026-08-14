using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace MagicChatbox.Tests.UI;

public class HelpTextAccuracyTests
{
    [Fact]
    public void Every_newline_tip_names_the_escape_the_builder_actually_expands()
    {
        // A tip naming the wrong escape is worse than no tip: the user follows it, the escape is
        // printed verbatim in the chatbox and it eats two of the 144 characters.
        string expected = BuilderNewlineEscape();
        var offenders = new List<string>();

        foreach (string file in AppXamlFiles())
        {
            foreach (Match attribute in Regex.Matches(File.ReadAllText(file), @"Text=""([^""]*)"""))
            {
                string text = attribute.Groups[1].Value;
                if (!text.Contains("line", StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (Match escape in Regex.Matches(text, @"(?<![A-Za-z0-9])([/\\]n)\b"))
                {
                    if (escape.Groups[1].Value != expected)
                        offenders.Add($"{Path.GetFileName(file)} -> {text}");
                }
            }
        }

        Assert.True(offenders.Count == 0, $"help text promising an escape other than {expected}: " + string.Join(" | ", offenders));
    }

    [Fact]
    public void The_reorder_dialog_claims_no_control_over_drop_order()
    {
        // The saved order drives assembly position only. The trim loop picks its victim by the
        // provider's hardcoded Priority, which this dialog cannot reach, so it must not promise it.
        string builder = File.ReadAllText(Path.Combine(AppDir(), "Core", "Osc", "OscOutputBuilder.cs"));
        Assert.Matches(@"collected\[i\]\.Priority > collected\[worstIdx\]\.Priority", builder);

        string codeBehind = File.ReadAllText(Path.Combine(AppDir(), "UI", "Dialogs", "ReorderIntegrations.xaml.cs"));
        Assert.DoesNotContain("Priority", codeBehind, StringComparison.OrdinalIgnoreCase);

        string blurb = IntroText(File.ReadAllText(Path.Combine(AppDir(), "UI", "Dialogs", "ReorderIntegrations.xaml")));
        Assert.DoesNotContain("priority", blurb, StringComparison.OrdinalIgnoreCase);
    }

    private static string IntroText(string xaml)
    {
        var match = Regex.Match(xaml, @"Foreground=""LightYellow""\s*\r?\n\s*Text=""([^""]*)""");
        Assert.True(match.Success, "the reorder dialog's explanatory line was not found");

        return match.Groups[1].Value;
    }

    private static string BuilderNewlineEscape()
    {
        string builder = File.ReadAllText(Path.Combine(AppDir(), "Core", "Osc", "OscOutputBuilder.cs"));
        var match = Regex.Match(builder, @"Replace\(""([^""]+)"",\s*""\\n""\)");
        Assert.True(match.Success, "ExpandNewlines no longer replaces a single escape sequence");

        return match.Groups[1].Value.Replace(@"\\", @"\");
    }

    private static IEnumerable<string> AppXamlFiles()
        => Directory.EnumerateFiles(AppDir(), "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

    private static string AppDir() => Path.Combine(FindRepoRoot(), "vrcosc-magicchatbox");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "vrcosc-magicchatbox", "Core")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new DirectoryNotFoundException("repo root not found from " + AppContext.BaseDirectory);
    }
}
