using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules.Lyrics;

namespace vrcosc_magicchatbox.Services.Lyrics;

public sealed class LrcLibLyricsProvider : ILyricsProvider
{
    public const string BaseUrl = "https://lrclib.net";
    public const string ProviderName = "LRCLIB";

    private static readonly TimeSpan MinimumSpacing = TimeSpan.FromMilliseconds(350);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _userAgent;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private DateTime _nextAllowedRequestUtc = DateTime.MinValue;
    private DateTime _backoffUntilUtc = DateTime.MinValue;

    public LrcLibLyricsProvider(IHttpClientFactory httpClientFactory, string appVersion)
    {
        _httpClientFactory = httpClientFactory;
        _userAgent = $"MagicChatBox/{appVersion} (https://github.com/BoiHanny/vrcosc-magicchatbox)";
    }

    public string Name => ProviderName;

    public bool RequiresInternet => true;

    public async Task<LyricTrack?> TryGetAsync(LyricsQuery query, CancellationToken ct = default)
    {
        if (!query.IsUsable)
            return null;

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (DateTime.UtcNow < _backoffUntilUtc)
                return null;

            await RespectSpacingAsync(ct).ConfigureAwait(false);

            var direct = await GetDirectAsync(query, includeDuration: query.Duration > TimeSpan.Zero, ct)
                .ConfigureAwait(false);
            if (direct != null)
                return direct;

            if (query.Duration > TimeSpan.Zero)
            {
                await RespectSpacingAsync(ct).ConfigureAwait(false);
                direct = await GetDirectAsync(query, includeDuration: false, ct).ConfigureAwait(false);
                if (direct != null)
                    return direct;
            }

            await RespectSpacingAsync(ct).ConfigureAwait(false);
            var searched = await SearchAsync(query, structured: true, ct).ConfigureAwait(false);
            if (searched != null)
                return searched;

            await RespectSpacingAsync(ct).ConfigureAwait(false);
            return await SearchAsync(query, structured: false, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"LRCLIB lookup failed: {ex.Message}");
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<LyricTrack?> GetDirectAsync(LyricsQuery query, bool includeDuration, CancellationToken ct)
    {
        string url = $"{BaseUrl}/api/get"
                   + $"?track_name={Uri.EscapeDataString(query.Title)}"
                   + $"&artist_name={Uri.EscapeDataString(query.Artist)}";

        if (query.Album.Length > 0)
            url += $"&album_name={Uri.EscapeDataString(query.Album)}";

        if (includeDuration)
            url += $"&duration={(int)Math.Round(query.Duration.TotalSeconds)}";

        string? body = await SendAsync(url, ct).ConfigureAwait(false);
        if (body == null)
            return null;

        return ParseRecord(JObject.Parse(body));
    }

    private async Task<LyricTrack?> SearchAsync(LyricsQuery query, bool structured, CancellationToken ct)
    {
        string url = structured
            ? $"{BaseUrl}/api/search"
                + $"?track_name={Uri.EscapeDataString(query.Title)}"
                + $"&artist_name={Uri.EscapeDataString(query.Artist)}"
            : $"{BaseUrl}/api/search"
                + $"?q={Uri.EscapeDataString($"{query.Artist} {query.Title}")}";

        string? body = await SendAsync(url, ct).ConfigureAwait(false);
        if (body == null)
            return null;

        var results = JArray.Parse(body);
        var best = PickBest(results, query);

        return best == null ? null : ParseRecord(best);
    }

    public static JObject? PickBest(JArray results, LyricsQuery query)
    {
        var records = results.OfType<JObject>().ToList();
        if (records.Count == 0)
            return null;

        var candidates = records.Select(ToCandidate).ToList();
        var match = LyricsMatchScorer.PickBest(candidates, query);

        if (match.Index < 0)
        {
            Logging.WriteInfo(
                $"LRCLIB returned {records.Count} result(s) for \"{query.Artist} — {query.Title}\" but none matched well enough.");
            return null;
        }

        return records[match.Index];
    }

    private static LyricsCandidate ToCandidate(JObject record) => new(
        record["trackName"]?.ToString() ?? record["name"]?.ToString() ?? string.Empty,
        record["artistName"]?.ToString() ?? string.Empty,
        record["albumName"]?.ToString() ?? string.Empty,
        record["duration"]?.Value<double?>() ?? -1,
        record["instrumental"]?.Value<bool?>() ?? false,
        !string.IsNullOrWhiteSpace(record["syncedLyrics"]?.ToString()));

    private static LyricTrack? ParseRecord(JObject record)
    {
        string? synced = record["syncedLyrics"]?.ToString();
        if (string.IsNullOrWhiteSpace(synced))
            return null;

        var track = LrcParser.Parse(synced, ProviderName);
        return track.IsSynced ? track : null;
    }

    private async Task<string?> SendAsync(string url, CancellationToken ct)
    {
        using var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", _userAgent);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");

        using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
        _nextAllowedRequestUtc = DateTime.UtcNow + MinimumSpacing;

        if (response.StatusCode == HttpStatusCode.TooManyRequests ||
            response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            ApplyBackoff(response);
            return null;
        }

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    private void ApplyBackoff(HttpResponseMessage response)
    {
        TimeSpan wait = TimeSpan.FromSeconds(5);

        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
            wait = delta;
        else if (retryAfter?.Date is { } date && date > DateTimeOffset.UtcNow)
            wait = date - DateTimeOffset.UtcNow;

        _backoffUntilUtc = DateTime.UtcNow + wait;
        Logging.WriteInfo($"LRCLIB asked us to back off for {wait.TotalSeconds:F0}s ({(int)response.StatusCode}).");
    }

    private async Task RespectSpacingAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (now >= _nextAllowedRequestUtc)
            return;

        await Task.Delay(_nextAllowedRequestUtc - now, ct).ConfigureAwait(false);
    }
}
