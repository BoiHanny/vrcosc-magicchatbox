using System.Collections.Generic;
using System.Linq;
using vrcosc_magicchatbox.ViewModels.State;
using Xunit;

namespace MagicChatbox.Tests.ViewModels.State;

public class IntegrationSortOrderTests
{
    private static List<string> WithoutFollowers()
        => IntegrationDisplayState.DefaultSortOrder
            .Where(key => !IntegrationDisplayState.IsFollower(key))
            .ToList();

    [Fact]
    public void LyricsIsAFollowerAndIsNotOfferedForReordering()
    {
        Assert.True(IntegrationDisplayState.IsFollower("Lyrics"));
        Assert.True(IntegrationDisplayState.IsFollower("lyrics"));
        Assert.False(IntegrationDisplayState.IsFollower("MediaLink"));
        Assert.False(IntegrationDisplayState.IsFollower("Spotify"));
    }

    [Fact]
    public void ASavedOrderWithoutLyricsStillGetsLyricsBack()
    {
        var normalized = IntegrationDisplayState.NormalizeSortOrder(WithoutFollowers());

        Assert.Contains("Lyrics", normalized);
    }

    [Fact]
    public void LyricsIsReanchoredDirectlyAfterMediaLink()
    {
        var normalized = IntegrationDisplayState.NormalizeSortOrder(WithoutFollowers());

        int media = normalized.IndexOf("MediaLink");
        int lyrics = normalized.IndexOf("Lyrics");

        Assert.True(media >= 0);
        Assert.Equal(media + 1, lyrics);
    }

    [Fact]
    public void LyricsFollowsMediaLinkEvenWhenMediaLinkIsMovedToTheFront()
    {
        var reordered = WithoutFollowers();
        reordered.Remove("MediaLink");
        reordered.Insert(0, "MediaLink");

        var normalized = IntegrationDisplayState.NormalizeSortOrder(reordered);

        Assert.Equal(0, normalized.IndexOf("MediaLink"));
        Assert.Equal(1, normalized.IndexOf("Lyrics"));
    }

    [Fact]
    public void EveryRealIntegrationSurvivesAReorderThatDropsFollowers()
    {
        var normalized = IntegrationDisplayState.NormalizeSortOrder(WithoutFollowers());

        foreach (string key in IntegrationDisplayState.DefaultSortOrder)
            Assert.Contains(key, normalized);
    }

    [Fact]
    public void NoDuplicatesAreIntroduced()
    {
        var normalized = IntegrationDisplayState.NormalizeSortOrder(WithoutFollowers());

        Assert.Equal(normalized.Count, normalized.Distinct().Count());
    }
}
