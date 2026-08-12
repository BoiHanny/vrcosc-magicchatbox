using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.Modules.Lyrics;

namespace vrcosc_magicchatbox.Services.Lyrics;

public sealed record LyricsLookupResult(LyricTrack? Track, string ProviderName, bool WasCached);

public sealed class LyricsResolver
{
    public static readonly TimeSpan NegativeCacheLifetime = TimeSpan.FromHours(24);
    public const int MaxCacheEntries = 128;

    private readonly IReadOnlyList<ILyricsProvider> _providers;
    private readonly Func<bool> _internetAllowed;
    private readonly Func<DateTime> _utcNow;
    private readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    public LyricsResolver(
        IReadOnlyList<ILyricsProvider> providers,
        Func<bool> internetAllowed,
        Func<DateTime>? utcNow = null)
    {
        _providers = providers;
        _internetAllowed = internetAllowed;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    public async Task<LyricsLookupResult> ResolveAsync(LyricsQuery query, CancellationToken ct = default)
    {
        if (!query.IsUsable)
            return new LyricsLookupResult(null, string.Empty, false);

        string key = query.CacheKey;

        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var cached) && !IsExpired(cached))
                return new LyricsLookupResult(cached.Track, cached.ProviderName, true);
        }

        foreach (var provider in _providers)
        {
            if (provider.RequiresInternet && !_internetAllowed())
                continue;

            var track = await provider.TryGetAsync(query, ct).ConfigureAwait(false);
            if (track is { IsSynced: true })
            {
                Store(key, track, provider.Name);
                return new LyricsLookupResult(track, provider.Name, false);
            }
        }

        Store(key, null, string.Empty);
        return new LyricsLookupResult(null, string.Empty, false);
    }

    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
        }
    }

    private bool IsExpired(CacheEntry entry)
        => entry.Track == null && _utcNow() - entry.StoredAtUtc > NegativeCacheLifetime;

    private void Store(string key, LyricTrack? track, string providerName)
    {
        lock (_lock)
        {
            if (!_cache.ContainsKey(key) && _cache.Count >= MaxCacheEntries)
                EvictOldest();

            _cache[key] = new CacheEntry(track, providerName, _utcNow());
        }
    }

    private void EvictOldest()
    {
        string? oldestKey = null;
        DateTime oldestStamp = DateTime.MaxValue;

        foreach (var pair in _cache)
        {
            if (pair.Value.StoredAtUtc < oldestStamp)
            {
                oldestStamp = pair.Value.StoredAtUtc;
                oldestKey = pair.Key;
            }
        }

        if (oldestKey != null)
            _cache.Remove(oldestKey);
    }

    private readonly record struct CacheEntry(LyricTrack? Track, string ProviderName, DateTime StoredAtUtc);
}
