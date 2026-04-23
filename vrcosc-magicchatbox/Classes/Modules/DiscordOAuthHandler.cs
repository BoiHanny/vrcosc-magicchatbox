using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json.Linq;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Services;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Result of a successful Discord OAuth2 authorization code exchange.
/// </summary>
public sealed record DiscordTokenResult(
    string AccessToken,
    string? RefreshToken,
    int ExpiresIn);

/// <summary>
/// Handles Discord OAuth2 Authorization Code + PKCE flow via local HTTP listener.
/// Uses PKCE (Proof Key for Code Exchange) so no client_secret is needed — safe for
/// public/open-source desktop clients.
/// </summary>
public class DiscordOAuthHandler : IDisposable
{
    private bool _disposed;
    private readonly INavigationService _nav;
    private readonly IHttpClientFactory _httpClientFactory;
    private HttpListener? _redirectListener;
    private readonly object _listenerLock = new();

    public DiscordOAuthHandler(INavigationService nav, IHttpClientFactory httpClientFactory)
    {
        _nav = nav;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Runs the full Authorization Code + PKCE OAuth flow:
    /// 1. Opens the Discord authorize URL in the user's browser with PKCE challenge
    /// 2. Captures the authorization code from the redirect
    /// 3. Exchanges the code for tokens using the PKCE verifier
    /// Returns a token result, or null on failure.
    /// </summary>
    public async Task<DiscordTokenResult?> AuthenticateAsync()
    {
        try
        {
            // Generate PKCE pair
            var codeVerifier = GenerateCodeVerifier();
            var codeChallenge = GenerateCodeChallenge(codeVerifier);

            StartListener();

            var authUrl = $"{Core.Constants.DiscordOAuthEndpoint}" +
                          $"?client_id={Core.Constants.DiscordClientId}" +
                          $"&response_type=code" +
                          $"&redirect_uri={Uri.EscapeDataString(Core.Constants.DiscordOAuthRedirectUri)}" +
                          $"&scope={Uri.EscapeDataString(Core.Constants.DiscordOAuthScope)}" +
                          $"&code_challenge={codeChallenge}" +
                          $"&code_challenge_method=S256";

            _nav.OpenUrl(authUrl);

            // Capture the redirect — code arrives as a query parameter
            var context = await _redirectListener!.GetContextAsync();
            var queryString = context.Request.Url?.Query;
            var queryParams = HttpUtility.ParseQueryString(queryString ?? string.Empty);

            var code = queryParams["code"];
            var error = queryParams["error"];

            if (!string.IsNullOrEmpty(error))
            {
                var errorDesc = queryParams["error_description"] ?? error;
                Logging.WriteInfo($"Discord OAuth error: {error} — {errorDesc}");
                await SendHtmlResponseAsync(context.Response,
                    "<h2>MagicChatbox</h2>" +
                    $"<p>Discord authorization failed: {WebUtility.HtmlEncode(errorDesc)}</p>" +
                    "<p>You can close this tab.</p>");
                return null;
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                Logging.WriteInfo("Discord OAuth: no code received in redirect.");
                await SendHtmlResponseAsync(context.Response,
                    "<h2>MagicChatbox</h2><p>No authorization code received. Please try again.</p>");
                return null;
            }

            // Show a nice "working on it" page
            await SendHtmlResponseAsync(context.Response,
                "<h2>MagicChatbox</h2><p>Discord connected! This tab will close automatically.</p>" +
                "<script>setTimeout(function(){ window.close(); }, 2000);</script>");

            // Exchange the authorization code for tokens using PKCE
            return await ExchangeCodeAsync(code, codeVerifier);
        }
        catch (Exception ex)
        {
            Logging.WriteException(new Exception("Discord authentication failed.", ex), MSGBox: true);
            return null;
        }
        finally
        {
            StopListener();
        }
    }

    /// <summary>
    /// Refreshes an access token using a refresh token.
    /// Returns a new token result, or null on failure.
    /// </summary>
    public async Task<DiscordTokenResult?> RefreshTokenAsync(string refreshToken)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = Core.Constants.DiscordClientId,
            });

            var response = await client.PostAsync(Core.Constants.DiscordTokenEndpoint, content);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Logging.WriteInfo($"Discord token refresh failed ({response.StatusCode}): {body}");
                return null;
            }

            var json = JObject.Parse(body);
            return new DiscordTokenResult(
                json["access_token"]?.ToString() ?? string.Empty,
                json["refresh_token"]?.ToString(),
                json["expires_in"]?.Value<int>() ?? 0);
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"Discord token refresh exception: {ex.Message}");
            return null;
        }
    }

    private async Task<DiscordTokenResult?> ExchangeCodeAsync(string code, string codeVerifier)
    {
        using var client = _httpClientFactory.CreateClient();
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = Core.Constants.DiscordOAuthRedirectUri,
            ["client_id"] = Core.Constants.DiscordClientId,
            ["code_verifier"] = codeVerifier,
        });

        var response = await client.PostAsync(Core.Constants.DiscordTokenEndpoint, content);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Logging.WriteInfo($"Discord token exchange failed ({response.StatusCode}): {body}");
            return null;
        }

        var json = JObject.Parse(body);
        var accessToken = json["access_token"]?.ToString();

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            Logging.WriteInfo("Discord token exchange: no access_token in response.");
            return null;
        }

        return new DiscordTokenResult(
            accessToken,
            json["refresh_token"]?.ToString(),
            json["expires_in"]?.Value<int>() ?? 0);
    }

    private async Task SendHtmlResponseAsync(HttpListenerResponse response, string bodyHtml)
    {
        var html = $"<html><body>{bodyHtml}</body></html>";
        var buffer = Encoding.UTF8.GetBytes(html);
        response.ContentLength64 = buffer.Length;
        response.ContentType = "text/html";
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        response.OutputStream.Close();
    }

    private void StartListener()
    {
        lock (_listenerLock)
        {
            if (_redirectListener == null)
            {
                _redirectListener = new HttpListener { Prefixes = { Core.Constants.DiscordOAuthRedirectUri } };
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

    // --- PKCE helpers ---

    private static string GenerateCodeVerifier()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] input) =>
        Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
                StopListener();
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
