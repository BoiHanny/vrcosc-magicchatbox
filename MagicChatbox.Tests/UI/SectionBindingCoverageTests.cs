using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using vrcosc_magicchatbox.Classes.Modules;
using Xunit;

namespace MagicChatbox.Tests.UI;

public class SectionBindingCoverageTests
{
    [Fact]
    public void Every_WindowActivity_setting_is_reachable_from_its_options_section()
    {
        // A setting the module reads but no control writes is a feature the user cannot turn off.
        var unreachable = PersistedProperties(typeof(WindowActivitySettings))
            .Where(name => !SectionXaml("WindowActivitySection").Contains($"WindowActivitySettings.{name}", StringComparison.Ordinal))
            .ToList();

        Assert.True(unreachable.Count == 0, "no control binds: " + string.Join(", ", unreachable));
    }

    [Fact]
    public void The_enhanced_hook_checkbox_does_not_bind_the_decoy_property()
    {
        // IntegrationSettings carries an identically named ApplicationHookV2 that nothing reads.
        // Binding it persists a value the window-activity module never consults.
        Assert.DoesNotContain("IntegrationSettings.ApplicationHookV2", SectionXaml("WindowActivitySection"), StringComparison.Ordinal);
    }

    [Fact]
    public void Every_Weather_setting_is_reachable_from_a_control()
    {
        // WeatherLocationCityEncrypted is the on-disk form of WeatherLocationCity, and
        // WeatherConditionOverrides is edited through the override grid rather than by name.
        var skip = new HashSet<string> { "WeatherLocationCityEncrypted", "WeatherConditionOverrides" };
        string allXaml = string.Concat(AppXamlFiles().Select(File.ReadAllText));

        var unreachable = PersistedProperties(typeof(WeatherSettings))
            .Where(name => !skip.Contains(name))
            .Where(name => !allXaml.Contains($"WeatherSettings.{name}", StringComparison.Ordinal))
            .ToList();

        Assert.True(unreachable.Count == 0, "no control binds: " + string.Join(", ", unreachable));
    }

    [Fact]
    public void Every_ComponentStats_setting_is_reachable_from_a_control()
    {
        // The legacy pair is not settable any more: it is what a pre-scales settings file wrote,
        // read once on load to work out which scales the user had chosen, and never again.
        var skip = new HashSet<string> { "IsFahrenheit", "IsTemperatureSwitchEnabled" };
        string allXaml = string.Concat(AppXamlFiles().Select(File.ReadAllText));

        var unreachable = PersistedProperties(typeof(ComponentStatsSettings))
            .Where(name => !skip.Contains(name))
            .Where(name => !allXaml.Contains($"Settings.{name}", StringComparison.Ordinal))
            .ToList();

        Assert.True(unreachable.Count == 0, "no control binds: " + string.Join(", ", unreachable));
    }

    private static IEnumerable<string> PersistedProperties(Type settingsType)
        => settingsType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(p => p.GetSetMethod() != null)
            .Select(p => p.Name);

    private static string SectionXaml(string name)
        => File.ReadAllText(Path.Combine(FindRepoRoot(), "vrcosc-magicchatbox", "UI", "Pages", "Options", name + ".xaml"));

    private static IEnumerable<string> AppXamlFiles()
        => Directory.EnumerateFiles(Path.Combine(FindRepoRoot(), "vrcosc-magicchatbox"), "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "vrcosc-magicchatbox", "Core")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new DirectoryNotFoundException("repo root not found from " + AppContext.BaseDirectory);
    }
}
