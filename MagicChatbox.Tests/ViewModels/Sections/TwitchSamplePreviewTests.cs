using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.ViewModels.Sections;
using Xunit;

namespace MagicChatbox.Tests.ViewModels.Sections;

/// <summary>
/// Twitch asks the user to lay out a line out of fifteen placeholders and, until now, showed them
/// nothing back. The preview is only worth having if it is the same formatter the chatbox uses, so
/// these hold it to that and to reacting to the switches beside it.
/// </summary>
public sealed class TwitchSamplePreviewTests
{
    private static TwitchSettings Settings() => new() { ChannelName = "channel" };

    [Fact]
    public void The_preview_is_the_formatter_the_chatbox_uses()
    {
        var settings = Settings();

        Assert.Equal(
            TwitchModule.BuildOutputString(
                settings,
                TwitchSectionViewModel.SampleGame,
                TwitchSectionViewModel.SampleViewerCount,
                TwitchSectionViewModel.SampleFollowerCount,
                TwitchSectionViewModel.SampleStreamTitle,
                isLive: true),
            TwitchSectionViewModel.BuildSampleLine(settings, isLive: true));
    }

    [Fact]
    public void It_has_something_to_show_before_the_channel_has_ever_been_live()
    {
        string line = TwitchSectionViewModel.BuildSampleLine(Settings(), isLive: true);

        Assert.NotEmpty(line);
        Assert.Contains(TwitchSectionViewModel.SampleGame, line);
        Assert.Contains("1234", line);
    }

    [Fact]
    public void Turning_the_viewer_count_off_takes_it_out_of_the_preview()
    {
        var settings = Settings();
        Assert.Contains("1234", TwitchSectionViewModel.BuildSampleLine(settings, isLive: true));

        settings.ShowViewerCount = false;

        Assert.DoesNotContain("1234", TwitchSectionViewModel.BuildSampleLine(settings, isLive: true));
    }

    [Fact]
    public void The_shortened_number_is_the_one_the_checkbox_promises()
    {
        var compact = Settings();
        compact.ViewerCountCompact = true;
        compact.ShowFollowerCount = true;
        compact.FollowerCountCompact = true;

        string line = TwitchSectionViewModel.BuildSampleLine(compact, isLive: true);

        // The two checkboxes beside this preview name these exact results, so if the rounding ever
        // changes the copy has to change with it.
        Assert.Contains("1.23K", line);
        Assert.Contains("8.42K", line);
    }

    [Fact]
    public void A_custom_layout_is_previewed_with_the_sample_values_in_it()
    {
        var settings = Settings();
        settings.Template = "{live} · {game} · {viewerCount}";

        string line = TwitchSectionViewModel.BuildSampleLine(settings, isLive: true);

        Assert.StartsWith("LIVE", line);
        Assert.Contains(TwitchSectionViewModel.SampleGame, line);
        Assert.DoesNotContain("{", line);
    }

    [Fact]
    public void The_offline_preview_is_empty_when_the_offline_message_is()
    {
        var settings = Settings();
        Assert.Equal(string.Empty, TwitchSectionViewModel.BuildSampleLine(settings, isLive: false));

        settings.OfflineMessage = "stream is over, see you tomorrow";

        Assert.Equal(
            "stream is over, see you tomorrow",
            TwitchSectionViewModel.BuildSampleLine(settings, isLive: false));
    }
}
