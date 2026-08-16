using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace MagicChatbox.Tests.Core.Vrc;

// The fence. Every value bound for an avatar goes through IAvatarParameterSink, which means exactly
// one place decides how a name becomes an address, whether the bridge also gets a copy, and how a
// pulse behaves. Before this, twenty-two literal addresses were spread across three modules and the
// two hand-rolled pulses had different semantics. These tests stop that growing back.
public class AvatarParameterFenceTests
{
    // The only places allowed to name an OSC address directly.
    private static readonly string[] AllowedFiles =
    [
        Path.Combine("Core", "Vrc", "AvatarParameter.cs"),
        Path.Combine("Core", "Vrc", "AvatarParameterContract.cs"),
        Path.Combine("Core", "Vrc", "IAvatarParameterSink.cs"),
        Path.Combine("Core", "Vrc", "AvatarParameterRouter.cs"),
        // The user can edit this one, and it has always been stored as a whole address.
        Path.Combine("Classes", "Modules", "VrcLogSettings.cs"),
    ];

    [Fact]
    public void No_module_writes_an_avatar_parameter_address_directly()
    {
        var offenders = new List<string>();

        foreach (string file in AppSources())
        {
            if (IsAllowed(file))
                continue;

            string text = File.ReadAllText(file);

            foreach (Match match in Regex.Matches(text, @"""/avatar/parameters/[^""]*"""))
                offenders.Add($"{Relative(file)}: {match.Value}");
        }

        Assert.True(
            offenders.Count == 0,
            "avatar parameter addresses written outside Core/Vrc - route these through IAvatarParameterSink: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void No_module_calls_SendOscParam_for_an_avatar_parameter()
    {
        // SendOscParam still exists for the chatbox and voice paths. What must not come back is a
        // module using it to drive an avatar, because that bypasses the bridge entirely.
        var offenders = new List<string>();

        foreach (string file in AppSources())
        {
            if (IsAllowed(file))
                continue;

            string text = File.ReadAllText(file);

            foreach (Match match in Regex.Matches(text, @"SendOscParam\(\s*\$?""/avatar/parameters/[^""]*"""))
                offenders.Add($"{Relative(file)}: {match.Value}");
        }

        Assert.True(offenders.Count == 0, "direct SendOscParam calls for avatar parameters: " + string.Join(", ", offenders));
    }

    [Fact]
    public void There_is_exactly_one_pulse_implementation()
    {
        // Two hand-rolled pulses used to exist with different durations, different cancellation
        // behaviour, and one of them swallowing every exception.
        string router = File.ReadAllText(AppFile("Core", "Vrc", "AvatarParameterRouter.cs"));

        Assert.Contains("_pulseSequence", router, StringComparison.Ordinal);

        var others = AppSources()
            .Where(f => !IsAllowed(f))
            .Where(f => File.ReadAllText(f).Contains("_pulseSequence", StringComparison.Ordinal))
            .Select(Relative)
            .ToList();

        Assert.True(others.Count == 0, "pulse sequencing outside the router: " + string.Join(", ", others));
    }

    private static bool IsAllowed(string file)
        => AllowedFiles.Any(a => file.EndsWith(a, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> AppSources()
        => Directory
            .EnumerateFiles(AppRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string Relative(string file) => file[(AppRoot().Length + 1)..];

    private static string AppRoot() => Path.Combine(FindRepoRoot(), "vrcosc-magicchatbox");

    private static string AppFile(params string[] parts)
        => Path.Combine(new[] { AppRoot() }.Concat(parts).ToArray());

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "vrcosc-magicchatbox", "Core")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new DirectoryNotFoundException("repo root not found from " + AppContext.BaseDirectory);
    }
}
