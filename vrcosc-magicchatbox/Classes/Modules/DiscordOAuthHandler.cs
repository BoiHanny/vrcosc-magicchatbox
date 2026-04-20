using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Services;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Handles Discord OAuth2 implicit grant flow via local HTTP listeners.
/// The browser redirect extracts the access_token from the URL fragment
/// and POSTs it to the callback listener.
/// </summary>
public class DiscordOAuthHandler : IDisposable
{
    private bool _disposed;
    private readonly INavigationService _nav;
    private HttpListener? _redirectListener;
    private HttpListener? _callbackListener;
    private readonly object _listenerLock = new();

    public DiscordOAuthHandler(INavigationService nav)
    {
        _nav = nav;
    }

    /// <summary>
    /// Runs the full implicit grant OAuth flow:
    /// 1. Opens the Discord authorize URL in the user's browser
    /// 2. Captures the fragment-based access_token via local HTTP redirect
    /// Returns the access token string, or null on failure.
    /// </summary>
    public async Task<string?> AuthenticateAsync()
    {
        try
        {
            StartListeners();

            var authUrl = $"{Core.Constants.DiscordOAuthEndpoint}" +
                          $"?client_id={Core.Constants.DiscordClientId}" +
                          $"&response_type=token" +
                          $"&redirect_uri={Uri.EscapeDataString(Core.Constants.DiscordOAuthRedirectUri)}" +
                          $"&scope={Uri.EscapeDataString(Core.Constants.DiscordOAuthScope)}";

            _nav.OpenUrl(authUrl);

            // First listener: receives the browser redirect (fragment is client-side only)
            var context1 = await _redirectListener!.GetContextAsync();
            await SendFragmentExtractorPageAsync(context1.Response);

            // Second listener: receives the POSTed fragment data from the JS page
            var context2 = await _callbackListener!.GetContextAsync();
            string? fragmentData;
            using (var reader = new StreamReader(context2.Request.InputStream))
            {
                fragmentData = await reader.ReadToEndAsync();
            }

            // Send a "you can close this tab" response
            await SendClosePageAsync(context2.Response);

            if (string.IsNullOrWhiteSpace(fragmentData))
                return null;

            // Parse access_token from fragment query string
            var parsed = PulsoidOAuthHandler.ParseQueryString(fragmentData);
            return parsed.TryGetValue("access_token", out var token) ? token : null;
        }
        catch (Exception ex)
        {
            Logging.WriteException(new Exception("Discord authentication failed.", ex), MSGBox: true);
            return null;
        }
        finally
        {
            StopListeners();
        }
    }

    private async Task SendFragmentExtractorPageAsync(HttpListenerResponse response)
    {
        const string html = @"
<html>
<head>
    <script type='text/javascript'>
        var fragment = window.location.hash.substring(1);
        var xhttp = new XMLHttpRequest();
        xhttp.open('POST', 'http://localhost:7387/', true);
        xhttp.onload = function() { setTimeout(function(){ window.close(); }, 1000); };
        xhttp.send(fragment);
        document.body.innerHTML = '<h2>MagicChatbox</h2><p>Discord connected! This tab will close automatically.</p>';
    </script>
</head>
<body><p>Connecting to Discord...</p></body>
</html>";

        var buffer = Encoding.UTF8.GetBytes(html);
        response.ContentLength64 = buffer.Length;
        response.ContentType = "text/html";
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        response.OutputStream.Close();
    }

    private async Task SendClosePageAsync(HttpListenerResponse response)
    {
        var buffer = Encoding.UTF8.GetBytes("OK");
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        response.OutputStream.Close();
    }

    private void StartListeners()
    {
        lock (_listenerLock)
        {
            if (_redirectListener == null)
            {
                _redirectListener = new HttpListener { Prefixes = { Core.Constants.DiscordOAuthRedirectUri } };
                _redirectListener.Start();
            }

            if (_callbackListener == null)
            {
                _callbackListener = new HttpListener { Prefixes = { Core.Constants.DiscordOAuthCallbackUri } };
                _callbackListener.Start();
            }
        }
    }

    private void StopListeners()
    {
        lock (_listenerLock)
        {
            _redirectListener?.Stop();
            _redirectListener?.Close();
            _redirectListener = null;

            _callbackListener?.Stop();
            _callbackListener?.Close();
            _callbackListener = null;
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
                StopListeners();
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
