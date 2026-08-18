using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.Modules.Lyrics;
using vrcosc_magicchatbox.ViewModels;
using Xunit;

namespace MagicChatbox.Tests.UI;

/// <summary>
/// The dropdowns in the chatting, status and music sections. Their contents are C# identifiers
/// until a [Description] gives them words, and the words are all the reader ever gets.
/// </summary>
public class OptionsDropdownWordingTests
{
    public static TheoryData<Type> ReaderFacingEnums() =>
    [
        typeof(ChatAutocompleteMode),
        typeof(MediaLinkTimeSeekbar),
        typeof(SpotifyProgressDisplayMode),
        typeof(SpotifyMediaLinkCoexistence),
        typeof(LyricsMediaCoexistence),
        typeof(LyricsMatchStrictness),
    ];

    [Theory]
    [MemberData(nameof(ReaderFacingEnums))]
    public void Every_choice_carries_words_of_its_own(Type enumType)
    {
        var bare = Enum.GetNames(enumType).Where(name => Description(enumType, name) == null).ToList();

        Assert.True(bare.Count == 0, $"{enumType.Name} would show raw identifiers: {string.Join(", ", bare)}");
    }

    [Theory]
    [MemberData(nameof(ReaderFacingEnums))]
    public void No_choice_is_described_by_repeating_its_identifier(Type enumType)
    {
        // "SmallNumbers" -> "Small numbers" reads like English and explains nothing. A description
        // has to say what happens, not spell the same word with a space in it.
        var restated = Enum.GetNames(enumType)
            .Where(name => string.Equals(Squash(Description(enumType, name)), name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(restated.Count == 0, $"{enumType.Name} only restates itself for: {string.Join(", ", restated)}");
    }

    [Theory]
    [MemberData(nameof(ReaderFacingEnums))]
    public void No_two_choices_read_the_same(Type enumType)
    {
        var descriptions = Enum.GetNames(enumType).Select(name => Description(enumType, name)).ToList();

        Assert.Equal(descriptions.Count, descriptions.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void The_two_ways_of_showing_progress_agree_on_their_wording()
    {
        // Spotify and Media link share the same progress bars, so their menus must not describe the
        // same three choices in two different vocabularies.
        Assert.Equal(
            Description(typeof(MediaLinkTimeSeekbar), nameof(MediaLinkTimeSeekbar.NumbersAndSeekBar)),
            Description(typeof(SpotifyProgressDisplayMode), nameof(SpotifyProgressDisplayMode.Seekbar)));

        Assert.Equal(
            Description(typeof(MediaLinkTimeSeekbar), nameof(MediaLinkTimeSeekbar.SmallNumbers)),
            Description(typeof(SpotifyProgressDisplayMode), nameof(SpotifyProgressDisplayMode.SmallNumbers)));
    }

    private static string? Description(Type enumType, string name)
        => enumType.GetField(name, BindingFlags.Public | BindingFlags.Static)
            ?.GetCustomAttribute<DescriptionAttribute>()
            ?.Description;

    private static string Squash(string? description)
        => description == null ? string.Empty : new string(description.Where(char.IsLetterOrDigit).ToArray());
}
