using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using vrcosc_magicchatbox.Core.Osc;
using Xunit;

namespace MagicChatbox.Tests.Core.Osc;

public class OscProviderNamesTests
{
    [Fact]
    public void An_unknown_key_falls_back_to_itself_rather_than_going_blank()
    {
        // A notice reading "No room for " helps nobody, so an unmapped key still says something.
        Assert.Equal("SomethingNew", OscProviderNames.Describe("SomethingNew"));
    }

    [Theory]
    [InlineData("Window", "Window activity")]
    [InlineData("ComponentStat", "Component stats")]
    [InlineData("NetworkStatistics", "Network stats")]
    [InlineData("VrcRadar", "VRChat radar")]
    public void The_keys_that_do_not_match_their_tile_name_are_spelled_out(string key, string expected)
    {
        Assert.Equal(expected, OscProviderNames.Describe(key));
    }

    [Fact]
    public void Keys_are_matched_regardless_of_case()
    {
        Assert.Equal("Discord", OscProviderNames.Describe("discord"));
    }

    [Fact]
    public void One_name_reads_as_one_name()
    {
        Assert.Equal("Discord", OscProviderNames.DescribeList(new[] { "Discord" }));
    }

    [Fact]
    public void Two_names_are_joined_with_and()
    {
        Assert.Equal("Discord and Weather", OscProviderNames.DescribeList(new[] { "Discord", "Weather" }));
    }

    [Fact]
    public void More_than_two_use_commas_and_a_final_and()
    {
        Assert.Equal(
            "Discord, Weather and Time",
            OscProviderNames.DescribeList(new[] { "Discord", "Weather", "Time" }));
    }

    [Fact]
    public void Nothing_produces_nothing()
    {
        Assert.Equal(string.Empty, OscProviderNames.DescribeList(Array.Empty<string>()));
        Assert.Equal(string.Empty, OscProviderNames.DescribeList(null));
    }

    [Fact]
    public void Every_provider_in_the_app_has_a_name_here()
    {
        // The build reports UiKeys, and an unnamed one would surface to the user as a raw key like
        // "ComponentStat". This walks the providers so adding one without a name fails here rather
        // than in front of somebody.
        var providers = Path.Combine(FindRepoRoot(), "vrcosc-magicchatbox", "Core", "Osc", "Providers");
        Assert.True(Directory.Exists(providers), $"provider folder not found: {providers}");

        var missing = new List<string>();

        foreach (var file in Directory.EnumerateFiles(providers, "*OscProvider.cs"))
        {
            var match = Regex.Match(File.ReadAllText(file), @"UiKey\s*=>\s*""([^""]+)""");
            if (!match.Success)
                continue;

            string key = match.Groups[1].Value;
            if (!OscProviderNames.IsKnown(key))
                missing.Add($"{Path.GetFileName(file)} -> {key}");
        }

        Assert.True(missing.Count == 0, "providers with no friendly name: " + string.Join(", ", missing));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "vrcosc-magicchatbox", "Core")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new DirectoryNotFoundException("repo root not found from " + AppContext.BaseDirectory);
    }
}
