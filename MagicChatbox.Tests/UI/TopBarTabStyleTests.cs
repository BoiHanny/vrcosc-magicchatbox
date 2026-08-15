using System;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using vrcosc_magicchatbox.Classes;
using Xunit;

namespace MagicChatbox.Tests.UI;

/// <summary>
/// The top bar tab style, exercised rather than trusted.
/// </summary>
/// <remarks>
/// A control template compiles whatever it references. Everything it asks for by StaticResource -
/// the converter, the brushes, the attached property - is only resolved when something is actually
/// built from it, and a miss throws at that moment rather than at build. This does the building.
/// </remarks>
public class TopBarTabStyleTests
{
    [Fact]
    public void A_tab_can_actually_be_built_from_the_style()
    {
        Exception? failure = RunOnUiThread(() =>
        {
            // Loaded straight from the dictionary rather than through Application.Current: that is
            // a process-wide singleton another test may have created first, and then this would be
            // testing whatever it happened to merge.
            var theme = LoadTheme();
            var style = (Style)theme["TopBarTabStyle"];

            var button = new Button { Style = style, Content = "Integrations" };
            button.Resources.MergedDictionaries.Add(theme);

            button.Measure(new Size(200, 55));
            button.Arrange(new Rect(0, 0, 200, 55));
        });

        Assert.Null(failure);
    }

    [Fact]
    public void The_active_tab_marker_survives_a_round_trip()
    {
        Exception? failure = RunOnUiThread(() =>
        {
            var button = new Button();
            Assert.False(ButtonProperties.GetIsActive(button));

            ButtonProperties.SetIsActive(button, true);
            Assert.True(ButtonProperties.GetIsActive(button));
        });

        Assert.Null(failure);
    }

    [Theory]
    [InlineData(0, "0", true)]
    [InlineData(1, "0", false)]
    [InlineData(3, "3", true)]
    [InlineData(2, "not a number", false)]
    public void The_index_converter_answers_only_for_its_own_tab(int selected, string parameter, bool expected)
    {
        var converter = new IndexToBoolConverter();

        Assert.Equal(expected, converter.Convert(selected, typeof(bool), parameter, CultureInfo.InvariantCulture));
    }

    private static ResourceDictionary LoadTheme() => new()
    {
        Source = new Uri("pack://application:,,,/MagicChatbox;component/UI/Theme.xaml", UriKind.Absolute),
    };

    [Fact]
    public void The_converter_the_tabs_bind_through_is_registered()
    {
        Exception? failure = RunOnUiThread(() =>
        {
            var converters = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/MagicChatbox;component/UI/Resources/SharedConverters.xaml", UriKind.Absolute),
            };

            Assert.IsType<IndexToBoolConverter>(converters["IndexToBoolConverter"]);
        });

        Assert.Null(failure);
    }

    private static Exception? RunOnUiThread(Action action)
    {
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(30));

        return failure;
    }
}
