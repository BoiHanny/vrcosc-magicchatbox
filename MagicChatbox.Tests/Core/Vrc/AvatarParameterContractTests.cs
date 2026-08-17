using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using vrcosc_magicchatbox.Core.Vrc;
using Xunit;

namespace MagicChatbox.Tests.Core.Vrc;

// The contract is what avatar creators build against, so it has to describe what the app actually
// sends - not what it sent two releases ago. These tests read the real source for
// /avatar/parameters/ literals and hold the contract to them in both directions: nothing sent that
// is undocumented, and nothing documented that is no longer sent.
public class AvatarParameterContractTests
{
    // Addresses the app builds at runtime rather than writing as one literal. Each is covered by the
    // contract under the name listed here, and each needs a reason to be in this list.
    private static readonly Dictionary<string, string> ComposedAddresses = new(StringComparer.Ordinal)
    {
        ["MCB_Heartrate_Min"] = "SendHeartRateDigits appends _Ones/_Tens/_Hundreds",
        ["MCB_Heartrate_Max"] = "SendHeartRateDigits appends _Ones/_Tens/_Hundreds",
        ["MCB_Heartrate_Avg"] = "SendHeartRateDigits appends _Ones/_Tens/_Hundreds",
        ["CameraFlash"] = "the name is user-editable through VrcLogSettings.OscCameraFlashParam",
    };

    [Fact]
    public void Inbound_commands_in_the_contract_are_all_registered_to_do_something()
    {
        // A control parameter a creator can wire up but nothing listens for is a support ticket
        // waiting to happen, so the contract and the command registry have to agree.
        var registered = new HashSet<string>(
            Regex.Matches(
                    File.ReadAllText(Path.Combine(FindRepoRoot(), "vrcosc-magicchatbox", "Core", "Vrc", "InboundCommandRegistry.cs")),
                    @"new InboundCommand\(\s*""([^""]+)""")
                .Select(m => m.Groups[1].Value),
            StringComparer.Ordinal);

        // The Control tier is the command surface - a press that makes something happen. The Config
        // tier also flows from the avatar but is not a command: it is a switch the seeder reads, so it
        // is checked against the binding registry instead, by AvatarConfigBindingRegistryTests.
        var orphans = AvatarParameterContract.Parameters
            .Where(p => p.Tier == AvatarParameterTier.Control)
            .Where(p => !registered.Contains(p.Name))
            .Select(p => p.Name)
            .ToList();

        Assert.True(orphans.Count == 0, "control parameters with no registered command: " + string.Join(", ", orphans));

        var undocumented = registered
            .Where(n => !AvatarParameterContract.Parameters.Any(p => p.Name == n))
            .ToList();

        Assert.True(undocumented.Count == 0, "commands missing from the contract: " + string.Join(", ", undocumented));
    }

    [Fact]
    public void Every_address_the_app_sends_is_described_by_the_contract()
    {
        var undocumented = SentAddresses()
            .Where(a => !AvatarParameterContract.IsKnownAddress(a))
            .OrderBy(a => a, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            undocumented.Count == 0,
            "avatar parameters the app sends but the contract does not document: "
            + string.Join(", ", undocumented));
    }

    [Fact]
    public void Every_parameter_in_the_contract_is_actually_sent()
    {
        // A contract entry nobody emits is worse than a missing one: a creator wires their avatar to
        // it and it silently never moves.
        var sent = SentAddresses();

        var phantom = AvatarParameterContract.Parameters
            .Where(p => p.Flow != AvatarParameterFlow.AvatarToApp)
            .Where(p => !sent.Contains(p.Address))
            .Where(p => !IsComposed(p.Name))
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            phantom.Count == 0,
            "contract parameters that no code sends: " + string.Join(", ", phantom));
    }

    [Fact]
    public void Parameter_names_are_legal_for_VRChat()
    {
        // VRChat rewrites spaces in an address and misbehaves on OSC pattern characters, so an
        // illegal name fails quietly on somebody else's avatar rather than here.
        var illegal = AvatarParameterContract.Parameters
            .Where(p => Regex.IsMatch(p.Name, @"[ #*,?\[\]{}]") || p.Name.Any(char.IsControl))
            .Select(p => p.Name)
            .ToList();

        Assert.True(illegal.Count == 0, "parameter names VRChat cannot address safely: " + string.Join(", ", illegal));
    }

    [Fact]
    public void No_two_parameters_share_a_name()
    {
        var duplicates = AvatarParameterContract.Parameters
            .GroupBy(p => p.Name, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.True(duplicates.Count == 0, "duplicate parameter names: " + string.Join(", ", duplicates));
    }

    [Fact]
    public void The_two_case_variants_of_HeartRate3_are_both_present_and_distinct()
    {
        // Both spellings ship in the wild and VRChat treats them as different parameters. If one of
        // these ever disappears it is a compatibility regression, not a tidy-up.
        Assert.Contains(AvatarParameterContract.Parameters, p => p.Name == "HeartRate3");
        Assert.Contains(AvatarParameterContract.Parameters, p => p.Name == "Heartrate3");
    }

    [Fact]
    public void Every_parameter_documents_a_source_and_a_gate()
    {
        var vague = AvatarParameterContract.Parameters
            .Where(p => string.IsNullOrWhiteSpace(p.Source) || string.IsNullOrWhiteSpace(p.Gate))
            .Select(p => p.Name)
            .ToList();

        Assert.True(vague.Count == 0, "parameters with no source or no gate documented: " + string.Join(", ", vague));
    }

    [Fact]
    public void The_rendered_contract_covers_every_parameter()
    {
        string markdown = AvatarParameterContract.ToMarkdown();
        string clipboard = AvatarParameterContract.ToClipboardText();

        foreach (var parameter in AvatarParameterContract.Parameters)
        {
            Assert.Contains(parameter.Name, markdown, StringComparison.Ordinal);
            Assert.Contains(parameter.Name, clipboard, StringComparison.Ordinal);
        }
    }

    private static bool IsComposed(string name)
        => ComposedAddresses.Keys.Any(root => name.Equals(root, StringComparison.Ordinal)
                                              || name.StartsWith(root + "_", StringComparison.Ordinal));

    private static HashSet<string> SentAddresses()
    {
        var app = Path.Combine(FindRepoRoot(), "vrcosc-magicchatbox");
        Assert.True(Directory.Exists(app), $"app folder not found: {app}");

        var found = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(app, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}Core{Path.DirectorySeparatorChar}Vrc{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            string text = File.ReadAllText(file);

            // Any address still written as a literal.
            foreach (Match match in Regex.Matches(text, @"""(/avatar/parameters/[^""]*)"""))
                found.Add(match.Groups[1].Value);

            // The normal form: a bare name handed to the parameter sink.
            foreach (Match match in Regex.Matches(text, @"Params\.(?:Set|Pulse)\(\$?""([A-Za-z0-9_/]+)"""))
                found.Add(AvatarParameter.AddressPrefix + match.Groups[1].Value);
        }

        // The digit helper and the user-editable flash name are assembled at runtime, so the literal
        // scan cannot see the final addresses. Add what those code paths really produce.
        foreach (var root in ComposedAddresses.Keys)
        {
            if (root == "CameraFlash")
            {
                found.Add(AvatarParameter.AddressPrefix + root);
                continue;
            }

            foreach (var suffix in new[] { "_Ones", "_Tens", "_Hundreds" })
                found.Add(AvatarParameter.AddressPrefix + root + suffix);
        }

        return found;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "vrcosc-magicchatbox", "Core")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new DirectoryNotFoundException("repo root not found from " + AppContext.BaseDirectory);
    }
}
