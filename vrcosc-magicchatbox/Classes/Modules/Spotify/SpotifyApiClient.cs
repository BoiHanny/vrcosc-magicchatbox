using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Core;

namespace vrcosc_magicchatbox.Classes.Modules.Spotify;

public sealed class SpotifyApiClient : ISpotifyApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;

    public SpotifyApiClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<SpotifyApiResult<SpotifyProfileSnapshot>> GetProfileAsync(string accessToken)
    {
        using var response = await SendAsync(HttpMethod.Get, "me", accessToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return Failure<SpotifyProfileSnapshot>(response, "Spotify profile request failed.");

        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
        var root = doc.RootElement;
        return Success(response, new SpotifyProfileSnapshot(
            GetString(root, "id"),
            GetString(root, "display_name")));
    }

    public async Task<SpotifyApiResult<SpotifyPlaybackSnapshot>> GetPlaybackAsync(string accessToken)
    {
        using var response = await SendAsync(HttpMethod.Get, "me/player", accessToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NoContent)
            return Success(response, new SpotifyPlaybackSnapshot(false, false, 0, false, "off", string.Empty, 0, null));

        if (!response.IsSuccessStatusCode)
            return Failure<SpotifyPlaybackSnapshot>(response, "Spotify playback request failed.");

        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
        var root = doc.RootElement;
        bool isPlaying = GetBool(root, "is_playing");
        int progressMs = GetInt(root, "progress_ms");
        bool shuffle = GetBool(root, "shuffle_state");
        string repeat = GetString(root, "repeat_state");
        string deviceName = string.Empty;
        int volume = 0;

        if (root.TryGetProperty("device", out var device) && device.ValueKind == JsonValueKind.Object)
        {
            deviceName = GetString(device, "name");
            volume = GetInt(device, "volume_percent");
        }

        SpotifyTrackSnapshot? track = null;
        if (root.TryGetProperty("item", out var item) && item.ValueKind == JsonValueKind.Object)
            track = ParseTrack(item);

        return Success(response, new SpotifyPlaybackSnapshot(
            track != null,
            isPlaying,
            progressMs,
            shuffle,
            string.IsNullOrWhiteSpace(repeat) ? "off" : repeat,
            deviceName,
            volume,
            track));
    }

    public async Task<SpotifyApiResult<bool>> IsTrackSavedAsync(string accessToken, string trackId)
    {
        if (string.IsNullOrWhiteSpace(trackId))
            return Success(HttpStatusCode.OK, false);

        using var response = await SendAsync(HttpMethod.Get, $"me/tracks/contains?ids={Uri.EscapeDataString(trackId)}", accessToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return Failure<bool>(response, "Spotify liked-track request failed.");

        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
        bool liked = doc.RootElement.ValueKind == JsonValueKind.Array
                     && doc.RootElement.GetArrayLength() > 0
                     && doc.RootElement[0].ValueKind == JsonValueKind.True;
        return Success(response, liked);
    }

    public async Task<SpotifyApiResult<SpotifyQueueSnapshot>> GetQueueAsync(string accessToken)
    {
        using var response = await SendAsync(HttpMethod.Get, "me/player/queue", accessToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return Failure<SpotifyQueueSnapshot>(response, "Spotify queue request failed.");

        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
        var tracks = new List<string>();
        if (doc.RootElement.TryGetProperty("queue", out var queue) && queue.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in queue.EnumerateArray().Take(3))
            {
                var track = ParseTrack(item);
                if (track != null)
                    tracks.Add($"{track.Artist} - {track.Title}");
            }
        }

        return Success(response, new SpotifyQueueSnapshot(tracks));
    }

    public Task<SpotifyApiResult<bool>> PlayAsync(string accessToken)
        => SendControlAsync(HttpMethod.Put, "me/player/play", accessToken);

    public Task<SpotifyApiResult<bool>> PauseAsync(string accessToken)
        => SendControlAsync(HttpMethod.Put, "me/player/pause", accessToken);

    public Task<SpotifyApiResult<bool>> NextAsync(string accessToken)
        => SendControlAsync(HttpMethod.Post, "me/player/next", accessToken);

    public Task<SpotifyApiResult<bool>> PreviousAsync(string accessToken)
        => SendControlAsync(HttpMethod.Post, "me/player/previous", accessToken);

    public Task<SpotifyApiResult<bool>> SetShuffleAsync(string accessToken, bool state)
        => SendControlAsync(HttpMethod.Put, $"me/player/shuffle?state={state.ToString().ToLowerInvariant()}", accessToken);

    public Task<SpotifyApiResult<bool>> SetRepeatAsync(string accessToken, string state)
        => SendControlAsync(HttpMethod.Put, $"me/player/repeat?state={Uri.EscapeDataString(state)}", accessToken);

    public Task<SpotifyApiResult<bool>> SetVolumeAsync(string accessToken, int volumePercent)
        => SendControlAsync(HttpMethod.Put, $"me/player/volume?volume_percent={Math.Clamp(volumePercent, 0, 100)}", accessToken);

    public Task<SpotifyApiResult<bool>> SaveTrackAsync(string accessToken, string trackId)
        => SendControlAsync(HttpMethod.Put, $"me/tracks?ids={Uri.EscapeDataString(trackId)}", accessToken);

    public Task<SpotifyApiResult<bool>> RemoveTrackAsync(string accessToken, string trackId)
        => SendControlAsync(HttpMethod.Delete, $"me/tracks?ids={Uri.EscapeDataString(trackId)}", accessToken);

    private async Task<SpotifyApiResult<bool>> SendControlAsync(HttpMethod method, string relativeUrl, string accessToken)
    {
        using var response = await SendAsync(method, relativeUrl, accessToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return Failure<bool>(response, BuildControlMessage(response.StatusCode));

        return Success(response, true);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativeUrl, string accessToken)
    {
        var client = _httpClientFactory.CreateClient(Constants.HttpClients.Spotify);
        var request = new HttpRequestMessage(method, relativeUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request).ConfigureAwait(false);
    }

    private static SpotifyTrackSnapshot? ParseTrack(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
            return null;

        string type = GetString(item, "type");
        if (!string.IsNullOrWhiteSpace(type) && !string.Equals(type, "track", StringComparison.OrdinalIgnoreCase))
            return null;

        string id = GetString(item, "id");
        if (string.IsNullOrWhiteSpace(id))
            return null;

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

    private static SpotifyApiResult<T> Failure<T>(HttpResponseMessage response, string message)
        => new(
            false,
            default,
            response.StatusCode,
            message,
            response.StatusCode == HttpStatusCode.Unauthorized,
            response.StatusCode == HttpStatusCode.Forbidden,
            response.StatusCode == HttpStatusCode.TooManyRequests);

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
}
