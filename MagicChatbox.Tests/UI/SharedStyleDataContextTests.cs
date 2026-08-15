using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace MagicChatbox.Tests.UI;

/// <summary>
/// A shared style must not care whose DataContext it lands on.
/// </summary>
/// <remarks>
/// A trigger written as <c>{Binding SomeProperty}</c> inside App.xaml or Theme.xaml resolves against
/// whatever the element applying the style happens to be bound to. Where that matches, it works;
/// where it does not, WPF resolves the path by exception, logs a binding failure, and the trigger
/// simply never fires - so the feature is silently missing rather than broken loudly. Three of these
/// were live at once: two route toggles whose "you are on desktop" overlay never appeared, and an
/// options button wearing the style for a status-list item, asking status-list questions of a
/// settings view model on every template application.
///
/// A binding like this is only ever correct when the style is written for one specific shape of
/// DataContext, which is why the exceptions below name the shape rather than just the style.
/// </remarks>
public class SharedStyleDataContextTests
{
    /// <summary>
    /// Style key to the DataContext it is written for. Adding a row here is a claim that the style
    /// is only ever applied to that shape - and that a call site putting it anywhere else is a bug
    /// in the call site.
    /// </summary>
    private static readonly Dictionary<string, string> WrittenForOneShape = new(StringComparer.Ordinal)
    {
        ["Status_Button_style_Small"] = "a StatusItem inside the status ItemsControl",
        ["TrackerBatteryValueText"] = "a tracker battery reading",
        ["CharacterCountTextStyle"] = "the chatting page view model",
        ["ChatMessageCard"] = "a ChatItem in the message list",
    };

    [Fact]
    public void No_shared_style_binds_to_a_DataContext_it_cannot_be_sure_of()
    {
        var offenders = new List<string>();

        foreach (string file in new[] { "App.xaml", Path.Combine("UI", "Theme.xaml") })
        {
            string xaml = File.ReadAllText(AppFile(file));

            foreach (Match match in Regex.Matches(xaml, @"Binding=""\{Binding ([^""]*)\}"""))
            {
                string expression = match.Groups[1].Value;

                // The path comes first and the source qualifier after it, so this cannot be decided
                // by what the expression starts with - only by whether it names a source at all.
                if (expression.Contains("RelativeSource", StringComparison.Ordinal)
                    || expression.Contains("Source=", StringComparison.Ordinal)
                    || expression.Contains("ElementName=", StringComparison.Ordinal))
                    continue;

                string owner = StyleKeyAbove(xaml, match.Index);
                if (WrittenForOneShape.ContainsKey(owner))
                    continue;

                offenders.Add($"{Path.GetFileName(file)} -> {owner} binds {expression}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "a shared style is reaching into the DataContext of whoever applies it. Either bind through "
            + "an explicit Source/RelativeSource, or add it to WrittenForOneShape saying which shape it "
            + "is for:\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void The_route_toggles_that_never_resolved_are_gone()
    {
        string app = File.ReadAllText(AppFile("App.xaml"));

        Assert.DoesNotContain(@"x:Key=""GlowyToggleButtonDT""", app);
        Assert.DoesNotContain(@"x:Key=""GlowyToggleButtonVR""", app);
    }

    [Fact]
    public void Every_style_the_options_sections_name_is_one_that_still_exists()
    {
        // Deleting a style is only safe if nothing still asks for it, and a StaticResource that names
        // nothing throws when the section is opened rather than when it is built.
        var declared = new HashSet<string>(StringComparer.Ordinal);
        declared.UnionWith(Keys(File.ReadAllText(AppFile("App.xaml"))));
        declared.UnionWith(Keys(File.ReadAllText(AppFile(Path.Combine("UI", "Theme.xaml")))));
        declared.UnionWith(Keys(File.ReadAllText(AppFile(Path.Combine("UI", "Resources", "SharedConverters.xaml")))));

        var missing = new List<string>();

        foreach (string section in Directory.GetFiles(AppFile(Path.Combine("UI", "Pages", "Options")), "*.xaml"))
        {
            string xaml = File.ReadAllText(section);
            var local = new HashSet<string>(declared, StringComparer.Ordinal);
            local.UnionWith(Keys(xaml));

            missing.AddRange(
                Regex.Matches(xaml, @"\{(?:Static|Dynamic)Resource\s+([A-Za-z0-9_.]+)\s*\}")
                    .Select(m => m.Groups[1].Value)
                    .Distinct(StringComparer.Ordinal)
                    .Where(key => !local.Contains(key))
                    .Select(key => Path.GetFileName(section) + " -> " + key));
        }

        Assert.True(missing.Count == 0, "options sections name resources nothing defines: " + string.Join(", ", missing));
    }

    /// <summary>The nearest x:Key declared before this point, which is the style the trigger sits in.</summary>
    private static string StyleKeyAbove(string xaml, int index)
    {
        var keys = Regex.Matches(xaml[..index], @"x:Key=""([^""]+)""");
        return keys.Count == 0 ? "(no style)" : keys[^1].Groups[1].Value;
    }

    private static IEnumerable<string> Keys(string xaml)
        => Regex.Matches(xaml, @"x:Key=""([^""]+)""").Select(m => m.Groups[1].Value);

    private static string AppFile(string relativePath)
        => Path.Combine(FindRepoRoot(), "vrcosc-magicchatbox", relativePath);

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "vrcosc-magicchatbox", "Core")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new DirectoryNotFoundException("repo root not found from " + AppContext.BaseDirectory);
    }
}
