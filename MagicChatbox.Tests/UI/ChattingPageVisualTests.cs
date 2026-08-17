using System;
using System.Windows;
using System.Windows.Controls;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.UI.Pages;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;
using Xunit;

namespace MagicChatbox.Tests.UI;

/// <summary>
/// The chat page, built rather than trusted.
/// </summary>
/// <remarks>
/// Everything a template asks for by StaticResource - a brush, a style, a converter, an x:Static
/// constant - is resolved when something is built from it and not a moment sooner. A page can
/// compile perfectly and then throw XamlParseException the first time a person opens it. The only
/// way to know is to build it, and for the message rows that means giving the list something to
/// show: a DataTemplate that is never applied is a DataTemplate that is never checked.
/// </remarks>
[Collection(WpfCollection.Name)]
public class ChattingPageVisualTests
{
    [Theory]
    [InlineData("ChatMessageCard")]
    [InlineData("ChatRowActions")]
    [InlineData("ChatRowButton")]
    [InlineData("ChatRowToggle")]
    [InlineData("ChatComposerShell")]
    [InlineData("LiveTypingChip")]
    [InlineData("ChatComposerIconButton")]
    public void Every_chat_style_resolves_what_it_reaches_for(string key)
    {
        Exception? failure = WpfHost.Run(() =>
        {
            var style = (Style)Application.Current.Resources[key];
            FrameworkElement element = Build(style.TargetType);
            element.Style = style;

            element.Measure(new Size(400, 60));
            element.Arrange(new Rect(0, 0, 400, 60));
        });

        Assert.True(failure == null, key + " could not be applied: " + failure);
    }

    [Fact]
    public void The_page_and_a_message_row_both_build()
    {
        Exception? failure = WpfHost.RunInWindow(
            () =>
            {
                var chatStatus = new ChatStatusDisplayState();
                chatStatus.LastMessages.Add(new ChatItem(chatStatus)
                {
                    Msg = "the live one",
                    MainMsg = "the live one",
                    Opacity = "1",
                    IsRunning = true,
                    CanLiveEdit = true,
                });
                chatStatus.LastMessages.Add(new ChatItem(chatStatus)
                {
                    Msg = "an older one",
                    MainMsg = "an older one",
                    Opacity = "0.68",
                });

                return new ChattingPage { DataContext = new ChatPageStandIn(chatStatus, new ChatSettings()) };
            },
            page => Assert.NotNull(page.FindName("NewChattingTxt")));

        Assert.True(failure == null, "the chat page did not build: " + failure);
    }

    [Fact]
    public void The_live_toggle_is_bound_to_the_setting_that_survives_a_restart()
    {
        // The chip is the only way most people will ever reach live typing, so it has to write to
        // the stored setting rather than to page state that dies with the window.
        var settings = new ChatSettings();
        Assert.False(settings.ChatLiveTyping);

        Exception? failure = WpfHost.RunInWindow(
            () => new ChattingPage { DataContext = new ChatPageStandIn(new ChatStatusDisplayState(), settings) },
            page =>
            {
                var chip = (System.Windows.Controls.Primitives.ToggleButton?)page.FindName("LiveTypingToggle");
                Assert.NotNull(chip);
                chip!.IsChecked = true;
            });

        Assert.True(failure == null, "the live chip could not be toggled: " + failure);
        Assert.True(settings.ChatLiveTyping);
    }

    private static FrameworkElement Build(Type targetType)
    {
        if (targetType == typeof(Border)) return new Border { Child = new TextBlock { Text = "x" } };
        if (targetType == typeof(StackPanel)) return new StackPanel();
        if (targetType == typeof(Button)) return new Button { Content = "x" };
        if (targetType == typeof(System.Windows.Controls.Primitives.ToggleButton))
            return new System.Windows.Controls.Primitives.ToggleButton { Content = "x" };

        throw new NotSupportedException("no stand-in for " + targetType.Name);
    }

    /// <summary>
    /// Enough of the page's view model for its bindings to resolve. Bindings are matched by name at
    /// runtime, so this does not need to be the real type - only to carry the same shape.
    /// </summary>
    // Public on purpose: WPF resolves binding paths by reflection and will not read properties off a
    // type it cannot see, so a private stand-in binds to nothing and every assertion passes vacuously.
    public sealed class ChatPageStandIn
    {
        public ChatPageStandIn(ChatStatusDisplayState chatStatus, ChatSettings chatSettings)
        {
            ChatStatus = chatStatus;
            ChatSettings = chatSettings;
        }

        public ChatStatusDisplayState ChatStatus { get; }

        public ChatSettings ChatSettings { get; }
    }
}
