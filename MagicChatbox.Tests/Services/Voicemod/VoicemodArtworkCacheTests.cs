using vrcosc_magicchatbox.Services.Voicemod;
using Xunit;

namespace MagicChatbox.Tests.Services.Voicemod;

public class VoicemodArtworkCacheTests
{
    // Smallest real PNG there is. Anything that decodes this proves the base64 path works.
    private const string OnePixelPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    [Fact]
    public void Voices_and_sounds_never_collide_even_with_the_same_id()
    {
        var cache = new VoicemodArtworkCache();

        Assert.True(cache.Store("voice", "shared-id", OnePixelPng));

        Assert.True(cache.Contains("voice", "shared-id"));
        Assert.False(cache.Contains("sound", "shared-id"));
    }

    [Fact]
    public void A_stored_image_raises_the_event_so_a_bound_view_can_refresh()
    {
        var cache = new VoicemodArtworkCache();
        string? raised = null;
        cache.ArtworkStored += (_, e) => raised = e.Key;

        cache.Store("voice", "robot", OnePixelPng);

        Assert.Equal("voice:robot", raised);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-base64-at-all!!")]
    [InlineData("aGVsbG8gd29ybGQ=")]
    public void Junk_is_rejected_rather_than_cached(string payload)
    {
        var cache = new VoicemodArtworkCache();

        Assert.False(cache.Store("voice", "robot", payload));
        Assert.False(cache.Contains("voice", "robot"));
    }

    [Fact]
    public void A_data_uri_prefix_is_tolerated()
    {
        var cache = new VoicemodArtworkCache();

        Assert.True(cache.Store("sound", "airhorn", "data:image/png;base64," + OnePixelPng));
        Assert.NotNull(cache.Get("sound", "airhorn"));
    }

    [Fact]
    public void A_full_cache_drops_its_oldest_entry_rather_than_refusing_new_ones()
    {
        // Browsing a library of well over a thousand sounds passes the cap long before you stop
        // scrolling. Refusing to store meant icons simply stopped appearing partway through.
        var cache = new VoicemodArtworkCache();

        for (int i = 0; i < 460; i++)
            Assert.True(cache.Store("sound", $"sound-{i}", OnePixelPng));

        Assert.True(cache.Contains("sound", "sound-459"));
        Assert.False(cache.Contains("sound", "sound-0"));
    }

    [Fact]
    public void Restoring_an_evicted_entry_works_the_second_time_too()
    {
        var cache = new VoicemodArtworkCache();
        for (int i = 0; i < 460; i++)
            cache.Store("sound", $"sound-{i}", OnePixelPng);

        Assert.True(cache.Store("sound", "sound-0", OnePixelPng));
        Assert.NotNull(cache.Get("sound", "sound-0"));
    }

    [Fact]
    public void Clearing_drops_everything_so_a_reconnect_cannot_show_a_stale_icon()
    {
        var cache = new VoicemodArtworkCache();
        cache.Store("voice", "robot", OnePixelPng);

        cache.Clear();

        Assert.False(cache.Contains("voice", "robot"));
        Assert.Null(cache.Get("voice", "robot"));
    }
}
