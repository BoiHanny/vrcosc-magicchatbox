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

/// <summary>One attempt in the lookup ladder, and how much slack its results have earned.</summary>
public readonly record struct LyricsLookupStep(string Url, bool RequiresCloseDuration);

public sealed class LrcLibLyricsProvider : ILyricsProvider
{
    public const string BaseUrl = "https://lrclib.net";
    public const string ProviderName = "LRCLIB";

    private static readonly TimeSpan MinimumSpacing = TimeSpan.FromMilliseconds(350);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _userAgent;
    private readonly Func<LyricsSettings?> _settings;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private DateTime _nextAllowedRequestUtc = DateTime.MinValue;
    private DateTime _backoffUntilUtc = DateTime.MinValue;

    public LrcLibLyricsProvider(
        IHttpClientFactory httpClientFactory,
        string appVersion,
        Func<LyricsSettings?>? settings = null)
    {
        _httpClientFactory = httpClientFactory;
        _userAgent = $"MagicChatBox/{appVersion} (https://github.com/BoiHanny/vrcosc-magicchatbox)";
        _settings = settings ?? (() => null);
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

            var settings = _settings();
            var strictness = settings?.MatchStrictness ?? LyricsMatchStrictness.Balanced;
            bool broaden = settings?.BroadenSearchWhenNoMatch ?? true;

            foreach (LyricsLookupStep step in BuildLookupSteps(query, broaden))
            {
                if (DateTime.UtcNow < _backoffUntilUtc)
                    return null;

                await RespectSpacingAsync(ct).ConfigureAwait(false);

                string? body = await SendAsync(step.Url, ct).ConfigureAwait(false);
                if (body == null)
                    continue;

                var options = LyricsMatchOptions.For(strictness, step.RequiresCloseDuration);
                var track = ParseResponse(body, query, options);
                if (track != null)
                    return track;
            }

            return null;
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

    /// <summary>
    /// Every lookup worth trying, most specific first, stopping as soon as one answers. The later
    /// rungs give up detail - the version, then all but the lead artist - because the plain and the
    /// versioned spelling of one recording can be separate entries with only one carrying synced
    /// lyrics. Once detail is gone the running time has to agree closely.
    /// </summary>
    public static IReadOnlyList<LyricsLookupStep> BuildLookupSteps(LyricsQuery query, bool allowBroadening = true)
    {
        var steps = new List<LyricsLookupStep>();

        void Add(string url, bool requiresCloseDuration)
        {
            if (!steps.Any(s => string.Equals(s.Url, url, StringComparison.Ordinal)))
                steps.Add(new LyricsLookupStep(url, requiresCloseDuration));
        }

        string title = query.Title;
        string artist = query.Artist;
        string baseTitle = TitleQualifier.BaseTitle(title);
        string primaryArtist = TitleQualifier.PrimaryArtist(artist);

        // Full title: it still identifies the recording, so the usual tolerance holds.
        Add(Direct(title, artist, query.Album, query.Duration), false);
        if (query.Duration > TimeSpan.Zero)
            Add(Direct(title, artist, query.Album, TimeSpan.Zero), false);

        Add(Structured(title, artist), false);
        Add(Keyword($"{artist} {title}"), false);

        if (!allowBroadening)
            return steps;

        // Detail dropped from here on, so the length has to carry the proof.
        if (baseTitle.Length > 0 && !string.Equals(baseTitle, title, StringComparison.OrdinalIgnoreCase))
        {
            Add(Structured(baseTitle, artist), true);
            Add(Keyword($"{artist} {baseTitle}"), true);
        }

        if (primaryArtist.Length > 0 && !string.Equals(primaryArtist, artist, StringComparison.OrdinalIgnoreCase))
            Add(Keyword($"{primaryArtist} {(baseTitle.Length > 0 ? baseTitle : title)}"), true);

        return steps;
    }

    private static string Direct(string title, string artist, string album, TimeSpan duration)
    {
        string url = $"{BaseUrl}/api/get"
                   + $"?track_name={Uri.EscapeDataString(title)}"
                   + $"&artist_name={Uri.EscapeDataString(artist)}";

        if (album.Length > 0)
            url += $"&album_name={Uri.EscapeDataString(album)}";

        if (duration > TimeSpan.Zero)
            url += $"&duration={(int)Math.Round(duration.TotalSeconds)}";

        return url;
    }

    private static string Structured(string title, string artist)
        => $"{BaseUrl}/api/search"
         + $"?track_name={Uri.EscapeDataString(title)}"
         + $"&artist_name={Uri.EscapeDataString(artist)}";

    private static string Keyword(string q)
        => $"{BaseUrl}/api/search?q={Uri.EscapeDataString(q)}";

    /// <summary>
    /// Handles both shapes the API returns. A single record is scored rather than trusted: asking
    /// without a duration lets the server pick the version, and the wrong one is worse than none.
    /// </summary>
    public static LyricTrack? ParseResponse(string body, LyricsQuery query, LyricsMatchOptions options = default)
    {
        JToken token = JToken.Parse(body);

        var records = token switch
        {
            JArray array => array.OfType<JObject>().ToList(),
            JObject record => [record],
            _ => [],
        };

        var best = PickBest(records, query, options);
        return best == null ? null : ParseRecord(best);
    }

    public static JObject? PickBest(
        IReadOnlyList<JObject> records,
        LyricsQuery query,
        LyricsMatchOptions options = default)
    {
        if (records.Count == 0)
            return null;

        var candidates = records.Select(ToCandidate).ToList();
        var match = LyricsMatchScorer.PickBest(candidates, query, options);

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
