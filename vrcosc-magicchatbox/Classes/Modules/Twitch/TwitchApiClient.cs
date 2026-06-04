using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TwitchLib.Api;
using vrcosc_magicchatbox.Classes.DataAndSecurity;

namespace vrcosc_magicchatbox.Classes.Modules.Twitch;

/// <summary>
/// Handles all raw Twitch Helix API communication.
/// Stateless regarding UI — only manages API session (credentials, cached broadcaster ID).
/// </summary>
public sealed class TwitchApiClient : ITwitchApiClient
{
    private readonly TwitchAPI _api = new();
    private readonly IHttpClientFactory _httpClientFactory;

    private HttpClient _helixClient;
    private HttpClient HelixClient => _helixClient ??= _httpClientFactory.CreateClient("Twitch");

    private string _configuredClientId = string.Empty;
    private string _configuredAccessToken = string.Empty;

    public TwitchApiClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public void Configure(string clientId, string accessToken)
    {
        string cid = clientId?.Trim() ?? string.Empty;
        string tok = accessToken?.Trim() ?? string.Empty;

        if (!string.Equals(_api.Settings.ClientId, cid, StringComparison.Ordinal))
            _api.Settings.ClientId = cid;

        if (!string.Equals(_api.Settings.AccessToken, tok, StringComparison.Ordinal))
            _api.Settings.AccessToken = tok;

        _configuredClientId = cid;
        _configuredAccessToken = tok;
    }

    public async Task<TwitchTokenValidation> ValidateTokenAsync(string accessToken)
    {
        try
        {
            var validation = await _api.Auth.ValidateAccessTokenAsync(accessToken).ConfigureAwait(false);
            if (validation == null)
                return new TwitchTokenValidation(false, string.Empty, string.Empty, string.Empty, Array.Empty<string>());

            IReadOnlyList<string> scopes = (validation.Scopes ?? new List<string>()).AsReadOnly();
            return new TwitchTokenValidation(
                true,
                validation.UserId ?? string.Empty,
                validation.Login ?? string.Empty,
                validation.ClientId ?? string.Empty,
                scopes);
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"Twitch token validation failed: {ex.Message}");
            return new TwitchTokenValidation(false, string.Empty, string.Empty, string.Empty, Array.Empty<string>());
        }
    }

    public async Task<string> GetBroadcasterIdAsync(string channelLogin)
    {
        if (string.IsNullOrWhiteSpace(channelLogin))
            return string.Empty;

        var response = await _api.Helix.Users.GetUsersAsync(logins: new List<string> { channelLogin }).ConfigureAwait(false);
        return response?.Users?.FirstOrDefault()?.Id ?? string.Empty;
    }

    public async Task<TwitchStreamSnapshot> GetStreamInfoAsync(string broadcasterId)
    {
        if (string.IsNullOrWhiteSpace(broadcasterId))
            return new TwitchStreamSnapshot(false, 0, string.Empty, string.Empty);

        var response = await _api.Helix.Streams.GetStreamsAsync(userIds: new List<string> { broadcasterId }).ConfigureAwait(false);
        var stream = response?.Streams?.FirstOrDefault();

        if (stream == null)
            return new TwitchStreamSnapshot(false, 0, string.Empty, string.Empty);

        return new TwitchStreamSnapshot(true, stream.ViewerCount, stream.GameName ?? string.Empty, stream.Title ?? string.Empty);
    }

    public async Task<TwitchFollowerResult> GetFollowerCountAsync(string broadcasterId, string moderatorId)
    {
        if (string.IsNullOrWhiteSpace(broadcasterId))
            return new TwitchFollowerResult(false, 0, false, false, "No broadcaster ID");

        string moderatorParam = string.IsNullOrWhiteSpace(moderatorId) ? string.Empty : $"&moderator_id={moderatorId}";
        using var request = CreateHelixRequest(HttpMethod.Get, $"channels/followers?broadcaster_id={broadcasterId}{moderatorParam}");
        using var response = await HelixClient.SendAsync(request).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return new TwitchFollowerResult(false, 0, true, false, "Unauthorized");

        if (response.StatusCode == HttpStatusCode.Forbidden)
            return new TwitchFollowerResult(false, 0, false, true, "Missing moderator:read:followers scope");

        if (!response.IsSuccessStatusCode)
            return new TwitchFollowerResult(false, 0, false, false, $"Followers request failed ({(int)response.StatusCode})");

        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

        if (doc.RootElement.TryGetProperty("total", out var totalElement) && totalElement.TryGetInt32(out var total))
            return new TwitchFollowerResult(true, total, false, false, string.Empty);

        return new TwitchFollowerResult(false, 0, false, false, "Followers total missing");
    }

    public async Task<TwitchActionResult> SendAnnouncementAsync(
        string broadcasterId,
        string moderatorId,
        string message,
        string color)
    {
        string trimmed = message?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
            return new TwitchActionResult(false, "Announcement message is empty.");

        var payload = new { message = trimmed, color };
        using var request = CreateHelixRequest(
            HttpMethod.Post,
            $"chat/announcements?broadcaster_id={broadcasterId}&moderator_id={moderatorId}");
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await HelixClient.SendAsync(request).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return new TwitchActionResult(false, "Token invalid.");

        if (response.StatusCode == HttpStatusCode.Forbidden)
            return new TwitchActionResult(false, "Missing announcement permission or scope.");

        if ((int)response.StatusCode == 429)
            return new TwitchActionResult(false, "Rate limited. Try again soon.");

        if (!response.IsSuccessStatusCode)
            return new TwitchActionResult(false, $"Announcement failed ({(int)response.StatusCode}).");

        return new TwitchActionResult(true, "Announcement sent!");
    }

    public async Task<TwitchActionResult> SendShoutoutAsync(
        string fromBroadcasterId,
        string toBroadcasterId,
        string moderatorId)
    {
        using var request = CreateHelixRequest(
            HttpMethod.Post,
            $"chat/shoutouts?from_broadcaster_id={Uri.EscapeDataString(fromBroadcasterId)}&to_broadcaster_id={Uri.EscapeDataString(toBroadcasterId)}&moderator_id={Uri.EscapeDataString(moderatorId)}");

        using var response = await HelixClient.SendAsync(request).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return new TwitchActionResult(false, "Token invalid.");

        if (response.StatusCode == HttpStatusCode.Forbidden)
            return new TwitchActionResult(false, "Missing shoutout permission or scope.");

        if ((int)response.StatusCode == 429)
            return new TwitchActionResult(false, "Rate limited. Try again soon.");

        if (!response.IsSuccessStatusCode)
            return new TwitchActionResult(false, $"Shoutout failed ({(int)response.StatusCode}).");

        return new TwitchActionResult(true, "Shoutout sent!");
    }

    public async Task<string> ResolveUserIdAsync(string login)
    {
        string normalized = NormalizeLogin(login);
        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        var response = await _api.Helix.Users.GetUsersAsync(logins: new List<string> { normalized }).ConfigureAwait(false);
        return response?.Users?.FirstOrDefault()?.Id ?? string.Empty;
    }

    private HttpRequestMessage CreateHelixRequest(HttpMethod method, string relativeUrl)
    {
        var request = new HttpRequestMessage(method, relativeUrl);
        request.Headers.Add("Client-Id", _api.Settings.ClientId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _api.Settings.AccessToken);
        return request;
    }

    private static string NormalizeLogin(string login)
    {
        if (string.IsNullOrWhiteSpace(login))
            return string.Empty;

        return login.Trim().TrimStart('@');
    }
}
