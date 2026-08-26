using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using vrcosc_magicchatbox.Services.Vr;
using Xunit;

namespace MagicChatbox.Tests.Services.Vr;

public class SteamVrManifestTests : IDisposable
{
    private const string Executable = @"C:\Portable\MagicChatbox\MagicChatbox.exe";
    private const string LaunchArgument = "--startedbysteamvr";

    private readonly string _root;

    public SteamVrManifestTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "mcb-steamvr-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, true);
        }
        catch (IOException)
        {
        }
    }

    private static JObject Application(string manifest)
    {
        var root = JObject.Parse(manifest);
        var applications = root["applications"] as JArray;

        Assert.NotNull(applications);
        return Assert.IsType<JObject>(Assert.Single(applications!));
    }

    [Fact]
    public void Build_produces_json_that_parses()
    {
        var root = JObject.Parse(SteamVrManifest.Build(Executable, LaunchArgument));

        Assert.Equal("builtin", (string?)root["source"]);
        Assert.Single((JArray)root["applications"]!);
    }

    [Fact]
    public void The_entry_describes_the_executable_steamvr_should_launch()
    {
        JObject application = Application(SteamVrManifest.Build(Executable, LaunchArgument));

        Assert.Equal("boihanny.magicchatbox", (string?)application["app_key"]);
        Assert.Equal(SteamVrManifest.AppKey, (string?)application["app_key"]);
        Assert.Equal("binary", (string?)application["launch_type"]);
        Assert.Equal(Executable, (string?)application["binary_path_windows"]);
        Assert.Equal(LaunchArgument, (string?)application["arguments"]);
    }

    [Fact]
    public void The_entry_carries_a_display_name()
    {
        JObject application = Application(SteamVrManifest.Build(Executable, LaunchArgument));

        Assert.Equal("MagicChatbox", (string?)application["strings"]?["en_us"]?["name"]);
        Assert.False(string.IsNullOrWhiteSpace((string?)application["strings"]?["en_us"]?["description"]));
    }

    [Fact]
    public void The_entry_is_a_dashboard_overlay()
    {
        // SteamVR only offers auto-launch to overlay applications. Without the flag the manifest
        // still registers and the startup entry silently never runs, so this is load-bearing.
        JObject application = Application(SteamVrManifest.Build(Executable, LaunchArgument));

        Assert.Equal(JTokenType.Boolean, application["is_dashboard_overlay"]!.Type);
        Assert.True((bool?)application["is_dashboard_overlay"]);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void A_missing_path_or_argument_becomes_an_empty_string_not_a_json_null(string? executable, string? argument)
    {
        JObject application = Application(SteamVrManifest.Build(executable!, argument!));

        Assert.Equal(JTokenType.String, application["binary_path_windows"]!.Type);
        Assert.Equal(JTokenType.String, application["arguments"]!.Type);
        Assert.Equal(string.Empty, (string?)application["binary_path_windows"]);
        Assert.Equal(string.Empty, (string?)application["arguments"]);
    }

    [Fact]
    public void The_manifest_lives_in_its_own_folder_under_local_app_data()
    {
        string localAppData = Path.Combine(_root, "LocalAppData");

        Assert.Equal(
            Path.Combine(localAppData, "Vrcosc-MagicChatbox", "steamvr"),
            SteamVrManifest.DirectoryFor(localAppData));

        Assert.Equal(
            Path.Combine(localAppData, "Vrcosc-MagicChatbox", "steamvr", "magicchatbox.vrmanifest"),
            SteamVrManifest.PathFor(localAppData));

        Assert.Equal(SteamVrManifest.DirectoryFor(localAppData), Path.GetDirectoryName(SteamVrManifest.PathFor(localAppData)));
        Assert.EndsWith("magicchatbox.vrmanifest", SteamVrManifest.PathFor(localAppData), StringComparison.Ordinal);
        Assert.Equal("magicchatbox.vrmanifest", SteamVrManifest.FileName);
    }

    [Fact]
    public void TryWrite_creates_the_folder_and_writes_the_manifest()
    {
        string localAppData = Path.Combine(_root, "LocalAppData");
        string path = SteamVrManifest.PathFor(localAppData);

        Assert.False(Directory.Exists(SteamVrManifest.DirectoryFor(localAppData)));

        Assert.True(SteamVrManifest.TryWrite(path, Executable, LaunchArgument));

        Assert.True(File.Exists(path));
        Assert.Equal(SteamVrManifest.Build(Executable, LaunchArgument), File.ReadAllText(path));
    }

    [Fact]
    public void Writing_the_same_manifest_again_leaves_the_file_alone()
    {
        string path = SteamVrManifest.PathFor(Path.Combine(_root, "LocalAppData"));
        Assert.True(SteamVrManifest.TryWrite(path, Executable, LaunchArgument));

        byte[] before = File.ReadAllBytes(path);
        var stamp = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, stamp);

        Assert.True(SteamVrManifest.TryWrite(path, Executable, LaunchArgument));

        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public void Writing_a_different_manifest_replaces_the_old_one()
    {
        // A portable copy that moved, or an update that replaced the installation, has to leave
        // SteamVR pointing at the executable that exists now.
        string path = SteamVrManifest.PathFor(Path.Combine(_root, "LocalAppData"));
        Assert.True(SteamVrManifest.TryWrite(path, Executable, LaunchArgument));

        const string moved = @"D:\Elsewhere\MagicChatbox\MagicChatbox.exe";
        Assert.True(SteamVrManifest.TryWrite(path, moved, "--other"));

        string contents = File.ReadAllText(path);
        Assert.Equal(SteamVrManifest.Build(moved, "--other"), contents);

        JObject application = Application(contents);
        Assert.Equal(moved, (string?)application["binary_path_windows"]);
        Assert.Equal("--other", (string?)application["arguments"]);
        Assert.DoesNotContain(Executable, contents, StringComparison.Ordinal);
    }

    [Fact]
    public void Writing_never_leaves_a_temp_file_behind()
    {
        string localAppData = Path.Combine(_root, "LocalAppData");
        string path = SteamVrManifest.PathFor(localAppData);

        Assert.True(SteamVrManifest.TryWrite(path, Executable, LaunchArgument));
        Assert.True(SteamVrManifest.TryWrite(path, Executable, LaunchArgument));
        Assert.True(SteamVrManifest.TryWrite(path, @"D:\Moved\MagicChatbox.exe", LaunchArgument));

        Assert.False(File.Exists(path + ".tmp"));
        Assert.Empty(Directory.GetFiles(SteamVrManifest.DirectoryFor(localAppData), "*.tmp"));
        Assert.Equal(new[] { SteamVrManifest.FileName }, Directory
            .GetFiles(SteamVrManifest.DirectoryFor(localAppData))
            .Select(Path.GetFileName));
    }

    [Fact]
    public void Delete_removes_the_manifest()
    {
        string path = SteamVrManifest.PathFor(Path.Combine(_root, "LocalAppData"));
        Assert.True(SteamVrManifest.TryWrite(path, Executable, LaunchArgument));

        SteamVrManifest.Delete(path);

        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Delete_is_quiet_when_there_is_nothing_to_remove()
    {
        string path = SteamVrManifest.PathFor(Path.Combine(_root, "NeverWritten"));

        SteamVrManifest.Delete(path);
        SteamVrManifest.Delete(path);
        SteamVrManifest.Delete(Path.Combine(_root, "no-such-folder", "no-such-file.vrmanifest"));
        SteamVrManifest.Delete(null!);
        SteamVrManifest.Delete(string.Empty);
        SteamVrManifest.Delete("   ");

        Assert.False(Directory.Exists(Path.Combine(_root, "NeverWritten")));
    }
}
