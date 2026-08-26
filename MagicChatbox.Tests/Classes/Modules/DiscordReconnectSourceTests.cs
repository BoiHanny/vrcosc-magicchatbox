using System;
using System.IO;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules;

public sealed class DiscordReconnectSourceTests
{
    [Fact]
    public void Reconnect_keeps_trying_at_the_capped_delay_until_cancelled()
    {
        string source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "vrcosc-magicchatbox",
            "Classes",
            "Modules",
            "DiscordModule.cs"));

        Assert.Contains("while (!ct.IsCancellationRequested && !_disposed)", source, StringComparison.Ordinal);
        Assert.Contains("DiscordReconnectMaxDelay", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxAutoReconnectAttempts", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Reconnect attempts exhausted", source, StringComparison.Ordinal);
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
