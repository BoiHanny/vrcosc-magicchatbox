using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace MagicChatbox.Tests.UI;

public class ThemeVisualsTests
{
    [Fact]
    public void The_packed_fonts_are_shipped_as_resources()
    {
        // The fonts lived in the repo for years without ever reaching the build output, so every
        // user saw Segoe UI fallback. This pins both the csproj entry and the file itself.
        string appDir = Path.Combine(FindRepoRoot(), "vrcosc-magicchatbox");
        string csproj = File.ReadAllText(Path.Combine(appDir, "MagicChatbox.csproj"));

        Assert.Contains(@"<Resource Include=""Fonts\Albert.ttf"" />", csproj);
        Assert.Contains(@"<Resource Include=""Fonts\Comfortaa.ttf"" />", csproj);
        Assert.True(File.Exists(Path.Combine(appDir, "Fonts", "Albert.ttf")));
        Assert.True(File.Exists(Path.Combine(appDir, "Fonts", "Comfortaa.ttf")));
    }

    [Fact]
    public void No_xaml_references_a_font_by_bare_family_name()
    {
        // A bare name like "Albert Sans Thin" only resolves when the font is installed on the
        // user's machine; the packed form /Fonts/#Name resolves from the exe everywhere.
        var offenders = new List<string>();

        foreach (string file in AppXamlFiles())
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"(?:FontFamily|Value)=""((?:Albert|Comfortaa)[^""]*)"""))
                offenders.Add($"{Path.GetFileName(file)} -> {m.Groups[1].Value}");
        }

        Assert.True(offenders.Count == 0, "bare font names found: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Theme_defines_packed_font_tokens_matching_the_ttf_internal_names()
    {
        // The internal family names were probed from the TTFs (GlyphTypeface): Albert.ttf is
        // "Albert Sans", Comfortaa.ttf is "Comfortaa". A rename of either breaks every reference.
        string theme = ThemeXaml();

        Assert.Contains(@"<FontFamily x:Key=""FontPrimary"">/Fonts/#Albert Sans</FontFamily>", theme);
        Assert.Contains(@"<FontFamily x:Key=""FontSecondary"">/Fonts/#Comfortaa</FontFamily>", theme);
    }

    [Theory]
    [InlineData("TextLabelBrush", "SurfaceCardBrush")]
    [InlineData("TextMutedBrush", "SurfaceCardBrush")]
    [InlineData("TextSecondaryBrush", "SurfaceCardBrush")]
    [InlineData("TextSubtleBrush", "SurfaceCardBrush")]
    [InlineData("TextDimLabelBrush", "SurfaceCardBrush")]
    [InlineData("TextDarkMutedBrush", "SurfaceCardBrush")]
    [InlineData("TextSubtlePurpleBrush", "SurfaceCardBrush")]
    [InlineData("TextLabelLightBrush", "SurfaceCardBrush")]
    [InlineData("TextMutedGrayBrush", "SurfaceCardBrush")]
    [InlineData("TextSoftPurpleBrush", "SurfaceCardBrush")]
    [InlineData("TextPrimaryBrush", "SurfaceCardBrush")]
    [InlineData("TextBodyBrush", "SurfaceCardBrush")]
    [InlineData("ButtonTextBrush", "SurfaceDarkBrush")]
    [InlineData("DeckTextBrush", "DeckSurfaceBrush")]
    [InlineData("DeckTextMutedBrush", "DeckSurfaceBrush")]
    public void Text_token_meets_wcag_aa_on_its_surface(string textKey, string surfaceKey)
    {
        double ratio = ContrastRatio(BrushColor(textKey), BrushColor(surfaceKey));
        Assert.True(ratio >= 4.5, $"{textKey} on {surfaceKey}: {ratio:F2}:1, needs 4.5:1");
    }

    [Fact]
    public void Tile_description_meets_wcag_aa_on_the_lightest_gradient_stop()
    {
        // TileSurfaceBrush is a diagonal gradient; the top-left stop is the lightest ground the
        // description ever sits on, so that is the stop that has to carry the ratio.
        string theme = ThemeXaml();
        var brush = Regex.Match(theme, @"x:Key=""TileSurfaceBrush"".*?</LinearGradientBrush>", RegexOptions.Singleline);
        Assert.True(brush.Success, "TileSurfaceBrush not found in Theme.xaml");

        var stops = Regex.Matches(brush.Value, @"Color=""#(?:FF)?([0-9A-Fa-f]{6})""")
            .Select(m => m.Groups[1].Value)
            .ToList();
        Assert.NotEmpty(stops);

        string lightest = stops.OrderByDescending(Luminance).First();
        double ratio = ContrastRatio(BrushColor("TileDescriptionBrush"), lightest);
        Assert.True(ratio >= 4.5, $"TileDescriptionBrush on #{lightest}: {ratio:F2}:1, needs 4.5:1");
    }

    [Fact]
    public void TextSubtleColor_stays_in_sync_with_TextSubtleBrush()
    {
        string theme = ThemeXaml();
        var color = Regex.Match(theme, @"<Color x:Key=""TextSubtleColor"">#(?:FF)?([0-9A-Fa-f]{6})</Color>");
        Assert.True(color.Success, "TextSubtleColor not found in Theme.xaml");
        Assert.Equal(BrushColor("TextSubtleBrush"), color.Groups[1].Value, ignoreCase: true);
    }

    [Fact]
    public void Help_text_carries_no_opacity_dim()
    {
        // The token is tuned to 4.5:1 already; an Opacity setter on top would stack a second dim
        // and silently drop it below AA again.
        var style = Regex.Match(ThemeXaml(), @"x:Key=""OptionHelpTextStyle"".*?</Style>", RegexOptions.Singleline);
        Assert.True(style.Success, "OptionHelpTextStyle not found in Theme.xaml");
        Assert.DoesNotContain("Opacity", style.Value);
    }

    private static string BrushColor(string key)
    {
        var m = Regex.Match(ThemeXaml(), $@"x:Key=""{key}"" Color=""#(?:FF)?([0-9A-Fa-f]{{6}})""");
        Assert.True(m.Success, $"{key} not found in Theme.xaml");
        return m.Groups[1].Value;
    }

    private static double ContrastRatio(string hexA, string hexB)
    {
        double la = Luminance(hexA), lb = Luminance(hexB);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Luminance(string hex)
    {
        double Channel(int value)
        {
            double c = value / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(Convert.ToInt32(hex.Substring(0, 2), 16))
             + 0.7152 * Channel(Convert.ToInt32(hex.Substring(2, 2), 16))
             + 0.0722 * Channel(Convert.ToInt32(hex.Substring(4, 2), 16));
    }

    private static string ThemeXaml()
        => File.ReadAllText(Path.Combine(FindRepoRoot(), "vrcosc-magicchatbox", "UI", "Theme.xaml"));

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
