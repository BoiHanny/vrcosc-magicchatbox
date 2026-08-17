using System;
using System.Collections.Generic;
using System.Linq;
using vrcosc_magicchatbox.Core.Vrc;
using Xunit;

namespace MagicChatbox.Tests.Core.Vrc;

// "The same on every avatar" only works for names that are on more than one avatar, and most are not:
// measured across 390 avatars on this machine, 4,768 distinct saved names of which 69.3% appear on
// exactly one. What travels is the ecosystem's shared core - Go/Locomotion on 180 of them, face
// tracking on 96 - and this finds that core in the user's own library rather than assuming a list.
public class AvatarLibraryIndexTests
{
    private static LocalAvatarState Avatar(string id, params (string Name, double Value)[] values)
        => new(
            id,
            1.3,
            false,
            values.Select(v => new LocalAvatarValue(v.Name, v.Value)).ToList(),
            DateTime.UtcNow);

    [Fact]
    public void A_name_on_one_avatar_is_not_something_that_travels()
    {
        var shared = AvatarLibraryIndex.Shared(
            [Avatar("a", ("Toggles/OnlyHere", 1)), Avatar("b", ("Toggles/Other", 1))],
            minimumAvatars: 2);

        Assert.Empty(shared);
    }

    [Fact]
    public void A_name_on_enough_avatars_is()
    {
        var shared = AvatarLibraryIndex.Shared(
            [
                Avatar("a", ("Go/Locomotion", 0)),
                Avatar("b", ("Go/Locomotion", 0)),
                Avatar("c", ("Go/Locomotion", 1)),
            ],
            minimumAvatars: 3);

        SharedParameter only = Assert.Single(shared);
        Assert.Equal("Go/Locomotion", only.Name);
        Assert.Equal(3, only.AvatarCount);
    }

    [Fact]
    public void The_value_offered_is_the_one_most_of_the_avatars_agree_on()
    {
        var shared = AvatarLibraryIndex.Shared(
            [
                Avatar("a", ("EyeTrackingActive", 1)),
                Avatar("b", ("EyeTrackingActive", 1)),
                Avatar("c", ("EyeTrackingActive", 0)),
            ],
            minimumAvatars: 2);

        Assert.Equal(1, Assert.Single(shared).MostCommonValue);
    }

    [Fact]
    public void VRChat_s_own_parameters_are_never_offered()
    {
        // VRCEmote is on 221 of the avatars here, so it would top the list - and adopting it as a
        // default would make somebody perform every time they changed avatar.
        var shared = AvatarLibraryIndex.Shared(
            [
                Avatar("a", ("VRCEmote", 3), ("Go/Locomotion", 0)),
                Avatar("b", ("VRCEmote", 3), ("Go/Locomotion", 0)),
                Avatar("c", ("VRCEmote", 3), ("Go/Locomotion", 0)),
            ],
            minimumAvatars: 2);

        Assert.DoesNotContain(shared, s => s.Name == "VRCEmote");
        Assert.Contains(shared, s => s.Name == "Go/Locomotion");
    }

    [Fact]
    public void The_same_name_twice_on_one_avatar_counts_once()
    {
        var shared = AvatarLibraryIndex.Shared(
            [Avatar("a", ("Go/Locomotion", 0), ("Go/Locomotion", 1))],
            minimumAvatars: 1);

        Assert.Equal(1, Assert.Single(shared).AvatarCount);
    }

    [Fact]
    public void The_most_widely_shared_come_first()
    {
        var shared = AvatarLibraryIndex.Shared(
            [
                Avatar("a", ("Everywhere", 1), ("Sometimes", 1)),
                Avatar("b", ("Everywhere", 1), ("Sometimes", 1)),
                Avatar("c", ("Everywhere", 1)),
            ],
            minimumAvatars: 2);

        Assert.Equal("Everywhere", shared.First().Name);
    }

    [Fact]
    public void An_empty_library_is_an_empty_answer()
    {
        Assert.Empty(AvatarLibraryIndex.Shared(Array.Empty<LocalAvatarState>()));
    }

    [Fact]
    public void The_count_is_described_against_the_size_of_the_library()
    {
        var shared = new SharedParameter("Go/Locomotion", 180, 0);

        Assert.Equal("on 180 of your 390 avatars", shared.Describe(390));
        Assert.Equal("on 180 avatars", shared.Describe(0));
    }
}
