using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace MagicChatbox.Tests.UI;

/// <summary>
/// A ratchet for the four streaming sections. Every finding behind this pass was a pattern that was
/// correct once and then copy-pasted, so without a guard it drifts back within a few features.
/// </summary>
public class StreamingSectionStyleTests
{
    private static readonly string[] Sections =
    [
        "TwitchSection",
        "TikTokLiveSection",
        "DiscordSection",
        "VrcRadarSection"
    ];

    [Fact]
    public void No_section_hardcodes_a_font_size()
    {
        var offenders = Offenders(@"FontSize=""\d", "raw FontSize");

        Assert.True(offenders.Count == 0, "use the type scale tokens instead: " + string.Join(" | ", offenders));
    }

    [Fact]
    public void No_section_hardcodes_a_font_file_path()
    {
        var offenders = Offenders(@"/Fonts/#", "literal font path");

        Assert.True(offenders.Count == 0, "use FontPrimary or FontSecondary: " + string.Join(" | ", offenders));
    }

    [Fact]
    public void No_section_still_reaches_for_the_empty_checkbox_alias()
    {
        // SettingsCheckbox is a BasedOn alias with zero setters. The implicit style already does
        // the work, and the checkboxes that never named it render identically.
        var offenders = Offenders("SettingsCheckbox", "SettingsCheckbox reference");

        Assert.True(offenders.Count == 0, "delete the reference: " + string.Join(" | ", offenders));
    }

    [Fact]
    public void No_section_overrides_the_padding_its_field_label_style_exists_to_set()
    {
        var offenders = new List<string>();

        foreach (string section in Sections)
        {
            string xaml = SectionXaml(section);
            foreach (Match element in Regex.Matches(xaml, @"<TextBlock\b[^>]*?/>", RegexOptions.Singleline))
            {
                if (element.Value.Contains("OptionFieldLabelStyle", StringComparison.Ordinal)
                    && element.Value.Contains(@"Padding=""0,0,0,0""", StringComparison.Ordinal))
                {
                    offenders.Add(section);
                }
            }
        }

        Assert.True(offenders.Count == 0, "redundant Padding override in: " + string.Join(", ", offenders.Distinct()));
    }

    [Fact]
    public void Every_section_that_shapes_the_chatbox_line_shows_the_user_what_it_will_look_like()
    {
        var missing = Sections
            .Where(section => !SectionXaml(section).Contains("SegmentPreview", StringComparison.Ordinal))
            .ToList();

        Assert.True(missing.Count == 0, "no preview in: " + string.Join(", ", missing));
    }

    [Fact]
    public void No_dropdown_in_these_sections_falls_back_to_showing_member_names()
    {
        // A ComboBox bound to an enum array prints ToString() unless an ItemTemplate runs the
        // description converter. That is how the radar came to list "TransientOnly".
        var offenders = new List<string>();

        foreach (string section in Sections)
        {
            string xaml = SectionXaml(section);
            foreach (Match combo in Regex.Matches(xaml, @"<ComboBox\b.*?(?:/>|</ComboBox>)", RegexOptions.Singleline))
            {
                if (Regex.IsMatch(combo.Value, @"ItemsSource=""\{Binding (DisplayModes|OutputOrders|Available\w+)")
                    && !combo.Value.Contains("EnumDescriptionConverter", StringComparison.Ordinal))
                {
                    offenders.Add(section);
                }
            }
        }

        Assert.True(offenders.Count == 0, "enum dropdown without an ItemTemplate in: " + string.Join(", ", offenders.Distinct()));
    }

    private static List<string> Offenders(string pattern, string label)
        => Sections
            .Where(section => Regex.IsMatch(SectionXaml(section), pattern))
            .Select(section => $"{section} ({label})")
            .ToList();

    private static string SectionXaml(string section)
        => File.ReadAllText(Path.Combine(AppDir(), "UI", "Pages", "Options", section + ".xaml"));

    private static string AppDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "vrcosc-magicchatbox")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "vrcosc-magicchatbox");
    }
}
