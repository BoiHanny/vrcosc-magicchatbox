using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace MagicChatbox.Tests.UI;

// A misspelled resource key compiles cleanly and only fails when a human opens the page. That is
// exactly how the Avatar page shipped referencing "FontSizeHeader" when the token is "FontSizeHeading":
// 1749 tests passed, the build was clean, and the page threw XamlParseException on first click.
//
// StaticResource throws; DynamicResource fails silently and just drops the value, which is quieter and
// therefore worse. Both are pinned here.
//
// Scope note: WPF resolves a key by walking the element tree outwards and then the application
// dictionaries. This checks the two ends of that walk -- keys declared in the same file, and keys in
// the app-scope dictionaries reachable from App.xaml -- which is where every key in this codebase
// actually lives. It cannot see a key declared on a nested element in another file, so it is a lower
// bound on correctness. It catches typos, which is the failure mode that has actually bitten.
public class XamlResourceKeyTests
{
    private static readonly Regex KeyDeclaration = new(@"x:Key=""([^""]+)""", RegexOptions.Compiled);

    [Fact]
    public void Every_StaticResource_key_in_app_xaml_resolves()
    {
        var offenders = UnresolvedKeys("StaticResource");

        Assert.True(
            offenders.Count == 0,
            "StaticResource keys that resolve to nothing (these throw XamlParseException when the page loads): "
                + string.Join(", ", offenders));
    }

    [Fact]
    public void Every_DynamicResource_key_in_app_xaml_resolves()
    {
        var offenders = UnresolvedKeys("DynamicResource");

        Assert.True(
            offenders.Count == 0,
            "DynamicResource keys that resolve to nothing (these fail silently and drop the value): "
                + string.Join(", ", offenders));
    }

    [Fact]
    public void The_app_scope_dictionaries_are_actually_found()
    {
        // Without this, a change to how App.xaml merges its dictionaries would empty the app scope and
        // turn both tests above into a check that passes because it looked at nothing.
        HashSet<string> appScope = AppScopeKeys();

        Assert.True(appScope.Count > 100, $"app scope collapsed to {appScope.Count} keys");
        Assert.Contains("FontSizeHeading", appScope);
        Assert.Contains("OptionCardStyle", appScope);
    }

    private static List<string> UnresolvedKeys(string extension)
    {
        var pattern = new Regex(@"\{" + extension + @"\s+(?:ResourceKey=)?([A-Za-z0-9_.]+)\s*\}");
        var elementPattern = new Regex(@"<" + extension + @"\s+ResourceKey=""([^""]+)""");

        HashSet<string> appScope = AppScopeKeys();
        var offenders = new List<string>();

        foreach (string file in AppXamlFiles())
        {
            string text = File.ReadAllText(file);
            HashSet<string> declaredHere = DeclaredKeys(text);

            IEnumerable<string> used = pattern.Matches(text)
                .Concat(elementPattern.Matches(text))
                .Select(m => m.Groups[1].Value);

            foreach (string key in used)
            {
                if (!appScope.Contains(key) && !declaredHere.Contains(key))
                    offenders.Add($"{Path.GetFileName(file)} -> {key}");
            }
        }

        return offenders.Distinct().OrderBy(o => o, StringComparer.Ordinal).ToList();
    }

    private static HashSet<string> AppScopeKeys()
    {
        // Follows App.xaml's merged dictionaries rather than naming them, so adding a dictionary does
        // not silently shrink what this test can see.
        string appDir = Path.Combine(FindRepoRoot(), "vrcosc-magicchatbox");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<string>();
        pending.Enqueue(Path.Combine(appDir, "App.xaml"));

        while (pending.Count > 0)
        {
            string path = pending.Dequeue();
            if (!seen.Add(path) || !File.Exists(path))
                continue;

            string text = File.ReadAllText(path);
            keys.UnionWith(DeclaredKeys(text));

            foreach (Match m in Regex.Matches(text, @"<ResourceDictionary[^>]*\sSource=""([^""]+)"""))
            {
                string source = m.Groups[1].Value;
                if (!source.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                    continue;

                int component = source.IndexOf(";component/", StringComparison.OrdinalIgnoreCase);
                if (component >= 0)
                    source = source[(component + ";component/".Length)..];

                string relative = source.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                pending.Enqueue(Path.Combine(appDir, relative));
            }
        }

        return keys;
    }

    private static HashSet<string> DeclaredKeys(string xaml)
        => KeyDeclaration.Matches(xaml).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

    private static IEnumerable<string> AppXamlFiles()
        => Directory.EnumerateFiles(Path.Combine(FindRepoRoot(), "vrcosc-magicchatbox"), "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "vrcosc-magicchatbox", "Core")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new DirectoryNotFoundException("repo root not found from " + AppContext.BaseDirectory);
    }
}
