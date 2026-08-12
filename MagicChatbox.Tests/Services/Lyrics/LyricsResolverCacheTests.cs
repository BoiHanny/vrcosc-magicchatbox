using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.Modules.Lyrics;
using vrcosc_magicchatbox.Services.Lyrics;
using Xunit;

namespace MagicChatbox.Tests.Services.Lyrics;

public class LyricsResolverCacheTests
{
    private sealed class CountingProvider : ILyricsProvider
    {
        public int Calls;

        public string Name => "Counting";
        public bool RequiresInternet => true;

        public Task<LyricTrack?> TryGetAsync(LyricsQuery query, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult<LyricTrack?>(null);
        }
    }

    private static LyricsQuery Query(int index) => new()
    {
        Title = $"Track {index}",
        Artist = "Artist",
        Duration = TimeSpan.FromSeconds(200),
    };

    [Fact]
    public async Task ARepeatedMissDoesNotHitTheNetworkTwice()
    {
        var provider = new CountingProvider();
        var resolver = new LyricsResolver(new List<ILyricsProvider> { provider }, () => true);

        await resolver.ResolveAsync(Query(1));
        await resolver.ResolveAsync(Query(1));
        await resolver.ResolveAsync(Query(1));

        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task FillingTheCacheDoesNotThrowAwayEverySingleEntry()
    {
        var provider = new CountingProvider();
        var resolver = new LyricsResolver(new List<ILyricsProvider> { provider }, () => true);

        for (int i = 0; i < LyricsResolver.MaxCacheEntries + 5; i++)
            await resolver.ResolveAsync(Query(i));

        int callsAfterFill = provider.Calls;

        var recent = Query(LyricsResolver.MaxCacheEntries + 4);
        await resolver.ResolveAsync(recent);

        Assert.Equal(callsAfterFill, provider.Calls);
    }

    [Fact]
    public async Task AnExpiredMissIsRetried()
    {
        var provider = new CountingProvider();
        var now = new DateTime(2026, 8, 12, 12, 0, 0, DateTimeKind.Utc);
        var resolver = new LyricsResolver(new List<ILyricsProvider> { provider }, () => true, () => now);

        await resolver.ResolveAsync(Query(1));
        Assert.Equal(1, provider.Calls);

        now += LyricsResolver.NegativeCacheLifetime + TimeSpan.FromMinutes(1);
        await resolver.ResolveAsync(Query(1));

        Assert.Equal(2, provider.Calls);
    }

    [Fact]
    public async Task InternetDeniedMeansNoProviderIsEverCalled()
    {
        var provider = new CountingProvider();
        var resolver = new LyricsResolver(new List<ILyricsProvider> { provider }, () => false);

        await resolver.ResolveAsync(Query(1));

        Assert.Equal(0, provider.Calls);
    }
}
