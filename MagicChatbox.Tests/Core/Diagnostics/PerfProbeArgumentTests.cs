using System;
using System.IO;
using vrcosc_magicchatbox.Core.Diagnostics;
using Xunit;

namespace MagicChatbox.Tests.Core.Diagnostics;

public sealed class PerfProbeArgumentTests
{
    [Theory]
    [InlineData("--perf")]
    [InlineData("--PERF")]
    [InlineData("-perf")]
    public void Supported_performance_switches_are_recognized(string argument)
        => Assert.True(PerfProbe.IsEnableArgument(argument));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("--performance")]
    public void Unrelated_switches_are_not_recognized(string? argument)
        => Assert.False(PerfProbe.IsEnableArgument(argument));

    [Fact]
    public void Startup_accepts_both_the_documented_and_legacy_switches()
    {
        string startup = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "vrcosc-magicchatbox",
            "App.xaml.cs"));

        Assert.Contains("PerfProbe.IsEnableArgument(arg)", startup, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null &&
               !Directory.Exists(Path.Combine(directory.FullName, "vrcosc-magicchatbox", "Core")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("repo root not found from " + AppContext.BaseDirectory);
    }
}
