using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Services;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Handles Pulsoid OAuth token validation and browser-based authentication flow.
/// Implements IPulsoidTokenValidator for narrow injection into transport-layer code.
/// </summary>
public class PulsoidOAuthHandler : IDisposable, IPulsoidTokenValidator
{
    private bool disposed = false;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly INavigationService _nav;
    private HttpClient _httpClient;
    private HttpClient OAuthHttpClient => _httpClient ??= _httpClientFactory.CreateClient("Pulsoid");
    private HttpListener httpListener;
    private readonly object listenerLock = new object();
    private HttpListener secondListener;

    public PulsoidOAuthHandler(IHttpClientFactory httpClientFactory, INavigationService nav)
    {
        _httpClientFactory = httpClientFactory;
        _nav = nav;
    }

    private async Task SendBrowserCloseResponseAsync(HttpListenerResponse response)
    {
        const string responseString = @"
    <html>
        <head>
            <script type='text/javascript'>
                var fragment = window.location.hash.substring(1);
                var xhttp = new XMLHttpRequest();
                xhttp.open('POST', 'http://localhost:7385/', true);
                xhttp.send(fragment);

                window.location.replace('https://pulsoid.net/ui/integrations');
            </script>
        </head>
        <body></body>
    </html>";

        var buffer = Encoding.UTF8.GetBytes(responseString);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        response.OutputStream.Close();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                StopListeners();
            }
            disposed = true;
        }
    }

    public async Task<string> AuthenticateUserAsync(string authorizationEndpoint)
    {
        try
        {
            string token = null;

            if (httpListener == null || secondListener == null)
                throw new InvalidOperationException("Listeners are not started");

            _nav.OpenUrl(authorizationEndpoint);

            var context1 = await httpListener.GetContextAsync();
            await SendBrowserCloseResponseAsync(context1.Response);

            var context2 = await secondListener.GetContextAsync();
            using (var reader = new StreamReader(context2.Request.InputStream))
            {
                token = await reader.ReadToEndAsync();
            }

            return token;
        }
        catch (Exception ex)
        {
            Logging.WriteException(new Exception("Authentication failed.", ex), MSGBox: true);
            return null;
        }
        finally
        {
            StopListeners();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public static Dictionary<string, string> ParseQueryString(string queryString)
    {
        var nvc = HttpUtility.ParseQueryString(queryString);
        return nvc.AllKeys.ToDictionary(k => k, k => nvc[k]);
    }

    public void StartListeners()
    {
        lock (listenerLock)
        {
            if (httpListener == null)
            {
                httpListener = new HttpListener { Prefixes = { Core.Constants.PulsoidOAuthRedirectUri } };
                httpListener.Start();
            }

            if (secondListener == null)
            {
                try
                {
                    secondListener = new HttpListener { Prefixes = { Core.Constants.PulsoidOAuthCallbackUri } };
                    secondListener.Start();
                }
                catch
                {
                    // Clean up first listener if second fails to start
                    httpListener?.Stop();
                    httpListener?.Close();
                    throw;
                }
            }
        }
    }

    public void StopListeners()
    {
        lock (listenerLock)
        {
            httpListener?.Stop();
            httpListener?.Close();
            httpListener = null;

            secondListener?.Stop();
            secondListener?.Close();
            secondListener = null;
        }
    }

    public async Task<bool> ValidateTokenAsync(string accessToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return false;
            }

            using (var request = new HttpRequestMessage(HttpMethod.Get, Core.Constants.PulsoidTokenValidateUrl))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                var response = await OAuthHttpClient.SendAsync(request).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var tokenInfo = JsonConvert.DeserializeObject<TokenInfo>(content);

                    var requiredScopes = Core.Constants.PulsoidOAuthScope.Split(',');
                    if (tokenInfo?.Scopes == null)
                    {
                        Logging.WriteInfo("Token validation response missing scopes.");
                        return false;
                    }

                    return requiredScopes.All(scope => tokenInfo.Scopes.Contains(scope));
                }
                else
                {
                    if (response.StatusCode == HttpStatusCode.Unauthorized ||
                        response.StatusCode == HttpStatusCode.Forbidden ||
                        response.StatusCode == HttpStatusCode.BadRequest)
                    {
                        Logging.WriteInfo($"Token validation failed with status code {response.StatusCode}");
                        return false;
                    }

                    Logging.WriteInfo($"Token validation failed with status code {response.StatusCode}, treating as transient.");
                    return true;
                }
            }
        }
        catch (HttpRequestException ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return true;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return false;
        }
    }

    private class TokenInfo
    {
        [JsonProperty("scopes")]
        public string[] Scopes { get; set; }
    }
}
