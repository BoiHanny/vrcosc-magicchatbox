using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using vrcosc_magicchatbox.UI.Controls.Voicemod;
using Xunit;
using Xunit.Abstractions;

namespace MagicChatbox.Tests.UI;

/// <summary>
/// The Voicemod panel on the Integrations page is behind a template so a switched-off integration does
/// not pay for it. Collapsing a control does not stop WPF building it, so this checks the panel is
/// genuinely absent rather than merely invisible.
/// </summary>
public class VoicemodPanelDeferralTests
{
    private readonly ITestOutputHelper _out;

    public VoicemodPanelDeferralTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void A_collapsed_host_does_not_build_its_templated_content()
    {
        bool builtWhenCollapsed = false;
        bool builtWhenVisible = false;

        Exception? failure = WpfHost.RunInWindow(
            () => new ContentControl
            {
                Width = 600,
                Height = 400,
                Content = new Border
                {
                    Visibility = Visibility.Collapsed,
                    Child = new ContentControl
                    {
                        Content = new object(),
                        ContentTemplate = PanelTemplate(),
                    },
                },
            },
            element =>
            {
                element.UpdateLayout();
                builtWhenCollapsed = Contains<VoicemodControlPanel>(element);

                var border = (Border)((ContentControl)element).Content;
                border.Visibility = Visibility.Visible;
                element.UpdateLayout();
                builtWhenVisible = Contains<VoicemodControlPanel>(element);
            });

        Assert.True(failure == null, "the host did not build: " + failure);
        _out.WriteLine($"panel present while collapsed: {builtWhenCollapsed}, while visible: {builtWhenVisible}");

        Assert.True(builtWhenVisible, "the panel never appeared, so this measures nothing");
        Assert.False(builtWhenCollapsed, "the panel is built even while its host is collapsed");
    }

    private static DataTemplate PanelTemplate()
    {
        var template = new DataTemplate { VisualTree = new FrameworkElementFactory(typeof(VoicemodControlPanel)) };
        template.Seal();
        return template;
    }

    private static bool Contains<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T)
            return true;

        int children = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < children; i++)
        {
            if (Contains<T>(VisualTreeHelper.GetChild(root, i)))
                return true;
        }

        return false;
    }
}
