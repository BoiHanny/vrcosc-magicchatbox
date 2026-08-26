using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.ViewModels.State;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules;

public class IntegrationTileCatalogTests
{
    [Fact]
    public void EveryTileInTheSortOrderHasACatalogEntry()
    {
        var followers = IntegrationDisplayState.FollowerKeys;

        foreach (var key in IntegrationDisplayState.DefaultSortOrder)
        {
            if (followers.Any(f => string.Equals(f, key, StringComparison.OrdinalIgnoreCase)))
                continue;

            Assert.True(IntegrationTileCatalog.TryGet(key, out _), $"No catalog entry for '{key}'");
        }
    }

    [Fact]
    public void LyricsIsNotATileBecauseItHasNoCard()
    {
        Assert.False(IntegrationTileCatalog.TryGet("Lyrics", out _));
    }

    [Fact]
    public void TheCatalogCoversAllSeventeenTilesWithUniqueKeysAndElements()
    {
        Assert.Equal(17, IntegrationTileCatalog.Tiles.Count);
        Assert.Equal(17, IntegrationTileCatalog.Tiles.Select(t => t.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(17, IntegrationTileCatalog.Tiles.Select(t => t.ElementName).Distinct(StringComparer.Ordinal).Count());
        Assert.All(IntegrationTileCatalog.Tiles, t => Assert.False(string.IsNullOrWhiteSpace(t.DisplayName)));
    }

    [Fact]
    public void KeysAreMatchedCaseInsensitivelyLikeTheItemMap()
    {
        Assert.True(IntegrationTileCatalog.TryGet("spotify", out var lower));
        Assert.True(IntegrationTileCatalog.TryGet("SPOTIFY", out var upper));
        Assert.Equal("Spotify", lower.Key);
        Assert.Equal("Spotify", upper.Key);
    }

    // This is the regression for the bug adversarial review caught: ApplyIntegrationOrder has a
    // safety-net pass that re-adds any key the first pass did not record, so skipping a hidden key
    // without recording it made hidden tiles reappear at the bottom of the page.
    [Fact]
    public void AHiddenTileDoesNotComeBackViaTheSafetyNetPass()
    {
        var order = IntegrationDisplayState.DefaultSortOrder;
        var visible = IntegrationTileCatalog.VisibleKeysInOrder(order, new[] { "Spotify" });

        Assert.DoesNotContain("Spotify", visible, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(16, visible.Count);
    }

    [Fact]
    public void ATileMissingFromTheSortOrderStillAppearsUnlessHidden()
    {
        // Deliberately partial order: the safety net is what puts the rest back.
        var partial = new[] { "Status", "Spotify" };

        var visible = IntegrationTileCatalog.VisibleKeysInOrder(partial, Array.Empty<string>());
        Assert.Equal(17, visible.Count);
        Assert.Equal("Status", visible[0]);
        Assert.Equal("Spotify", visible[1]);

        var withHidden = IntegrationTileCatalog.VisibleKeysInOrder(partial, new[] { "Weather" });
        Assert.Equal(16, withHidden.Count);
        Assert.DoesNotContain("Weather", withHidden, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void HidingEverythingLeavesNothingVisible()
    {
        var visible = IntegrationTileCatalog.VisibleKeysInOrder(
            IntegrationDisplayState.DefaultSortOrder, IntegrationTileCatalog.Keys);

        Assert.Empty(visible);
    }

    [Fact]
    public void UnknownAndBlankStoredKeysAreIgnoredRatherThanHidingSomething()
    {
        var hidden = IntegrationTileCatalog.ResolveHidden(new[] { "NotAnIntegration", "", "  ", null, "Twitch" });

        Assert.Single(hidden);
        Assert.Contains("Twitch", hidden);
    }

    [Fact]
    public void MasterPropertiesMapBackToTheirTile()
    {
        // The irregular ones are the point of this test: these names do not follow "Intgr" + key.
        Assert.True(IntegrationTileCatalog.TryKeyForMasterProperty("IntgrScanWindowActivity", out var window));
        Assert.Equal("Window", window);

        Assert.True(IntegrationTileCatalog.TryKeyForMasterProperty("IntgrScanWindowTime", out var time));
        Assert.Equal("Time", time);

        Assert.True(IntegrationTileCatalog.TryKeyForMasterProperty("IntgrScanMediaLink", out var media));
        Assert.Equal("MediaLink", media);

        Assert.True(IntegrationTileCatalog.TryKeyForMasterProperty("IntgrNetworkStatistics", out var network));
        Assert.Equal("Network", network);

        Assert.True(IntegrationTileCatalog.TryKeyForMasterProperty("IntgrVoicemod", out var voicemod));
        Assert.Equal("Voicemod", voicemod);

        // Weather's switch lives on WeatherSettings, not IntegrationSettings.
        Assert.True(IntegrationTileCatalog.TryKeyForMasterProperty("ShowWeatherInTime", out var weather));
        Assert.Equal("Weather", weather);
    }

    [Fact]
    public void WeatherReadsItsMasterFromWeatherSettings()
    {
        var integrations = new IntegrationSettings();
        var weather = new WeatherSettings { ShowWeatherInTime = false };

        Assert.False(IntegrationTileCatalog.IsMasterOn("Weather", integrations, weather));

        weather.ShowWeatherInTime = true;
        Assert.True(IntegrationTileCatalog.IsMasterOn("Weather", integrations, weather));
    }

    [Fact]
    public void SwitchedOffKeysAreReportedForTheOneShot()
    {
        var integrations = new IntegrationSettings { IntgrTwitch = false, IntgrSpotify = true };
        var weather = new WeatherSettings();

        var off = IntegrationTileCatalog.KeysWithMasterOff(new[] { "Twitch", "Spotify" }, integrations, weather);

        Assert.Contains("Twitch", off);
        Assert.DoesNotContain("Spotify", off);
    }

    // The user's hard requirement: hiding is visual only. If this ever fails, hiding has started
    // changing what the app actually does.
    [Fact]
    public void ResolvingAndOrderingHiddenTilesMutatesNoSetting()
    {
        var integrations = new IntegrationSettings();
        var weather = new WeatherSettings();

        var before = Snapshot(integrations).Concat(Snapshot(weather)).ToList();

        IntegrationTileCatalog.ResolveHidden(IntegrationTileCatalog.Keys);
        IntegrationTileCatalog.VisibleKeysInOrder(IntegrationDisplayState.DefaultSortOrder, IntegrationTileCatalog.Keys);
        IntegrationTileCatalog.KeysWithMasterOff(IntegrationTileCatalog.Keys, integrations, weather);
        foreach (var key in IntegrationTileCatalog.Keys)
            IntegrationTileCatalog.IsMasterOn(key, integrations, weather);

        var after = Snapshot(integrations).Concat(Snapshot(weather)).ToList();

        Assert.Equal(before, after);
    }

    private static List<string> Snapshot(object settings)
    {
        var values = new List<string>();

        foreach (var prop in settings.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            object? value;
            try { value = prop.GetValue(settings); }
            catch { continue; }

            values.Add($"{prop.Name}={value switch
            {
                null => "<null>",
                System.Collections.IEnumerable e and not string => string.Join(",", e.Cast<object>()),
                _ => value.ToString(),
            }}");
        }

        return values;
    }
}
