using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core;

namespace vrcosc_magicchatbox.Classes.Modules.Spotify;

public sealed class SpotifyApiClient : ISpotifyApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;

    public SpotifyApiClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<SpotifyApiResult<SpotifyProfileSnapshot>> GetProfileAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, "me", accessToken, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return await FailureAsync<SpotifyProfileSnapshot>(response, "Spotify profile request failed.").ConfigureAwait(false);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = doc.RootElement;
        return Success(response, new SpotifyProfileSnapshot(
            GetString(root, "id"),
            GetString(root, "display_name")));
    }

    public async Task<SpotifyApiResult<SpotifyPlaybackSnapshot>> GetPlaybackAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, "me/player?additional_types=track,episode", accessToken, cancellationToken).ConfigureAwait(false);
        DateTime capturedAtUtc = DateTime.UtcNow;
        if (response.StatusCode == HttpStatusCode.NoContent)
            return Success(response, new SpotifyPlaybackSnapshot(false, false, 0, capturedAtUtc, false, "off", string.Empty, false, 0, null));

        if (!response.IsSuccessStatusCode)
            return await FailureAsync<SpotifyPlaybackSnapshot>(response, "Spotify playback request failed.").ConfigureAwait(false);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = doc.RootElement;
        bool isPlaying = GetBool(root, "is_playing");
        int progressMs = GetInt(root, "progress_ms");
        bool shuffle = GetBool(root, "shuffle_state");
        string repeat = GetString(root, "repeat_state");
        string deviceName = string.Empty;
        int volume = 0;
        bool hasVolume = false;

        if (root.TryGetProperty("device", out var device) && device.ValueKind == JsonValueKind.Object)
        {
            deviceName = GetString(device, "name");
            int? volumePercent = GetNullableInt(device, "volume_percent");
            if (volumePercent.HasValue)
            {
                hasVolume = true;
                volume = Math.Clamp(volumePercent.Value, 0, 100);
            }
        }

        SpotifyTrackSnapshot? track = null;
        if (root.TryGetProperty("item", out var item) && item.ValueKind == JsonValueKind.Object)
            track = ParsePlaybackItem(item);

        return Success(response, new SpotifyPlaybackSnapshot(
            track != null,
            isPlaying,
            progressMs,
            capturedAtUtc,
            shuffle,
            string.IsNullOrWhiteSpace(repeat) ? "off" : repeat,
            deviceName,
            hasVolume,
            volume,
            track));
    }

    public async Task<SpotifyApiResult<bool>> IsTrackSavedAsync(string accessToken, string trackId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(trackId))
            return Success(HttpStatusCode.OK, false);

        using var response = await SendAsync(HttpMethod.Get, $"me/tracks/contains?ids={Uri.EscapeDataString(trackId)}", accessToken, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return await FailureAsync<bool>(response, "Spotify liked-track request failed.").ConfigureAwait(false);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        bool liked = doc.RootElement.ValueKind == JsonValueKind.Array
                     && doc.RootElement.GetArrayLength() > 0
                     && doc.RootElement[0].ValueKind == JsonValueKind.True;
        return Success(response, liked);
    }

    public async Task<SpotifyApiResult<SpotifyQueueSnapshot>> GetQueueAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, "me/player/queue", accessToken, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return await FailureAsync<SpotifyQueueSnapshot>(response, "Spotify queue request failed.").ConfigureAwait(false);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var tracks = new List<string>();
        if (doc.RootElement.TryGetProperty("queue", out var queue) && queue.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in queue.EnumerateArray().Take(3))
            {
                var track = ParsePlaybackItem(item);
                if (track != null)
                    tracks.Add($"{track.Artist} - {track.Title}");
            }
        }

        return Success(response, new SpotifyQueueSnapshot(tracks));
    }

    public Task<SpotifyApiResult<bool>> PlayAsync(string accessToken, CancellationToken cancellationToken = default)
        => SendControlAsync(HttpMethod.Put, "me/player/play", accessToken, cancellationToken);

    public Task<SpotifyApiResult<bool>> PauseAsync(string accessToken, CancellationToken cancellationToken = default)
        => SendControlAsync(HttpMethod.Put, "me/player/pause", accessToken, cancellationToken);

    public Task<SpotifyApiResult<bool>> NextAsync(string accessToken, CancellationToken cancellationToken = default)
        => SendControlAsync(HttpMethod.Post, "me/player/next", accessToken, cancellationToken);

    public Task<SpotifyApiResult<bool>> PreviousAsync(string accessToken, CancellationToken cancellationToken = default)
        => SendControlAsync(HttpMethod.Post, "me/player/previous", accessToken, cancellationToken);

    public Task<SpotifyApiResult<bool>> SetShuffleAsync(string accessToken, bool state, CancellationToken cancellationToken = default)
        => SendControlAsync(HttpMethod.Put, $"me/player/shuffle?state={state.ToString().ToLowerInvariant()}", accessToken, cancellationToken);

    public Task<SpotifyApiResult<bool>> SetRepeatAsync(string accessToken, string state, CancellationToken cancellationToken = default)
        => SendControlAsync(HttpMethod.Put, $"me/player/repeat?state={Uri.EscapeDataString(state)}", accessToken, cancellationToken);

    public Task<SpotifyApiResult<bool>> SetVolumeAsync(string accessToken, int volumePercent, CancellationToken cancellationToken = default)
        => SendControlAsync(HttpMethod.Put, $"me/player/volume?volume_percent={Math.Clamp(volumePercent, 0, 100)}", accessToken, cancellationToken);

    public Task<SpotifyApiResult<bool>> SaveTrackAsync(string accessToken, string trackId, CancellationToken cancellationToken = default)
        => SendControlAsync(HttpMethod.Put, $"me/tracks?ids={Uri.EscapeDataString(trackId)}", accessToken, cancellationToken);

    public Task<SpotifyApiResult<bool>> RemoveTrackAsync(string accessToken, string trackId, CancellationToken cancellationToken = default)
        => SendControlAsync(HttpMethod.Delete, $"me/tracks?ids={Uri.EscapeDataString(trackId)}", accessToken, cancellationToken);

    private async Task<SpotifyApiResult<bool>> SendControlAsync(
        HttpMethod method,
        string relativeUrl,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(method, relativeUrl, accessToken, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return await FailureAsync<bool>(response, BuildControlMessage(response.StatusCode)).ConfigureAwait(false);

        return Success(response, true);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string relativeUrl,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(Constants.HttpClients.Spotify);
        var request = new HttpRequestMessage(method, relativeUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static SpotifyTrackSnapshot? ParsePlaybackItem(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
            return null;

        string type = GetString(item, "type");
        if (string.Equals(type, "episode", StringComparison.OrdinalIgnoreCase))
            return ParseEpisode(item);

        if (!string.IsNullOrWhiteSpace(type) && !string.Equals(type, "track", StringComparison.OrdinalIgnoreCase))
            return null;

        string id = GetString(item, "id");
        string artist = string.Empty;
        if (item.TryGetProperty("artists", out var artists) && artists.ValueKind == JsonValueKind.Array)
        {
            artist = string.Join(", ", artists.EnumerateArray()
                .Select(a => GetString(a, "name"))
                .Where(name => !string.IsNullOrWhiteSpace(name)));
        }

        string album = string.Empty;
        if (item.TryGetProperty("album", out var albumElement) && albumElement.ValueKind == JsonValueKind.Object)
            album = GetString(albumElement, "name");

        string externalUrl = string.Empty;
        if (item.TryGetProperty("external_urls", out var urls) && urls.ValueKind == JsonValueKind.Object)
            externalUrl = GetString(urls, "spotify");

        return new SpotifyTrackSnapshot(
            id,
            GetString(item, "uri"),
            externalUrl,
            GetString(item, "name"),
            artist,
            album,
            GetBool(item, "explicit"),
            GetInt(item, "duration_ms"));
    }

    private static SpotifyTrackSnapshot? ParseEpisode(JsonElement item)
    {
        string id = GetString(item, "id");
        string show = string.Empty;
        if (item.TryGetProperty("show", out var showElement) && showElement.ValueKind == JsonValueKind.Object)
            show = GetString(showElement, "name");

        string externalUrl = string.Empty;
        if (item.TryGetProperty("external_urls", out var urls) && urls.ValueKind == JsonValueKind.Object)
            externalUrl = GetString(urls, "spotify");

        return new SpotifyTrackSnapshot(
            id,
            GetString(item, "uri"),
            externalUrl,
            GetString(item, "name"),
            show,
            show,
            GetBool(item, "explicit"),
            GetInt(item, "duration_ms"));
    }

    private static string BuildControlMessage(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Forbidden => "Spotify rejected the control. Spotify Premium or an active device may be required.",
        HttpStatusCode.NotFound => "No active Spotify device found.",
        HttpStatusCode.TooManyRequests => "Spotify rate limited this action. Try again soon.",
        HttpStatusCode.Unauthorized => "Spotify token expired. Reconnect Spotify.",
        _ => $"Spotify control failed ({(int)statusCode})."
    };

    private static SpotifyApiResult<T> Success<T>(HttpResponseMessage response, T value)
        => Success(response.StatusCode, value);

    private static SpotifyApiResult<T> Success<T>(HttpStatusCode statusCode, T value)
        => new(true, value, statusCode, string.Empty);

    private static async Task<SpotifyApiResult<T>> FailureAsync<T>(HttpResponseMessage response, string fallbackMessage)
    {
        var error = await ReadSpotifyErrorAsync(response).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(error.Message))
            Logging.WriteInfo($"Spotify API failure ({(int)response.StatusCode}) reason='{error.Reason}': {error.Message}");

        return new(
            false,
            default,
            response.StatusCode,
            BuildFailureMessage(response.StatusCode, fallbackMessage, error.Reason),
            response.StatusCode == HttpStatusCode.Unauthorized,
            response.StatusCode == HttpStatusCode.Forbidden,
            response.StatusCode == HttpStatusCode.TooManyRequests,
            GetRetryAfter(response),
            IsTransient(response.StatusCode),
            error.Reason);
    }

    private static string BuildFailureMessage(HttpStatusCode statusCode, string fallbackMessage, string reason)
    {
        // Spotify 403 commonly returns a "reason" field. Surface known codes to the user.
        if (statusCode == HttpStatusCode.Forbidden && !string.IsNullOrWhiteSpace(reason))
        {
            string? friendly = reason.ToUpperInvariant() switch
            {
                "NO_ACTIVE_DEVICE" => "No active Spotify device. Open Spotify on a device first.",
                "PREMIUM_REQUIRED" => "This action requires Spotify Premium.",
                "DEVICE_NOT_CONTROLLABLE" => "This Spotify device cannot be controlled remotely.",
                "VOLUME_CONTROL_DISALLOW" => "This Spotify device does not allow volume control.",
                "REMOTE_CONTROL_DISALLOW" => "Spotify remote control is disabled on this device.",
                "ALREADY_PAUSED" => "Spotify is already paused.",
                "NOT_PAUSED" => "Spotify is already playing.",
                "NOT_PLAYING_LOCALLY" or "NOT_PLAYING_TRACK" or "NOT_PLAYING_CONTEXT" => "Nothing is playing on Spotify.",
                "NO_PREV_TRACK" => "No previous track in Spotify queue.",
                "NO_NEXT_TRACK" => "No next track in Spotify queue.",
                "CONTEXT_DISALLOW" => "Spotify disallows this action in the current context.",
                "ENDLESS_CONTEXT" => "Spotify endless context — action not allowed.",
                "RATE_LIMITED" => "Spotify rate limited this action. Try again soon.",
                _ => null
            };
            if (friendly != null)
                return friendly;
        }

        return statusCode switch
        {
            HttpStatusCode.Unauthorized => "Spotify authorization expired. Refreshing connection...",
            HttpStatusCode.Forbidden => "Spotify rejected this request. Premium, an active device, or an extra permission may be required.",
            HttpStatusCode.NotFound => "No active Spotify device found.",
            HttpStatusCode.TooManyRequests => "Spotify rate limited MagicChatbox. Sync will pause briefly.",
            HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout => "Spotify is temporarily unavailable. Sync will retry shortly.",
            _ => fallbackMessage
        };
    }

    private static bool IsTransient(HttpStatusCode statusCode)
        => statusCode is HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

    private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter == null)
            return null;

        if (retryAfter.Delta.HasValue)
            return retryAfter.Delta.Value;

        if (retryAfter.Date.HasValue)
            return retryAfter.Date.Value - DateTimeOffset.UtcNow;

        return null;
    }

    private static async Task<(string Message, string Reason)> ReadSpotifyErrorAsync(HttpResponseMessage response)
    {
        try
        {
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(body))
                return (string.Empty, string.Empty);

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                string message = error.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String
                    ? m.GetString() ?? string.Empty
                    : string.Empty;
                string reason = error.TryGetProperty("reason", out var r) && r.ValueKind == JsonValueKind.String
                    ? r.GetString() ?? string.Empty
                    : string.Empty;
                return (message, reason);
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or HttpRequestException)
        {
            Logging.WriteInfo($"Spotify error body could not be parsed: {ex.Message}");
        }

        return (string.Empty, string.Empty);
    }

    private static string GetString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString() ?? string.Empty
            : string.Empty;

    private static bool GetBool(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.True;

    private static int GetInt(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out int value)
            ? value
            : 0;

    private static int? GetNullableInt(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out int value)
            ? value
            : null;
}
