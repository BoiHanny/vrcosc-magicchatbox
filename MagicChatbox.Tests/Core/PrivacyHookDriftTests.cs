using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using vrcosc_magicchatbox.Core.Privacy;
using Xunit;

namespace MagicChatbox.Tests.Core;

// A privacy hook is spread across an enum, a settings triple, two switch arms, a view model and a
// XAML block. The two switches throw when a hook is missing, so they announce themselves. The view
// model and the XAML do not: the consent simply never appears on the Privacy page, which means a
// capability the user can neither see nor revoke.
public class PrivacyHookDriftTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "vrcosc-magicchatbox", "Core")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new DirectoryNotFoundException("repo root not found");
    }

    private static string AppFile(params string[] parts)
        => Path.Combine(new[] { RepoRoot(), "vrcosc-magicchatbox" }.Concat(parts).ToArray());

    private static IEnumerable<PrivacyHook> AllHooks()
        => Enum.GetValues<PrivacyHook>();

    [Fact]
    public void Every_hook_has_a_name_and_an_icon()
    {
        var unnamed = AllHooks()
            .Where(h => PrivacyHookInfo.Get(h).Name == h.ToString())
            .Select(h => h.ToString())
            .ToList();

        Assert.True(unnamed.Count == 0, "privacy hooks falling through to their enum name: " + string.Join(", ", unnamed));
    }

    [Fact]
    public void Every_hook_has_its_own_stored_consent()
    {
        string settings = File.ReadAllText(AppFile("Core", "Privacy", "PrivacySettings.cs"));

        var missing = AllHooks()
            .Where(h => !settings.Contains(h + "Consent", StringComparison.OrdinalIgnoreCase))
            .Select(h => h.ToString())
            .ToList();

        Assert.True(missing.Count == 0, "hooks with no persisted consent field: " + string.Join(", ", missing));
    }

    [Fact]
    public void Every_hook_records_when_it_was_decided()
    {
        string settings = File.ReadAllText(AppFile("Core", "Privacy", "PrivacySettings.cs"));

        var missing = AllHooks()
            .Where(h => !settings.Contains(h + "DecidedAt", StringComparison.OrdinalIgnoreCase))
            .Select(h => h.ToString())
            .ToList();

        Assert.True(missing.Count == 0, "hooks that do not record a decision time: " + string.Join(", ", missing));
    }

    [Fact]
    public void Every_hook_can_be_read_and_written_by_the_consent_service()
    {
        string service = File.ReadAllText(AppFile("Core", "Privacy", "PrivacyConsentService.cs"));

        var missing = AllHooks()
            .Where(h => !service.Contains("PrivacyHook." + h, StringComparison.Ordinal))
            .Select(h => h.ToString())
            .ToList();

        Assert.True(missing.Count == 0, "hooks the consent service cannot handle: " + string.Join(", ", missing));
    }

    [Fact]
    public void Every_hook_is_visible_and_revocable_on_the_privacy_page()
    {
        // The silent one. Without an entry here the user is granting something they cannot see and
        // cannot take back.
        string viewModel = File.ReadAllText(AppFile("ViewModels", "Sections", "PrivacySectionViewModel.cs"));
        string xaml = File.ReadAllText(AppFile("UI", "Pages", "Options", "PrivacySection.xaml"));

        var missingFromViewModel = AllHooks()
            .Where(h => !viewModel.Contains(h.ToString(), StringComparison.Ordinal))
            .Select(h => h.ToString())
            .ToList();

        var missingFromXaml = AllHooks()
            .Where(h => !xaml.Contains(h.ToString(), StringComparison.Ordinal))
            .Select(h => h.ToString())
            .ToList();

        Assert.True(
            missingFromViewModel.Count == 0,
            "hooks absent from the privacy view model: " + string.Join(", ", missingFromViewModel));

        Assert.True(
            missingFromXaml.Count == 0,
            "hooks absent from the privacy page: " + string.Join(", ", missingFromXaml));
    }
}
