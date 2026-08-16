using System;
using System.IO;
using vrcosc_magicchatbox.Core.Vrc;
using Xunit;

namespace MagicChatbox.Tests.Core.Vrc;

// docs/avatar-parameters.md is what gets linked to avatar creators, and a stale copy is worse than
// none. It is generated from the same list the app emits from, and this test fails the build if the
// two drift apart. Set MCB_UPDATE_CONTRACT=1 to rewrite the file after changing the contract.
public class AvatarParameterDocumentTests
{
    [Fact]
    public void The_published_parameter_document_matches_the_contract()
    {
        string path = Path.Combine(FindRepoRoot(), "docs", "avatar-parameters.md");
        string expected = AvatarParameterContract.ToMarkdown();

        bool regenerate = Environment.GetEnvironmentVariable("MCB_UPDATE_CONTRACT") == "1";

        if (regenerate || !File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, expected);
        }

        string actual = File.ReadAllText(path);

        Assert.True(
            string.Equals(Normalize(actual), Normalize(expected), StringComparison.Ordinal),
            "docs/avatar-parameters.md is out of date. Re-run the tests with MCB_UPDATE_CONTRACT=1 to regenerate it.");
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n").TrimEnd();

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "vrcosc-magicchatbox", "Core")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new DirectoryNotFoundException("repo root not found from " + AppContext.BaseDirectory);
    }
}
