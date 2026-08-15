using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using Xunit;

namespace MagicChatbox.Tests.UI;

/// <summary>
/// Guards the resources a shared control and the Options sections are allowed to ask for.
/// </summary>
/// <remarks>
/// A UserControl's own XAML is parsed before the control is attached to anything, so a
/// StaticResource inside it can only reach the application dictionary - a page merging the same
/// dictionary further down the tree is already too late. SegmentPreview asked for
/// BoolToVisibilityConverter, which lived only in page-level dictionaries, and threw
/// XamlParseException the moment anything tried to show it. It compiled cleanly the whole time.
/// </remarks>
public class SharedControlResourceTests
{
    [Fact]
    public void SegmentPreview_can_actually_be_constructed()
    {
        Exception? failure = WpfHost.Run(() =>
        {
            var preview = new vrcosc_magicchatbox.UI.Controls.SegmentPreview { Caption = "x", Line = "hello" };
            Assert.Equal("5/144", preview.CostText);
        });

        Assert.True(failure == null, "SegmentPreview did not construct: " + failure);
    }

    [Fact]
    public void App_xaml_provides_the_converters_the_shared_controls_reach_for()
    {
        // Constructing SegmentPreview above proves the app-level dictionary works today; this says
        // where it has to stay. Moving the converters back down to the pages would break every
        // control in UI/Controls without breaking the build.
        Assert.Contains(@"Source=""UI/Resources/SharedConverters.xaml""", File.ReadAllText(AppFile("App.xaml")));
    }

    [Theory]
    [InlineData("UI/Controls/SegmentPreview.xaml")]
    [InlineData("UI/Pages/ChattingPage.xaml")]
    [InlineData("UI/Pages/Options/ChattingOptionsSection.xaml")]
    [InlineData("UI/Pages/Options/StatusSection.xaml")]
    [InlineData("UI/Pages/Options/MediaLinkSection.xaml")]
    [InlineData("UI/Pages/Options/SpotifySection.xaml")]
    [InlineData("UI/Pages/Options/LyricsSection.xaml")]
    public void Every_StaticResource_it_names_is_one_the_application_defines(string relativePath)
    {
        string xaml = File.ReadAllText(AppFile(relativePath.Replace('/', Path.DirectorySeparatorChar)));

        var available = new HashSet<string>(StringComparer.Ordinal);
        available.UnionWith(DeclaredKeys(File.ReadAllText(AppFile("App.xaml"))));
        available.UnionWith(DeclaredKeys(File.ReadAllText(AppFile(Path.Combine("UI", "Theme.xaml")))));
        available.UnionWith(DeclaredKeys(File.ReadAllText(AppFile(Path.Combine("UI", "Resources", "SharedConverters.xaml")))));
        available.UnionWith(DeclaredKeys(xaml));

        var missing = Regex.Matches(xaml, @"\{StaticResource\s+([A-Za-z0-9_]+)\s*\}")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .Where(key => !available.Contains(key))
            .ToList();

        Assert.True(missing.Count == 0, relativePath + " names resources nothing defines: " + string.Join(", ", missing));
    }

    private static IEnumerable<string> DeclaredKeys(string xaml)
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
