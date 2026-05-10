using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Services;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Result of a successful Discord OAuth2 authorization.
/// </summary>
public sealed record DiscordTokenResult(
    string AccessToken,
    string? RefreshToken,
    int ExpiresIn,
    string? Scope);

/// <summary>
/// Handles Discord's legacy implicit OAuth flow used by the local RPC voice integration.
/// </summary>
public sealed class DiscordOAuthHandler : IDisposable
{
    private sealed record DiscordOAuthAttemptResult(
        DiscordTokenResult? Result,
        bool HasRpcScope,
        string? Error,
        string? ErrorDescription);

    private readonly INavigationService _nav;
    private readonly object _listenerLock = new();
    private HttpListener? _activeListener;
    private bool _disposed;

    public DiscordOAuthHandler(INavigationService nav)
    {
        _nav = nav;
    }

    /// <summary>
    /// Implicit grant OAuth flow (response_type=token) for Discord.
    /// Discord may reject rpc scopes for apps/accounts without approval, so the flow
    /// retries with public identify scope to avoid leaving users stuck on invalid_scope.
    /// The token is returned in the URL fragment, so a small JS bridge page extracts it
    /// and POSTs it back to our listener. No refresh token is available with this flow.
    /// </summary>
    public async Task<(DiscordTokenResult? Result, bool HasRpcScope)> AuthenticateImplicitAsync(string? clientId = null)
    {
        if (!TryNormalizeClientId(clientId, out clientId, out string validationMessage))
        {
            Logging.WriteInfo($"Discord implicit OAuth: invalid application ID. {validationMessage}");
            return (null, false);
        }

        var voiceAttempt = await AuthenticateImplicitScopeAsync(
            clientId,
            Core.Constants.DiscordVoiceOAuthScope,
            "voice/RPC",
            validationMessage).ConfigureAwait(false);

        if (voiceAttempt.Result != null || !IsInvalidScopeError(voiceAttempt.Error, voiceAttempt.ErrorDescription))
            return (voiceAttempt.Result, voiceAttempt.HasRpcScope);

        Logging.WriteInfo("Discord implicit OAuth: Discord rejected rpc scope. Retrying with identify-only scope.");

        var basicAttempt = await AuthenticateImplicitScopeAsync(
            clientId,
            Core.Constants.DiscordBasicOAuthScope,
            "basic identify",
            validationMessage).ConfigureAwait(false);

        return (basicAttempt.Result, basicAttempt.HasRpcScope);
    }

