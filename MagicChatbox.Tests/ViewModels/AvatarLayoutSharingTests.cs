using MagicChatbox.Tests.TestDoubles;
using System;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.Vrc;
using vrcosc_magicchatbox.Core.Vrc.Sharing;
using vrcosc_magicchatbox.ViewModels;
using Xunit;

namespace MagicChatbox.Tests.ViewModels;

// The app shipped a consent card titled "Shared Layouts" on the Privacy page, with Manage and Revoke
// both working, in front of a feature that did not exist - PrivacyHook.SharedLayoutImport was the one
// hook in the app with a UI row and zero IsApproved calls anywhere. Asking somebody to make a decision
// and then never consulting it is worse than not asking.
//
// These tests exist so that stays fixed: the import path is the caller that makes the switch mean
// something, and the first test is the one that fails if the guard is ever removed.
public class AvatarLayoutSharingTests
{
    private static AvatarPageViewModel Build(IPrivacyConsentService consent) => new(
        new StubSettingsProvider<VrcBridgeSettings>(),
        new StubSettingsProvider<IntegrationSettings>(),
        new StubSettingsProvider<AvatarPresetSettings>(),
        new Lazy<IModuleHost>(() => new BridgelessModuleHost()),
        new RecordingParameterSink(),
        consent);

    private static string SampleCode()
    {
        var document = new LayoutDocument { Title = "Ears and tail" };
        document.Requires.Add(new LayoutRequirement { Name = "Toggles/Ears", Type = "Bool", Purpose = "ear toggle" });

        return LayoutCodec.ToCode(document);
    }

    [Fact]
    public void A_layout_is_not_read_at_all_until_consent_is_given()
    {
        var consent = new StubConsentService();
        consent.Deny(PrivacyHook.SharedLayoutImport);

        AvatarPageViewModel vm = Build(consent);
        vm.LayoutCode = SampleCode();

        vm.CheckLayoutCodeCommand.Execute(null);

        Assert.Empty(vm.LayoutMatches);
        Assert.Contains("Privacy", vm.LayoutShareStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_undecided_hook_is_treated_as_no()
    {
        // Unknown is the state every hook starts in. Reading somebody else's file on a maybe is the
        // wrong default.
        AvatarPageViewModel vm = Build(new StubConsentService());
        vm.LayoutCode = SampleCode();

        vm.CheckLayoutCodeCommand.Execute(null);

        Assert.Empty(vm.LayoutMatches);
    }

    [Fact]
    public void With_consent_a_damaged_code_is_reported_rather_than_ignored()
    {
        AvatarPageViewModel vm = Build(StubConsentService.ApprovingAll());
        vm.LayoutCode = "MCBL1-not-a-real-code";

        vm.CheckLayoutCodeCommand.Execute(null);

        Assert.False(string.IsNullOrWhiteSpace(vm.LayoutShareStatus));
        Assert.Empty(vm.LayoutMatches);
    }

    [Fact]
    public void A_layout_this_app_makes_can_be_read_back_by_it()
    {
        AvatarPageViewModel vm = Build(StubConsentService.ApprovingAll());

        vm.CopyLayoutCodeCommand.Execute(null);

        Assert.StartsWith(LayoutCodec.CodePrefix, vm.LayoutCode, StringComparison.Ordinal);

        LayoutParseResult parsed = LayoutCodec.FromCode(vm.LayoutCode);

        Assert.True(parsed.Ok, parsed.Detail);
        Assert.NotEmpty(parsed.Document!.Requires);
    }

    [Fact]
    public void The_layout_it_makes_asks_for_the_controls_and_never_for_an_address()
    {
        // The format excludes addresses structurally rather than by filtering, because one imported
        // string beginning with a slash is the difference between a recipe and a remote control.
        AvatarPageViewModel vm = Build(StubConsentService.ApprovingAll());

        vm.CopyLayoutCodeCommand.Execute(null);

        LayoutDocument document = LayoutCodec.FromCode(vm.LayoutCode).Document!;

        Assert.Contains(document.Requires, r => r.Name == "MCB/Ctrl/Panic");

        Assert.All(document.Requires, r =>
        {
            Assert.False(r.Name.StartsWith('/'), $"'{r.Name}' is an address, not a parameter name");
            Assert.DoesNotContain("/avatar/", r.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/input/", r.Name, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void The_config_switches_it_offers_are_marked_optional()
    {
        // They are extras, and a layout that called them required would tell every creator their
        // avatar was incomplete.
        AvatarPageViewModel vm = Build(StubConsentService.ApprovingAll());

        vm.CopyLayoutCodeCommand.Execute(null);

        LayoutDocument document = LayoutCodec.FromCode(vm.LayoutCode).Document!;

        Assert.All(
            document.Requires.Where(r => r.Name.StartsWith(AvatarConfigBinding.Prefix, StringComparison.Ordinal)),
            r => Assert.True(r.Optional));

        Assert.All(
            document.Requires.Where(r => r.Name.StartsWith("MCB/Ctrl/", StringComparison.Ordinal)),
            r => Assert.False(r.Optional));
    }
}
