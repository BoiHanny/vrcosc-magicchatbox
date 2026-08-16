using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using vrcosc_magicchatbox.Classes.Modules;
using Xunit;

namespace MagicChatbox.Tests.Core;

// This app keeps several hand-maintained registries in separate files: a tile catalog, an opacity
// switch, a reset switch, a section map, and the XAML that names them all. Most of them fail
// SILENTLY when an entry is missed - the tile just never renders, the Reset button reports "0
// settings reset", the trim-dimming quietly does nothing. Every test here reads the real source and
// asserts the registries agree, so the next person to add an integration finds out at build time
// instead of from a bug report.
public class RegistryDriftTests
{
    [Fact]
    public void Every_options_section_offering_a_reset_button_has_a_reset_case()
    {
        // OptionsPage.xaml gives a section a Reset button by tagging it. If the service has no case
        // for that tag, the button toasts "Unknown section - 0 setting(s) reset" and the user is
        // left thinking their settings are stuck.
        var tags = MatchesIn(
            AppFile("UI", "Pages", "OptionsPage.xaml"),
            @"Tag=""([a-z0-9-]+)""");

        var cases = MatchesIn(
            AppFile("Services", "OptionsSectionResetService.cs"),
            @"case ""([a-z0-9-]+)""");

        var missing = tags.Except(cases, StringComparer.Ordinal).OrderBy(t => t).ToList();

        Assert.True(
            missing.Count == 0,
            "Options sections with a Reset button but no case in OptionsSectionResetService: "
            + string.Join(", ", missing));
    }

    [Fact]
    public void Every_provider_can_be_dimmed_when_it_is_trimmed()
    {
        // The build dims a tile when its segment gets trimmed out of the 144-char line. A provider
        // missing from SetOpacity silently never dims, so the user gets no feedback that their
        // module lost the budget fight.
        var uiKeys = ProviderUiKeys();
        var displayState = File.ReadAllText(AppFile("ViewModels", "State", "IntegrationDisplayState.cs"));

        var setCases = new HashSet<string>(
            Regex.Matches(displayState, @"case ""([A-Za-z]+)"":\s*\w+Opacity = opacity;")
                 .Select(m => m.Groups[1].Value),
            StringComparer.Ordinal);

        var missing = uiKeys.Where(k => !setCases.Contains(k)).OrderBy(k => k).ToList();

        Assert.True(
            missing.Count == 0,
            "providers with no SetOpacity case (they will never dim when trimmed): "
            + string.Join(", ", missing));
    }

    [Fact]
    public void Every_dimmable_provider_is_restored_by_a_full_reset()
    {
        // ResetAllOpacity runs when the build no longer trims anything. A provider missing from it
        // stays dimmed forever after a single trim - visually indistinguishable from "disabled".
        var displayState = File.ReadAllText(AppFile("ViewModels", "State", "IntegrationDisplayState.cs"));

        var setCases = Regex.Matches(displayState, @"case ""[A-Za-z]+"":\s*(\w+Opacity) = opacity;")
                            .Select(m => m.Groups[1].Value)
                            .ToList();

        var resetBody = Regex.Match(
            displayState,
            @"public void ResetAllOpacity\(\)\s*\{(.*?)\n    \}",
            RegexOptions.Singleline);

        Assert.True(resetBody.Success, "ResetAllOpacity not found in IntegrationDisplayState.cs");

        var reset = new HashSet<string>(
            Regex.Matches(resetBody.Groups[1].Value, @"(\w+Opacity) = ""1"";")
                 .Select(m => m.Groups[1].Value),
            StringComparer.Ordinal);

        var missing = setCases.Where(p => !reset.Contains(p)).Distinct().OrderBy(p => p).ToList();

        Assert.True(
            missing.Count == 0,
            "opacity properties that SetOpacity can dim but ResetAllOpacity never restores: "
            + string.Join(", ", missing));
    }

