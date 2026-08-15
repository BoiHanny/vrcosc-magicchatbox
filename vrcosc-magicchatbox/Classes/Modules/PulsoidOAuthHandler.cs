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

            var redirectTask = httpListener.GetContextAsync();
            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(2));
            if (await Task.WhenAny(redirectTask, timeoutTask) == timeoutTask)
            {
                Logging.WriteInfo("Pulsoid OAuth timed out waiting for browser redirect.");
                StopListeners();
                return null;
            }

            var context1 = await redirectTask;
            await SendBrowserCloseResponseAsync(context1.Response);

            var callbackTask = secondListener.GetContextAsync();
            var callbackTimeoutTask = Task.Delay(TimeSpan.FromMinutes(2));
            if (await Task.WhenAny(callbackTask, callbackTimeoutTask) == callbackTimeoutTask)
            {
                Logging.WriteInfo("Pulsoid OAuth timed out waiting for token callback.");
                StopListeners();
                return null;
            }

            var context2 = await callbackTask;
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
            if (httpListener != null && secondListener != null)
                return;

            HttpListener first = null;
            HttpListener second = null;
            try
            {
                first = new HttpListener { Prefixes = { Core.Constants.PulsoidOAuthRedirectUri } };
                first.Start();

                second = new HttpListener { Prefixes = { Core.Constants.PulsoidOAuthCallbackUri } };
                second.Start();
            }
            catch
            {
                CloseListenerSafely(first);
                CloseListenerSafely(second);
                throw;
            }

            httpListener = first;
            secondListener = second;
        }
    }

    public void StopListeners()
    {
        lock (listenerLock)
        {
            CloseListenerSafely(httpListener);
            httpListener = null;

            CloseListenerSafely(secondListener);
            secondListener = null;
        }
    }

    private static void CloseListenerSafely(HttpListener listener)
    {
        if (listener == null)
            return;

        try
        {
            listener.Stop();
            listener.Close();
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"Pulsoid OAuth listener cleanup skipped: {ex.Message}");
        }
    }

    public async Task<PulsoidTokenValidation> ValidateTokenAsync(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return PulsoidTokenValidation.Invalid;
        }

        try
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, Core.Constants.PulsoidTokenValidateUrl))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                var response = await OAuthHttpClient.SendAsync(request).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var tokenInfo = JsonConvert.DeserializeObject<TokenInfo>(content);

                    if (tokenInfo?.Scopes == null)
                    {
                        Logging.WriteInfo("Pulsoid token validation returned 200 without a scopes array; treating as unverifiable.");
                        return PulsoidTokenValidation.Unknown;
                    }

                    if (!tokenInfo.Scopes.Contains(Core.Constants.PulsoidRequiredScope))
                    {
                        Logging.WriteInfo(
                            $"Pulsoid token is missing the required scope '{Core.Constants.PulsoidRequiredScope}' (granted: {string.Join(", ", tokenInfo.Scopes)}).");
                        return PulsoidTokenValidation.Invalid;
                    }

                    if (!tokenInfo.Scopes.Contains(Core.Constants.PulsoidStatisticsScope))
                        Logging.WriteInfo("Pulsoid token has no statistics scope; heart rate works, statistics will not.");

                    if (tokenInfo.ExpiresIn > 0)
                        Logging.WriteInfo($"Pulsoid token validated, expires in {TimeSpan.FromSeconds(tokenInfo.ExpiresIn):d\\.hh\\:mm\\:ss}.");

                    return PulsoidTokenValidation.Valid;
                }

                string body = await ReadBodySafelyAsync(response).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    Logging.WriteInfo($"Pulsoid rejected the token (401). {body}");
                    return PulsoidTokenValidation.Invalid;
                }

                Logging.WriteInfo($"Pulsoid token validation could not complete (HTTP {(int)response.StatusCode}). Keeping the saved sign-in. {body}");
                return PulsoidTokenValidation.Unknown;
            }
        }
        catch (OperationCanceledException ex)
        {
            Logging.WriteInfo($"Pulsoid token validation timed out: {ex.Message}");
            return PulsoidTokenValidation.Unknown;
        }
        catch (HttpRequestException ex)
        {
            Logging.WriteInfo($"Pulsoid token validation could not reach the server: {ex.Message}");
            return PulsoidTokenValidation.Unknown;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return PulsoidTokenValidation.Unknown;
        }
    }

    private static async Task<string> ReadBodySafelyAsync(HttpResponseMessage response)
    {
        try
        {
            string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(content) ? string.Empty : content.Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    private class TokenInfo
    {
        [JsonProperty("scopes")]
        public string[] Scopes { get; set; }

        [JsonProperty("expires_in")]
        public long ExpiresIn { get; set; }
    }
}
