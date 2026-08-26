using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Services;

namespace vrcosc_magicchatbox.Classes.Modules;

public class PulsoidOAuthHandler : IDisposable, IPulsoidTokenValidator
{
    private const int MaxCallbackBodyChars = 16 * 1024;
    private static readonly TimeSpan OAuthTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan CallbackReadTimeout = TimeSpan.FromSeconds(5);

    private bool disposed;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly INavigationService _nav;
    private HttpClient? _httpClient;
    private HttpClient OAuthHttpClient => _httpClient ??= _httpClientFactory.CreateClient("Pulsoid");
    private HttpListener? httpListener;
    private readonly object listenerLock = new object();
    private HttpListener? secondListener;

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
                xhttp.onloadend = function() {
                    window.location.replace('https://pulsoid.net/ui/integrations');
                };
                xhttp.send(fragment);
            </script>
        </head>
        <body></body>
    </html>";

        var buffer = Encoding.UTF8.GetBytes(responseString);
        response.ContentType = "text/html; charset=utf-8";
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

    public async Task<string?> AuthenticateUserAsync(string authorizationEndpoint, string expectedState)
    {
        if (string.IsNullOrWhiteSpace(expectedState))
            throw new ArgumentException("An OAuth state value is required.", nameof(expectedState));

        try
        {
            if (httpListener == null || secondListener == null)
                throw new InvalidOperationException("Listeners are not started");

            _nav.OpenUrl(authorizationEndpoint);

            var redirectTask = httpListener.GetContextAsync();
            var callbackTask = secondListener.GetContextAsync();
            var timeoutTask = Task.Delay(OAuthTimeout);

            while (true)
            {
                Task completed = await Task.WhenAny(redirectTask, callbackTask, timeoutTask);
                if (completed == timeoutTask)
                {
                    Logging.WriteInfo("Pulsoid OAuth timed out waiting for the browser callback.");
                    return null;
                }

                if (completed == redirectTask)
                {
                    HttpListenerContext redirect = await redirectTask;
                    redirectTask = httpListener.GetContextAsync();

                    if (!string.Equals(redirect.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
                    {
                        CompleteResponse(redirect.Response, HttpStatusCode.MethodNotAllowed, "GET");
                        continue;
                    }

                    try
                    {
                        await SendBrowserCloseResponseAsync(redirect.Response);
                    }
                    catch (Exception ex) when (ex is IOException or HttpListenerException)
                    {
                        Logging.WriteInfo($"Pulsoid OAuth bridge response failed: {ex.Message}");
                    }
                    continue;
                }

                HttpListenerContext callback = await callbackTask;
                callbackTask = secondListener.GetContextAsync();
                string? fragment = await ReadValidatedCallbackAsync(callback, expectedState);
                if (fragment != null)
                    return fragment;
            }
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

    private static async Task<string?> ReadValidatedCallbackAsync(
        HttpListenerContext context,
        string expectedState)
    {
        HttpListenerRequest request = context.Request;
        if (!string.Equals(request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            CompleteResponse(context.Response, HttpStatusCode.MethodNotAllowed, "POST");
            return null;
        }

        string? origin = request.Headers["Origin"];
        string expectedOrigin = new Uri(Core.Constants.PulsoidOAuthRedirectUri)
            .GetLeftPart(UriPartial.Authority);
        if (!string.IsNullOrWhiteSpace(origin) &&
            !string.Equals(origin, expectedOrigin, StringComparison.OrdinalIgnoreCase))
        {
            CompleteResponse(context.Response, HttpStatusCode.Forbidden);
            return null;
        }

        if (request.ContentLength64 > MaxCallbackBodyChars)
        {
            CompleteResponse(context.Response, HttpStatusCode.RequestEntityTooLarge);
            return null;
        }

        string? fragment;
        try
        {
            using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
            using var timeout = new CancellationTokenSource(CallbackReadTimeout);
            var buffer = new char[MaxCallbackBodyChars + 1];
            int total = 0;
            while (total < buffer.Length)
            {
                int read = await reader.ReadAsync(
                    buffer.AsMemory(total, buffer.Length - total),
                    timeout.Token);
                if (read == 0)
                    break;

                total += read;
            }

            fragment = total > MaxCallbackBodyChars
                ? null
                : new string(buffer, 0, total);
        }
        catch (OperationCanceledException)
        {
            CompleteResponse(context.Response, HttpStatusCode.RequestTimeout);
            return null;
        }
        catch (Exception ex) when (ex is IOException or HttpListenerException)
        {
            Logging.WriteInfo($"Pulsoid OAuth callback body could not be read: {ex.Message}");
            CompleteResponse(context.Response, HttpStatusCode.BadRequest);
            return null;
        }

        if (fragment == null)
        {
            CompleteResponse(context.Response, HttpStatusCode.RequestEntityTooLarge);
            return null;
        }

        if (!HasExpectedState(fragment, expectedState))
        {
            CompleteResponse(context.Response, HttpStatusCode.BadRequest);
            return null;
        }

        CompleteResponse(context.Response, HttpStatusCode.NoContent);
        return fragment;
    }

    private static void CompleteResponse(
        HttpListenerResponse response,
        HttpStatusCode statusCode,
        string? allowedMethod = null)
    {
        response.StatusCode = (int)statusCode;
        if (allowedMethod != null)
            response.Headers["Allow"] = allowedMethod;
        response.ContentLength64 = 0;
        response.Close();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public static Dictionary<string, string?> ParseQueryString(string queryString)
    {
        var nvc = HttpUtility.ParseQueryString(queryString);
        var result = new Dictionary<string, string?>();
        foreach (var key in nvc.AllKeys)
        {
            if (key == null)
                continue;

            result[key] = nvc[key];
        }
        return result;
    }

    public static bool HasExpectedState(string fragmentString, string expectedState)
    {
        if (string.IsNullOrWhiteSpace(fragmentString) || string.IsNullOrWhiteSpace(expectedState))
            return false;

        Dictionary<string, string?> fragment = ParseQueryString(fragmentString);
        return fragment.TryGetValue("state", out string? returnedState) &&
               string.Equals(returnedState, expectedState, StringComparison.Ordinal);
    }

    public void StartListeners()
    {
        lock (listenerLock)
        {
            if (httpListener != null && secondListener != null)
                return;

            HttpListener? first = null;
            HttpListener? second = null;
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

    private static void CloseListenerSafely(HttpListener? listener)
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
        public string[]? Scopes { get; set; }

        [JsonProperty("expires_in")]
        public long ExpiresIn { get; set; }
    }
}
