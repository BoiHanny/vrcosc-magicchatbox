using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using vrcosc_magicchatbox.Core.Vrc;
using Xunit;

namespace MagicChatbox.Tests.Core.Vrc;

// The Unity package cannot be compiled here - it needs the VRChat SDK - so these tests hold the
// parts that can drift silently. The control list in the editor script is the one thing that must
// track the app, because a creator whose avatar exposes a parameter nothing listens for gets no
// error anywhere: the button just does nothing.
public class UnityPackageContractTests
{
    private static string UnityRoot() => Path.Combine(FindRepoRoot(), "unity");

    private static string PackageRoot() => Path.Combine(UnityRoot(), "com.magicchatbox.avatar");

    private static string EditorScript()
        => Path.Combine(PackageRoot(), "Editor", "MagicChatboxAvatarSetup.cs");

    private static IReadOnlyList<string> ControlsInEditorScript()
    {
        string source = File.ReadAllText(EditorScript());

        var block = Regex.Match(
            source,
            @"Controls\s*=\s*\{(?<body>.*?)\};",
            RegexOptions.Singleline);

        Assert.True(block.Success, "could not find the Controls table in the editor script");

        return Regex.Matches(block.Groups["body"].Value, @"\(\s*""([^""]+)""\s*,")
            .Select(m => m.Groups[1].Value)
            .ToList();
    }

    [Fact]
    public void The_editor_script_offers_exactly_the_controls_the_app_listens_for()
    {
        var appControls = AvatarParameterContract.Parameters
            .Where(p => p.Flow == AvatarParameterFlow.AvatarToApp)
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        var packageControls = ControlsInEditorScript()
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(appControls, packageControls);
    }

    [Fact]
    public void The_generator_stamps_a_version_the_app_can_see()
    {
        // Encoded in the name rather than a value, because VRChat's OSCQuery reports stale values for
        // parameters that have not changed since load. Presence is reported reliably; values are not.
        string source = File.ReadAllText(EditorScript());

        Assert.Contains("MCB/Version/1", source, StringComparison.Ordinal);
        Assert.Contains("VersionParameter", source, StringComparison.Ordinal);
    }

    [Fact]
    public void The_version_the_generator_stamps_is_the_one_the_app_looks_for()
    {
        string source = File.ReadAllText(EditorScript());

        Match stamped = Regex.Match(source, @"VersionParameter\s*=\s*""(MCB/Version/\d+)""");
        Assert.True(stamped.Success, "the generator does not declare a version parameter name");

        Assert.StartsWith(LayoutDoctor.VersionPrefix, stamped.Groups[1].Value, StringComparison.Ordinal);
    }

    [Fact]
    public void The_generator_matches_the_avatar_s_own_Write_Defaults()
    {
        // A mismatch produces an SDK warning the creator cannot attribute to us, and an empty clip on
        // a Write-Defaults-off state produces a second one.
        string source = File.ReadAllText(EditorScript());

        Assert.Contains("DetectWriteDefaults", source, StringComparison.Ordinal);
        Assert.Contains("AnimLayerType.FX", source, StringComparison.Ordinal);
        Assert.DoesNotContain("writeDefaultValues = false", source, StringComparison.Ordinal);
    }

    [Fact]
    public void The_menu_paginates_rather_than_dropping_controls()
    {
        // The old code silently discarded anything past the eighth control with a warning.
        string source = File.ReadAllText(EditorScript());

        Assert.Contains("SubMenu", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Mathf.Min(Controls.Length", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_generated_parameter_is_unsynced()
    {
        // The entire pitch is that these cost nothing against the 256 bit budget. One synced entry
        // quietly makes that false.
        string source = File.ReadAllText(EditorScript());

        Assert.Contains("networkSynced = false", source, StringComparison.Ordinal);
        Assert.DoesNotContain("networkSynced = true", source, StringComparison.Ordinal);
    }

    [Fact]
    public void The_parameter_driver_only_ever_sets()
    {
        // VRChat documents Add and Random as unreliable on remote instances, and a driver that does
        // not reliably return the parameter to false turns one press into a stuck button.
        string source = File.ReadAllText(EditorScript());

        Assert.Contains("ChangeType.Set", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ChangeType.Add", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ChangeType.Random", source, StringComparison.Ordinal);
    }

    [Fact]
    public void The_generated_menu_respects_the_eight_control_cap()
    {
        string source = File.ReadAllText(EditorScript());

        Assert.Contains("8", source, StringComparison.Ordinal);
        Assert.True(
            ControlsInEditorScript().Count <= 8,
            "more controls than a single VRChat menu page holds");
    }

    [Fact]
    public void The_package_manifest_is_valid_and_declares_the_avatars_sdk()
    {
        using JsonDocument manifest = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(PackageRoot(), "package.json")));

        JsonElement root = manifest.RootElement;

        Assert.Equal("com.magicchatbox.avatar", root.GetProperty("name").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("version").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("unity").GetString()));

        JsonElement dependencies = root.GetProperty("vpmDependencies");
        Assert.True(
            dependencies.TryGetProperty("com.vrchat.avatars", out _),
            "the package must depend on the VRChat Avatars SDK and nothing else");

        // VRCFury and Modular Avatar are both supported, and neither may be a hard dependency.
        Assert.False(dependencies.TryGetProperty("com.vrcfury.vrcfury", out _));
        Assert.False(dependencies.TryGetProperty("nadena.dev.modular-avatar", out _));
    }

    [Fact]
    public void The_listing_source_is_valid_json()
    {
        using JsonDocument listing = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(UnityRoot(), "source.json")));

        Assert.False(string.IsNullOrWhiteSpace(listing.RootElement.GetProperty("id").GetString()));
        Assert.NotEqual(0, listing.RootElement.GetProperty("githubRepos").GetArrayLength());
    }

    [Fact]
    public void The_editor_script_is_guarded_so_it_cannot_leak_into_a_player_build()
    {
        string source = File.ReadAllText(EditorScript());

        Assert.StartsWith("#if UNITY_EDITOR", source.TrimStart(), StringComparison.Ordinal);
        Assert.Contains("#endif", source, StringComparison.Ordinal);
    }

    [Fact]
    public void The_editor_assembly_is_editor_only()
    {
        using JsonDocument asmdef = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(PackageRoot(), "Editor", "MagicChatbox.Avatar.Editor.asmdef")));

        JsonElement platforms = asmdef.RootElement.GetProperty("includePlatforms");

        Assert.Equal(1, platforms.GetArrayLength());
        Assert.Equal("Editor", platforms[0].GetString());
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "vrcosc-magicchatbox", "Core")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new DirectoryNotFoundException("repo root not found from " + AppContext.BaseDirectory);
    }
}
