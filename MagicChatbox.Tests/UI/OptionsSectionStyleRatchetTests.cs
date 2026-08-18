using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace MagicChatbox.Tests.UI;

/// <summary>
/// A ratchet, not an audit. Every finding it guards was correct once and then copy-pasted across the
/// options page: raw point sizes off the shared scale, a font named by path in each call site, an
/// element that applies a shared style and then overrides the very properties that style exists to
/// set. Sections join the list as they are cleaned; the point is that a cleaned one cannot drift back.
/// </summary>
public class OptionsSectionStyleRatchetTests
{
    private static readonly string[] CleanedSections =
    [
        "AppOptionsSection",
        "EggDevSection",
        "OpenAISection",
        "PrivacySection",
        "TtsOptionsSection",
    ];

    /// <summary>Margin and Padding place a control; the shared styles do not own placement.</summary>
    private static readonly string[] TypographyProperties =
        ["FontFamily", "FontSize", "FontWeight", "FontStyle", "Foreground"];

    [Fact]
    public void No_cleaned_section_states_a_point_size_of_its_own()
    {
        var offenders = Scan((name, xaml) =>
            Regex.Matches(xaml, @"FontSize=""(\d[\d.]*)""")
                 .Select(m => $"{name}: FontSize={m.Groups[1].Value}"));

        Assert.True(offenders.Count == 0, "raw font sizes: " + string.Join(", ", offenders));
    }

    [Fact]
    public void No_cleaned_section_names_a_font_by_path()
    {
        // /Fonts/#Albert Sans is right, but repeating it inline is how three files ended up with
        // subsection headers in a different face from everywhere else.
        var offenders = Scan((name, xaml) =>
            Regex.Matches(xaml, @"FontFamily=""(/Fonts/[^""]*)""")
                 .Select(m => $"{name}: {m.Groups[1].Value}"));

        Assert.True(offenders.Count == 0, "inline font paths: " + string.Join(", ", offenders));
    }

    [Fact]
    public void No_cleaned_section_overrides_the_typography_of_a_style_it_applies()
    {
        var offenders = Scan((name, xaml) =>
            from Match element in Regex.Matches(xaml, @"<\w+\b[^<>]*?/?>", RegexOptions.Singleline)
            where element.Value.Contains("Resource Option", StringComparison.Ordinal)
               && element.Value.Contains("Style=", StringComparison.Ordinal)
            from property in TypographyProperties
            where element.Value.Contains(property + "=", StringComparison.Ordinal)
            select $"{name}: {property}");

        Assert.True(offenders.Count == 0, "shared style overridden locally: " + string.Join(", ", offenders));
    }

    [Fact]
    public void No_cleaned_section_reaches_for_the_empty_checkbox_alias()
    {
        // SettingsCheckbox is a BasedOn alias with no setters of its own; the implicit CheckBox
        // style already does the work, and the checkboxes that omitted it always rendered the same.
        var offenders = Scan((name, xaml) =>
            xaml.Contains("SettingsCheckbox", StringComparison.Ordinal) ? [name] : Array.Empty<string>());

        Assert.True(offenders.Count == 0, "SettingsCheckbox referenced by: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Privacy_expands_on_the_same_persisted_flag_as_every_other_section()
    {
        // Its expander lived on a view-model field, so it was the only section that forgot whether
        // you had it open, and the only one left hanging open when you jumped somewhere else.
        string xaml = File.ReadAllText(SectionPath("PrivacySection"));
        Assert.Contains("AppSettings.Settings_Privacy", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Binding IsExpanded", xaml, StringComparison.Ordinal);

        string appSettings = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "vrcosc-magicchatbox", "Classes", "Modules", "AppSettings.cs"));
        Assert.Contains("_settings_Privacy", appSettings, StringComparison.Ordinal);

        // In the navigation sweep too, or opening another section would leave Privacy behind.
        string nav = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "vrcosc-magicchatbox", "Services", "MenuNavigationService.cs"));
        Assert.Contains("appSettings.Settings_Privacy = v", nav, StringComparison.Ordinal);
    }

    [Fact]
    public void The_ratchet_is_pointed_at_files_that_exist()
    {
        foreach (string name in CleanedSections)
            Assert.True(File.Exists(SectionPath(name)), name + " is listed as cleaned but was not found");
    }

    private static List<string> Scan(Func<string, string, IEnumerable<string>> find)
        => CleanedSections.SelectMany(name => find(name, File.ReadAllText(SectionPath(name)))).ToList();

    private static string SectionPath(string name)
        => Path.Combine(FindRepoRoot(), "vrcosc-magicchatbox", "UI", "Pages", "Options", name + ".xaml");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "vrcosc-magicchatbox", "Core")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new DirectoryNotFoundException("repo root not found from " + AppContext.BaseDirectory);
    }
}
