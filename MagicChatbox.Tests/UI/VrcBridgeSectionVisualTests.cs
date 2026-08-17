using MagicChatbox.Tests.TestDoubles;
using System;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.UI.Pages.Options;
using vrcosc_magicchatbox.ViewModels.Sections;
using Xunit;

namespace MagicChatbox.Tests.UI;

/// <summary>
/// The options section for the VRChat bridge, built rather than trusted.
/// </summary>
/// <remarks>
/// The Avatar page it belongs with threw XamlParseException the first time a person opened it, over a
/// resource key that was simply misspelled. This section was written in the same sitting and had never
/// been opened either.
/// </remarks>
public class VrcBridgeSectionVisualTests
{
    [Fact]
    public void The_section_builds()
    {
        Exception? failure = WpfHost.RunInWindow(
            () => new VrcBridgeSection
            {
                DataContext = new VrcBridgeSectionViewModel(
                    new StubSettingsProvider<VrcBridgeSettings>(),
                    new StubSettingsProvider<AppSettings>(),
                    new Lazy<IModuleHost>(() => new BridgelessModuleHost())),
            },
            section => Assert.NotNull(section.DataContext));

        Assert.True(failure == null, "the vrc bridge section did not build: " + failure);
    }

    [Fact]
    public void The_section_builds_with_the_bridge_switched_on()
    {
        // Half the section is collapsed until the bridge is enabled, so the default state leaves most of
        // its markup unbuilt and therefore unchecked.
        var settings = new VrcBridgeSettings { EnableBridge = true, EnableParameterInput = true };
        settings.MutedWorlds.Add("a world");
        settings.BlockedTerms.Add("a term");

        Exception? failure = WpfHost.RunInWindow(
            () => new VrcBridgeSection
            {
                DataContext = new VrcBridgeSectionViewModel(
                    new StubSettingsProvider<VrcBridgeSettings>(settings),
                    new StubSettingsProvider<AppSettings>(),
                    new Lazy<IModuleHost>(() => new BridgelessModuleHost())),
            },
            section => Assert.NotNull(section.DataContext));

        Assert.True(failure == null, "the vrc bridge section did not build switched on: " + failure);
    }
}
