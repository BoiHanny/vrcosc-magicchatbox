using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using vrcosc_magicchatbox.UI.Pages.Options;
using Xunit;

namespace MagicChatbox.Tests.UI;

public class VoicemodSectionTests
{
    [Fact]
    public void The_options_header_loads_with_the_shared_expand_collapse_style()
    {
        Exception? failure = WpfHost.RunInWindow(
            () => new VoicemodSection(),
            section =>
            {
                var toggle = Assert.IsType<ToggleButton>(section.FindName("VoicemodSectionToggle"));
                Assert.Equal("Voicemod options", toggle.Tag);

                // WPF hands every live Control a non-null theme Style, so asserting non-null here
                // passes even with the Style attribute deleted. Compare against the resource itself.
                object expected = section.TryFindResource("ExpandCollapseToggleButtonStyle");
                Assert.NotNull(expected);
                Assert.Same(expected, toggle.Style);
            });

        Assert.Null(failure);
    }

    [Fact]
    public void The_section_builds_its_whole_visual_tree()
    {
        // The section hosts the control panel, which carries the TabControl template and every
        // feature-gated tab. A missing StaticResource in any of that only shows up on load.
        Exception? failure = WpfHost.RunInWindow(
            () => new VoicemodSection(),
            section => Assert.NotNull(section.FindName("VoicemodSectionToggle")));

        Assert.True(failure == null, "the Voicemod options section did not build: " + failure);
    }
}