    [Fact]
    public void Every_catalog_tile_is_wired_into_the_integrations_page()
    {
        // IntegrationsPage.ApplyIntegrationOrder holds a literal key -> ListBoxItem map. A tile in
        // the catalog but not in that map is simply never inserted into the list: invisible, with no
        // error anywhere.
        var pageSource = File.ReadAllText(AppFile("UI", "Pages", "IntegrationsPage.xaml.cs"));

        var mapped = new HashSet<string>(
            Regex.Matches(pageSource, @"\{\s*""([A-Za-z]+)""\s*,\s*(\w+)\s*\}")
                 .Select(m => m.Groups[1].Value),
            StringComparer.OrdinalIgnoreCase);

        var missing = IntegrationTileCatalog.Tiles
            .Where(t => !mapped.Contains(t.Key))
            .Select(t => $"{t.Key} ({t.ElementName})")
            .OrderBy(t => t)
            .ToList();

        Assert.True(
            missing.Count == 0,
            "catalog tiles missing from the IntegrationsPage itemMap (they will never render): "
            + string.Join(", ", missing));
    }

    [Fact]
    public void Every_catalog_tile_names_an_element_that_exists_in_the_xaml()
    {
        // WPF accepts both Name= and x:Name= on a framework element, and this page uses the plain
        // form, so the check has to allow either or it fails on every tile.
        var xaml = File.ReadAllText(AppFile("UI", "Pages", "IntegrationsPage.xaml"));

        var declared = new HashSet<string>(
            Regex.Matches(xaml, @"(?:x:)?Name=""([A-Za-z]+)""").Select(m => m.Groups[1].Value),
            StringComparer.Ordinal);

        var missing = IntegrationTileCatalog.Tiles
            .Where(t => !declared.Contains(t.ElementName))
            .Select(t => $"{t.Key} -> {t.ElementName}")
            .OrderBy(t => t)
            .ToList();

        Assert.True(
            missing.Count == 0,
            "catalog tiles whose ElementName has no matching x:Name in IntegrationsPage.xaml: "
            + string.Join(", ", missing));
    }

    [Fact]
    public void Every_options_wrapper_is_reachable_from_the_section_map()
    {
        // Deep links ("open the Discord settings") scroll by looking the section up in _sectionMap.
        // A wrapper missing from the map lands the user on the page without scrolling anywhere.
        var declared = MatchesIn(
            AppFile("UI", "Pages", "OptionsPage.xaml"),
            @"x:Name=""(OptionsWrapper_[A-Za-z]+)""");

        var mapped = MatchesIn(
            AppFile("UI", "Pages", "OptionsPage.xaml.cs"),
            @"(OptionsWrapper_[A-Za-z]+)");

        var missing = declared.Except(mapped, StringComparer.Ordinal).OrderBy(w => w).ToList();

        Assert.True(
            missing.Count == 0,
            "option sections that exist in XAML but cannot be scrolled to: " + string.Join(", ", missing));
    }

    private static HashSet<string> ProviderUiKeys()
    {
        var providers = AppFile("Core", "Osc", "Providers");
        Assert.True(Directory.Exists(providers), $"provider folder not found: {providers}");

        return new HashSet<string>(
            Directory.EnumerateFiles(providers, "*OscProvider.cs")
                     .Select(f => Regex.Match(File.ReadAllText(f), @"UiKey\s*=>\s*""([^""]+)"""))
                     .Where(m => m.Success)
                     .Select(m => m.Groups[1].Value),
            StringComparer.Ordinal);
    }

    private static HashSet<string> MatchesIn(string path, string pattern)
    {
        Assert.True(File.Exists(path), $"file not found: {path}");

        return new HashSet<string>(
            Regex.Matches(File.ReadAllText(path), pattern).Select(m => m.Groups[1].Value),
            StringComparer.Ordinal);
    }

    private static string AppFile(params string[] parts)
        => Path.Combine(new[] { FindRepoRoot(), "vrcosc-magicchatbox" }.Concat(parts).ToArray());

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "vrcosc-magicchatbox", "Core")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new DirectoryNotFoundException("repo root not found from " + AppContext.BaseDirectory);
    }
}
