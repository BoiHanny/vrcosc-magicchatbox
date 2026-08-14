using System.Collections.Generic;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.Utilities;
using vrcosc_magicchatbox.Core.Osc;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules;

/// <summary>
/// Discord's line is made of a channel name, other people's nicknames and a head count, none of
/// which it owns and none of which had a limit. A busy voice channel could hand the builder about
/// three times the whole 144 characters and take every other integration off screen. These pin the
/// bound, the order things are given up in, and which half of the line is allowed to shrink.
/// </summary>
public class DiscordOutputTests
{
    private const int Line = OscBuildContext.MaxOscLength;
    private const string Nickname = "nickname";

    private static DiscordSettings Settings() => new();

    /// <summary>Distinct names of a fixed length, so a cap shows up as a missing character.</summary>
    private static List<string> Speakers(int count, int nameLength = 32)
        => Enumerable
            .Range(0, count)
            .Select(i => new string('n', nameLength - 2) + i.ToString("D2"))
            .ToList();

    #region The bound

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(20)]
    [InlineData(60)]
    [InlineData(Line)]
    public void The_segment_never_exceeds_the_room_it_was_given(int budget)
    {
        var settings = Settings();
        settings.MaxSpeakingUsersToShow = 10;

        // The worst there is: a full length channel name, a full voice channel, and everyone in it
        // speaking under a name at Discord's own 32 character limit.
        string text = DiscordModule.BuildOutputString(
            settings, new string('c', 100), 99, Speakers(99), isMuted: false, isDeafened: true, budget);

        Assert.True(text.Length <= budget, $"budget {budget} produced {text.Length}: {text}");
    }

    [Fact]
    public void No_room_left_means_no_segment()
        => Assert.Equal(
            string.Empty,
            DiscordModule.BuildOutputString(Settings(), "general", 2, [Nickname], false, false, budget: 0));

    [Fact]
    public void A_long_nickname_is_cut_rather_than_printed_whole()
    {
        string text = DiscordModule.BuildOutputString(
            Settings(), "general", 2, [new string('n', 32)], false, false, Line);

        Assert.DoesNotContain(new string('n', 32), text);
        Assert.Contains("…", text);
    }

    [Fact]
    public void A_long_channel_name_is_cut_rather_than_printed_whole()
    {
        var settings = Settings();
        settings.Template = "{channel}";

        string text = DiscordModule.BuildOutputString(
            settings, new string('c', 100), 2, [Nickname], false, false, Line);

        Assert.True(text.Length < 100, $"{text.Length} characters of channel name survived");
    }

    #endregion

    #region What gets given up, and in what order

    [Fact]
    public void The_speaker_list_gives_way_before_the_channel_does()
    {
        var settings = Settings();
        settings.MaxSpeakingUsersToShow = 10;
        settings.Template = "{channel} | {speaking}";

        string text = DiscordModule.BuildOutputString(
            settings, "channelname", 10, Speakers(10, 16), false, false, budget: 40);

        Assert.True(text.Length <= 40, $"{text.Length} characters: {text}");
        Assert.StartsWith("channelname", text);
    }

    [Fact]
    public void A_segment_that_will_not_fit_shortens_instead_of_vanishing()
    {
        var settings = Settings();
        settings.MaxSpeakingUsersToShow = 10;
        settings.Template = "{channel} | {speaking}";

        string text = DiscordModule.BuildOutputString(
            settings, new string('c', 100), 99, Speakers(99), false, false, budget: 30);

        Assert.NotEmpty(text);
        Assert.True(text.Length <= 30, $"{text.Length} characters: {text}");
    }

    #endregion

    #region The value/label rule

    [Fact]
    public void The_speaker_stays_full_size_while_the_state_word_is_raised()
    {
        var settings = Settings();
        settings.Template = "{speaking} {mute_state}";

        string text = DiscordModule.BuildOutputString(
            settings, "general", 2, [Nickname], isMuted: true, isDeafened: false, Line);

        Assert.Contains(Nickname, text);
        Assert.Contains(TextUtilities.TransformToSuperscript("muted"), text);
    }

    [Fact]
    public void The_state_word_with_no_raised_form_is_left_whole()
    {
        var settings = Settings();
        settings.Template = "{voice_state}";

        string text = DiscordModule.BuildOutputString(settings, "general", 2, [], false, false, Line);

        // Nothing that draws has a raised q, so raising "quiet" would strand one full-size letter in
        // the middle of the word and read as a rendering fault.
        Assert.Equal("quiet", text);
    }

    [Fact]
    public void The_count_of_the_speakers_that_did_not_fit_is_raised()
    {
        var settings = Settings();
        settings.Template = "{speaking}";
        settings.MaxSpeakingUsersToShow = 2;

        string text = DiscordModule.BuildOutputString(
            settings, "general", 5, Speakers(5, 6), false, false, Line);

        Assert.Contains("⁽⁺³⁾", text);
    }

    [Fact]
    public void The_head_count_is_a_number_the_reader_is_here_for_and_stays_full_size()
    {
        var settings = Settings();
        settings.Template = "{count} {speaking_count}";

        string text = DiscordModule.BuildOutputString(
            settings, "general", 12, Speakers(3, 6), false, false, Line);

        Assert.Equal("12 3", text);
    }

    #endregion

    #region Whitespace

    [Fact]
    public void An_unused_token_no_longer_leaves_a_trailing_space()
    {
        var settings = Settings();
        settings.Template = "🔊 {channel} ({count}) | 🎙️ {speaking} {mute_emoji}";

        // Nobody is muted, so {mute_emoji} is empty and three of the shipped presets ended on a space.
        string text = DiscordModule.BuildOutputString(
            settings, "general", 2, [Nickname], isMuted: false, isDeafened: false, Line);

        Assert.Equal(text.TrimEnd(), text);
    }

    #endregion

    #region Names

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("", "", "")]
    [InlineData("   ", null, "")]
    public void A_speaker_with_no_name_gets_a_stand_in_not_a_snowflake(string? nick, string? globalName, string? username)
    {
        string name = DiscordModule.ResolveDisplayName(nick, globalName, username);

        Assert.Equal(DiscordModule.UnknownSpeaker, name);
        Assert.All(name, c => Assert.False(char.IsDigit(c), "the stand-in must not be able to carry an id"));
    }

    [Fact]
    public void The_names_discord_did_send_are_preferred_in_order()
    {
        Assert.Equal("nick", DiscordModule.ResolveDisplayName("nick", "global", "user"));
        Assert.Equal("global", DiscordModule.ResolveDisplayName(null, "global", "user"));
        Assert.Equal("user", DiscordModule.ResolveDisplayName(" ", null, "user"));
    }

    #endregion
}