    private async Task<DiscordOAuthAttemptResult> AuthenticateImplicitScopeAsync(
        string clientId,
        string scope,
        string scopeLabel,
        string validationMessage)
    {
        StopListener();

        HttpListener? listener = null;
        try
        {
            var state = GenerateStateToken();

            listener = new HttpListener();
            listener.Prefixes.Add(Core.Constants.DiscordOAuthRedirectUri);
            SetActiveListener(listener);
            listener.Start();

            var authUrl = $"{Core.Constants.DiscordOAuthEndpoint}" +
                           $"?client_id={Uri.EscapeDataString(clientId)}" +
                           $"&response_type=token" +
                           $"&redirect_uri={Uri.EscapeDataString(Core.Constants.DiscordOAuthRedirectUri)}" +
                           $"&scope={Uri.EscapeDataString(scope)}" +
                           $"&state={Uri.EscapeDataString(state)}";

            Logging.WriteInfo($"Discord implicit OAuth: opening browser for {scopeLabel} scope. {validationMessage}");
            _nav.OpenUrl(authUrl);

            var timeout = Task.Delay(TimeSpan.FromMinutes(2));

            var ctxTask = listener.GetContextAsync();
            var completed = await Task.WhenAny(ctxTask, timeout).ConfigureAwait(false);
            if (completed == timeout)
            {
                Logging.WriteInfo($"Discord implicit OAuth: timed out waiting for browser redirect ({scopeLabel}).");
                return new DiscordOAuthAttemptResult(null, false, "timeout", null);
            }

            var ctx = await ctxTask.ConfigureAwait(false);
            await SendHtmlResponseAsync(ctx.Response, BuildFragmentBridgePage()).ConfigureAwait(false);

            while (true)
            {
                ctxTask = listener.GetContextAsync();
                completed = await Task.WhenAny(ctxTask, timeout).ConfigureAwait(false);
                if (completed == timeout)
                {
                    Logging.WriteInfo($"Discord implicit OAuth: timed out waiting for fragment callback ({scopeLabel}).");
                    return new DiscordOAuthAttemptResult(null, false, "timeout", null);
                }

                var cbCtx = await ctxTask.ConfigureAwait(false);

                if (cbCtx.Request.HttpMethod != "POST")
                {
                    cbCtx.Response.StatusCode = 204;
                    cbCtx.Response.Close();
                    continue;
                }

                string body;
                using (var reader = new StreamReader(cbCtx.Request.InputStream, Encoding.UTF8))
                    body = await reader.ReadToEndAsync().ConfigureAwait(false);

                var p = HttpUtility.ParseQueryString(body);

                var returnedState = p["state"];
                if (!string.Equals(state, returnedState, StringComparison.Ordinal))
                {
                    Logging.WriteInfo("Discord implicit OAuth: state mismatch on fragment callback.");
                    await SendHtmlResponseAsync(cbCtx.Response,
                        "<h2>MagicChatbox</h2><p>Authorization failed: security validation error.</p>").ConfigureAwait(false);
                    return new DiscordOAuthAttemptResult(null, false, "state_mismatch", null);
                }

                var error = p["error"];
                if (!string.IsNullOrEmpty(error))
                {
                    var desc = p["error_description"] ?? error;
                    Logging.WriteInfo($"Discord implicit OAuth error ({scopeLabel}): {error} - {desc}");
                    string html = IsInvalidScopeError(error, desc) && HasRpcScope(scope)
                        ? BuildInvalidScopeRetryPage(desc)
                        : BuildAuthorizationFailedPage(desc);

                    await SendHtmlResponseAsync(cbCtx.Response, html).ConfigureAwait(false);
                    return new DiscordOAuthAttemptResult(null, false, error, desc);
                }

                var accessToken = p["access_token"];
                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    Logging.WriteInfo("Discord implicit OAuth: no access_token in fragment.");
                    await SendHtmlResponseAsync(cbCtx.Response,
                        "<h2>MagicChatbox</h2><p>No access token received. Please try again.</p>").ConfigureAwait(false);
                    return new DiscordOAuthAttemptResult(null, false, "missing_access_token", null);
                }

                int.TryParse(p["expires_in"], out var expiresIn);
                var grantedScope = p["scope"];
                bool hasRpc = HasRpcScope(grantedScope);

                Logging.WriteInfo($"Discord implicit OAuth: success. scope={grantedScope}, hasRpc={hasRpc}");

                await SendHtmlResponseAsync(cbCtx.Response,
                    "<h2>MagicChatbox</h2><p>Discord connected! This tab will close automatically.</p>" +
                    "<script>setTimeout(function(){ window.close(); }, 2000);</script>").ConfigureAwait(false);

                return new DiscordOAuthAttemptResult(new DiscordTokenResult(accessToken, null, expiresIn, grantedScope), hasRpc, null, null);
            }
        }
        catch (ObjectDisposedException)
        {
            Logging.WriteInfo("Discord implicit OAuth: listener was stopped.");
            return new DiscordOAuthAttemptResult(null, false, "listener_stopped", null);
        }
        catch (HttpListenerException)
        {
            Logging.WriteInfo("Discord implicit OAuth: listener error.");
            return new DiscordOAuthAttemptResult(null, false, "listener_error", null);
        }
        catch (Exception ex)
        {
            Logging.WriteException(new Exception("Discord implicit authentication failed.", ex), MSGBox: false);
            return new DiscordOAuthAttemptResult(null, false, "exception", ex.Message);
        }
        finally
        {
            ClearActiveListener(listener);
            try
            {
                listener?.Stop();
                listener?.Close();
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"Discord implicit OAuth: listener cleanup failed: {ex.Message}");
            }
        }
    }

    public static bool TryNormalizeClientId(string? clientId, out string normalizedClientId, out string status)
    {
        normalizedClientId = string.IsNullOrWhiteSpace(clientId)
            ? Core.Constants.DiscordClientId
            : clientId.Trim();

        if (!normalizedClientId.All(char.IsDigit))
        {
            status = "Application ID must contain only digits. Copy the Application ID from Discord Developer Portal, not the Client Secret.";
            return false;
        }

        if (normalizedClientId.Length < 17 || normalizedClientId.Length > 20)
        {
            status = "Application ID should be a Discord snowflake, usually 17-20 digits.";
            return false;
        }

        status = string.Equals(normalizedClientId, Core.Constants.DiscordClientId, StringComparison.Ordinal)
            ? "Using the bundled MagicChatbox Application ID. If Discord rejects the private voice/RPC scope, MagicChatbox will fall back to basic Discord auth."
            : "Application ID looks valid. Discord may still reject private voice/RPC scope for unapproved apps.";
        return true;
    }

    public static bool IsRedirectPortAvailable(out string status)
    {
        if (!Uri.TryCreate(Core.Constants.DiscordOAuthRedirectUri, UriKind.Absolute, out var redirectUri)
            || redirectUri.Port <= 0)
        {
            status = $"Could not parse redirect URI: {Core.Constants.DiscordOAuthRedirectUri}";
            return false;
        }

        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, redirectUri.Port);
            listener.Server.ExclusiveAddressUse = true;
            listener.Start();
            status = $"Ready: {Core.Constants.DiscordOAuthRedirectUri} is free.";
            return true;
        }
        catch (SocketException ex)
        {
            status = $"Blocked: port {redirectUri.Port} is already in use. Close the other app or change it before connecting.";
            Logging.WriteInfo($"Discord OAuth redirect port unavailable: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            status = $"Could not check redirect port {redirectUri.Port}: {ex.Message}";
            Logging.WriteInfo($"Discord OAuth redirect port check failed: {ex.Message}");
            return false;
        }
    }

    private static bool HasRpcScope(string? scope)
        => !string.IsNullOrWhiteSpace(scope)
           && scope.Split(' ', StringSplitOptions.RemoveEmptyEntries)
               .Any(part => part is "rpc" or "rpc.voice.read" or "rpc.voice.channel.read");

    private static bool IsInvalidScopeError(string? error, string? description)
    {
        if (string.Equals(error, "invalid_scope", StringComparison.OrdinalIgnoreCase))
            return true;

        string text = $"{error} {description}";
        return text.Contains("scope", StringComparison.OrdinalIgnoreCase)
               && (text.Contains("invalid", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("unknown", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("malformed", StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildInvalidScopeRetryPage(string description) =>
        "<h2>MagicChatbox</h2>" +
        "<p>Discord rejected the private voice/RPC permission: " +
        WebUtility.HtmlEncode(description) +
        "</p><p>MagicChatbox is retrying with basic Discord authorization. " +
        "A second Discord tab should open. Voice/speaker detection may be unavailable, but connection and Rich Presence can still work.</p>";

    private static string BuildAuthorizationFailedPage(string description) =>
        $"<h2>MagicChatbox</h2><p>Discord authorization failed: {WebUtility.HtmlEncode(description)}</p>";

    private static string BuildFragmentBridgePage() =>
        """
        <html><body>
        <h2>MagicChatbox</h2>
        <p id="s">Connecting to Discord...</p>
        <script>
        (function(){
            var h = window.location.hash.substring(1);
            if (!h) { document.getElementById('s').textContent = 'No token received. You can close this tab.'; return; }
            fetch('/callback', { method:'POST', body:h, headers:{'Content-Type':'application/x-www-form-urlencoded'} })
            .then(function(r){ return r.text(); })
            .then(function(t){ document.body.innerHTML = t; })
            .catch(function(){ document.getElementById('s').textContent = 'Connection error. Please try again.'; });
        })();
        </script>
        </body></html>
        """;

    private static async Task SendHtmlResponseAsync(HttpListenerResponse response, string bodyHtml)
    {
        var html = bodyHtml.Contains("<html", StringComparison.OrdinalIgnoreCase)
            ? bodyHtml
            : $"<html><body>{bodyHtml}</body></html>";
        var buffer = Encoding.UTF8.GetBytes(html);
        response.ContentLength64 = buffer.Length;
        response.ContentType = "text/html";
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
        response.OutputStream.Close();
    }

    private static string GenerateStateToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private void SetActiveListener(HttpListener listener)
    {
        lock (_listenerLock)
        {
            _activeListener = listener;
        }
    }

    private void ClearActiveListener(HttpListener? listener)
    {
        lock (_listenerLock)
        {
            if (ReferenceEquals(_activeListener, listener))
                _activeListener = null;
        }
    }

    private void StopListener()
    {
        lock (_listenerLock)
        {
            _activeListener?.Stop();
            _activeListener?.Close();
            _activeListener = null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        StopListener();
        _disposed = true;
    }
}
