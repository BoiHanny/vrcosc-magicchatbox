using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace MagicChatbox.Tests.Services;

public sealed class HardwareMonitorSingleFlightSourceTests
{
    [Fact]
    public void Nvidia_sampling_is_serialized_around_the_cache_recheck_and_process_query()
    {
        string source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "vrcosc-magicchatbox",
            "Services",
            "HardwareMonitorService.cs"));
        Match method = Regex.Match(
            source,
            @"private IReadOnlyList<NvidiaSmiSample> GetNvidiaSmiSamples\(\)\s*\{(?<body>.*?)\n    \}",
            RegexOptions.Singleline);

        Assert.True(method.Success, "GetNvidiaSmiSamples was not found");
        string body = method.Groups["body"].Value;
        int gate = body.IndexOf("lock (_nvidiaSmiQueryLock)", StringComparison.Ordinal);
        int cacheCheck = body.IndexOf("_nvidiaSmiCache != null", StringComparison.Ordinal);
        int query = body.IndexOf("QueryNvidiaSmiAsync().GetAwaiter().GetResult()", StringComparison.Ordinal);

        Assert.True(gate >= 0);
        Assert.True(cacheCheck > gate);
        Assert.True(query > cacheCheck);
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
