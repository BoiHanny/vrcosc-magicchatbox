using System;
using System.IO;
using vrcosc_magicchatbox.Classes.Modules;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules;

public sealed class PulsoidOAuthStateTests
{
    [Fact]
    public void Callback_state_must_match_the_state_that_started_the_flow()
    {
        const string expected = "a1b2c3d4";

        Assert.True(PulsoidOAuthHandler.HasExpectedState(
            $"access_token=token&state={expected}",
            expected));
        Assert.False(PulsoidOAuthHandler.HasExpectedState(
            "access_token=token&state=some-other-flow",
            expected));
        Assert.False(PulsoidOAuthHandler.HasExpectedState(
            "access_token=token",
            expected));
    }

    [Fact]
    public void Callback_listener_requires_a_bounded_post_from_the_bridge()
    {
        string source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "vrcosc-magicchatbox",
            "Classes",
            "Modules",
            "PulsoidOAuthHandler.cs"));

        Assert.Contains("MaxCallbackBodyChars", source, StringComparison.Ordinal);
        Assert.Contains("request.HttpMethod, \"POST\"", source, StringComparison.Ordinal);
        Assert.Contains("request.Headers[\"Origin\"]", source, StringComparison.Ordinal);
        Assert.Contains("HasExpectedState(fragment, expectedState)", source, StringComparison.Ordinal);
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
