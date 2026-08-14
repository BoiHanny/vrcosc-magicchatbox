using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Osc;
using vrcosc_magicchatbox.ViewModels.Sections;
using Xunit;

namespace MagicChatbox.Tests.ViewModels.Sections;

/// <summary>
/// The Discord preview used to render whatever the live call happened to be, which outside a call
/// is nothing at all - so the template was authored against a blank line. These pin the stand-in
/// call and that it goes through the module's own formatter.
/// </summary>
public sealed class DiscordSamplePreviewTests
{
    private static DiscordSettings Settings() => new();

    [Fact]
    public void The_preview_is_the_formatter_the_chatbox_uses()
    {
        var settings = Settings();

        Assert.Equal(
            DiscordModule.BuildOutputString(
                settings,
                DiscordSectionViewModel.SampleChannelName,
                DiscordSectionViewModel.SampleChannelCount,
                DiscordSectionViewModel.SampleSpeakers,
                isMuted: false,
                isDeafened: false,
                OscBuildContext.MaxOscLength),
            DiscordSectionViewModel.BuildSampleLine(settings));
    }

    [Fact]
    public void It_shows_a_finished_line_with_nobody_connected()
    {
        string line = DiscordSectionViewModel.BuildSampleLine(Settings());

        Assert.NotEmpty(line);
        Assert.DoesNotContain("{", line);
        Assert.Contains(DiscordSectionViewModel.SampleChannelName, line);
    }

    [Fact]
    public void Asking_for_the_count_only_keeps_the_names_out_of_the_preview()
    {
        var settings = Settings();
        settings.ShowUserCountOnly = true;

        string line = DiscordSectionViewModel.BuildSampleLine(settings);

        foreach (string name in DiscordSectionViewModel.SampleSpeakers)
            Assert.DoesNotContain(name, line);
    }

    [Fact]
    public void Listing_fewer_names_shortens_the_preview()
    {
        var many = Settings();
        many.MaxSpeakingUsersToShow = DiscordSectionViewModel.SampleSpeakers.Length;

        var few = Settings();
        few.MaxSpeakingUsersToShow = 1;

        Assert.Contains(DiscordSectionViewModel.SampleSpeakers[0], DiscordSectionViewModel.BuildSampleLine(few));
        Assert.DoesNotContain(DiscordSectionViewModel.SampleSpeakers[^1], DiscordSectionViewModel.BuildSampleLine(few));
        Assert.Contains(DiscordSectionViewModel.SampleSpeakers[^1], DiscordSectionViewModel.BuildSampleLine(many));
    }

    [Fact]
    public void The_preview_never_shows_a_line_the_chatbox_would_refuse()
    {
        Assert.True(DiscordSectionViewModel.BuildSampleLine(Settings()).Length <= OscBuildContext.MaxOscLength);
    }
}
