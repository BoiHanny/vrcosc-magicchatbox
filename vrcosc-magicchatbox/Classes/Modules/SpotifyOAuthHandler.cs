using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules.Spotify;
using vrcosc_magicchatbox.Core;
using vrcosc_magicchatbox.Services;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Spotify OAuth2 Authorization Code + PKCE flow for desktop clients.
/// No client secret is used.
/// </summary>
public sealed class SpotifyOAuthHandler : IDisposable
{
    private readonly INavigationService _nav;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly object _listenerLock = new();
    private HttpListener? _redirectListener;
    private bool _disposed;

    public SpotifyOAuthHandler(INavigationService nav, IHttpClientFactory httpClientFactory)
    {
        _nav = nav;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<SpotifyTokenResult?> AuthenticateAsync(string clientId)
    {
        string normalizedClientId = clientId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedClientId))
            throw new InvalidOperationException("Spotify Client ID is required before authentication.");

        try
        {
            string verifier = GenerateCodeVerifier();
            string challenge = GenerateCodeChallenge(verifier);
            string state = GenerateCodeVerifier();

            StartListener();
            string authUrl = $"{Constants.SpotifyOAuthEndpoint}" +
                             $"?client_id={Uri.EscapeDataString(normalizedClientId)}" +
                             $"&response_type=code" +
                             $"&redirect_uri={Uri.EscapeDataString(Constants.SpotifyOAuthRedirectUri)}" +
                             $"&scope={Uri.EscapeDataString(Constants.SpotifyScopes)}" +
                             $"&code_challenge={challenge}" +
                             $"&code_challenge_method=S256" +
                             $"&state={Uri.EscapeDataString(state)}";

            _nav.OpenUrl(authUrl);

            var listenerTask = _redirectListener!.GetContextAsync();
            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(2));
            var completed = await Task.WhenAny(listenerTask, timeoutTask).ConfigureAwait(false);

            if (completed == timeoutTask)
            {
                Logging.WriteInfo("Spotify OAuth timed out waiting for browser redirect.");
                return null;
            }

            var context = await listenerTask.ConfigureAwait(false);
            var query = HttpUtility.ParseQueryString(context.Request.Url?.Query ?? string.Empty);

            if (!string.Equals(state, query["state"], StringComparison.Ordinal))
            {
                Logging.WriteInfo("Spotify OAuth state mismatch.");
                await SendHtmlResponseAsync(context.Response,
                    "<h2>MagicChatbox</h2><p>Spotify authorization failed security validation. Please try again.</p>").ConfigureAwait(false);
                return null;
            }

            string error = query["error"] ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(error))
            {
                await SendHtmlResponseAsync(context.Response,
                    $"<h2>MagicChatbox</h2><p>Spotify authorization failed: {WebUtility.HtmlEncode(error)}</p><p>You can close this tab.</p>").ConfigureAwait(false);
                return null;
            }

            string code = query["code"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(code))
            {
                await SendHtmlResponseAsync(context.Response,
                    "<h2>MagicChatbox</h2><p>No Spotify authorization code received. Please try again.</p>").ConfigureAwait(false);
                return null;
            }

            var token = await ExchangeCodeAsync(normalizedClientId, code, verifier).ConfigureAwait(false);
            if (token == null)
            {
                await SendHtmlResponseAsync(context.Response,
                    "<h2>MagicChatbox</h2><p>Spotify token exchange failed. Check your Client ID and redirect URI.</p><p>You can close this tab.</p>").ConfigureAwait(false);
                return null;
            }

            await SendHtmlResponseAsync(context.Response,
                "<h2>MagicChatbox</h2><p>Spotify connected! This tab will close automatically.</p><script>setTimeout(function(){ window.close(); }, 2000);</script>").ConfigureAwait(false);
            return token;
        }
        catch (ObjectDisposedException)
        {
            Logging.WriteInfo("Spotify OAuth listener was stopped.");
            return null;
        }
        catch (HttpListenerException)
        {
            Logging.WriteInfo("Spotify OAuth listener error.");
            return null;
        }
        catch (Exception ex)
        {
            Logging.WriteException(new Exception("Spotify authentication failed.", ex), MSGBox: false);
            return null;
        }
        finally
        {
            StopListener();
        }
    }

    public async Task<SpotifyTokenResult?> RefreshTokenAsync(string clientId, string refreshToken)
    {
        string normalizedClientId = clientId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedClientId) || string.IsNullOrWhiteSpace(refreshToken))
            return null;

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = normalizedClientId
        });

        using var client = _httpClientFactory.CreateClient();
        using var response = await client.PostAsync(Constants.SpotifyTokenEndpoint, content).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            Logging.WriteInfo($"Spotify token refresh failed ({response.StatusCode}).");
            return null;
        }

        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return ParseTokenResponse(body);
    }

    private async Task<SpotifyTokenResult?> ExchangeCodeAsync(string clientId, string code, string verifier)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = Constants.SpotifyOAuthRedirectUri,
            ["client_id"] = clientId,
            ["code_verifier"] = verifier
        });

        using var client = _httpClientFactory.CreateClient();
        using var response = await client.PostAsync(Constants.SpotifyTokenEndpoint, content).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            Logging.WriteInfo($"Spotify token exchange failed ({response.StatusCode}).");
            return null;
        }

        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return ParseTokenResponse(body);
    }

    private static SpotifyTokenResult? ParseTokenResponse(string body)
    {
        JObject json;
        try
        {
            json = JObject.Parse(body);
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"Spotify token response was malformed JSON: {ex.Message}");
            return null;
        }

        string accessToken = json["access_token"]?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            Logging.WriteInfo("Spotify token response did not contain an access token.");
            return null;
        }

        return new SpotifyTokenResult(
            accessToken,
            json["refresh_token"]?.ToString(),
            json["expires_in"]?.Value<int>() ?? 0,
            json["scope"]?.ToString());
    }

    private async Task SendHtmlResponseAsync(HttpListenerResponse response, string bodyHtml)
    {
        string html = $"<html><body>{bodyHtml}</body></html>";
        byte[] buffer = Encoding.UTF8.GetBytes(html);
        response.ContentLength64 = buffer.Length;
        response.ContentType = "text/html";
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
        response.OutputStream.Close();
    }

    private void StartListener()
    {
        lock (_listenerLock)
        {
            if (_redirectListener == null)
            {
                _redirectListener = new HttpListener { Prefixes = { Constants.SpotifyOAuthRedirectUri } };
                _redirectListener.Start();
            }
        }
    }

    private void StopListener()
    {
        lock (_listenerLock)
        {
            _redirectListener?.Stop();
            _redirectListener?.Close();
            _redirectListener = null;
        }
    }

    private static string GenerateCodeVerifier()
    {
        byte[] bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        using var sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] input) =>
        Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    public void Dispose()
    {
        if (_disposed)
            return;

        StopListener();
        _disposed = true;
    }
}
