using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;

namespace vrcosc_magicchatbox.Services;

public sealed class PulsoidApiClient : IPulsoidClient
{
    private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(30);
    private const int MinRetryDelayMs = 2_000;
    private const int MaxRetryDelayMs = 10_000;
    private const int MaxAttemptsBeforeNotice = 10;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IPulsoidTokenValidator _tokenValidator;
    private readonly Random _jitter = new();
    private ClientWebSocket? _webSocket;
    private HttpClient? _statsClient;
    private bool _disposed;

    private HttpClient StatsClient => _statsClient ??= _httpClientFactory.CreateClient("Pulsoid");

    public event Action<int>? HeartRateReceived;
    public event Action<PulsoidConnectionError, string>? ConnectionFailed;
    public event Action<bool>? ConnectionStateChanged;

    public bool IsConnected => _webSocket?.State == WebSocketState.Open;

    public PulsoidApiClient(IHttpClientFactory httpClientFactory, IPulsoidTokenValidator tokenValidator)
    {
        _httpClientFactory = httpClientFactory;
        _tokenValidator = tokenValidator;
    }

    public async Task ConnectAsync(string accessToken, CancellationToken ct)
    {
        int attempt = 0;
        bool exhaustedReported = false;

        while (!ct.IsCancellationRequested)
        {
            bool handshakeSucceeded = false;
            bool stop = false;

            try
            {
                var socket = new ClientWebSocket();
                _webSocket = socket;
                socket.Options.KeepAliveInterval = KeepAliveInterval;
                socket.Options.CollectHttpResponseDetails = true;

                var wsUri = new Uri(
                    $"wss://dev.pulsoid.net/api/v1/data/real_time?access_token={Uri.EscapeDataString(accessToken)}");
                await socket.ConnectAsync(wsUri, ct).ConfigureAwait(false);

                handshakeSucceeded = true;
                attempt = 0;
                exhaustedReported = false;
                ConnectionStateChanged?.Invoke(true);

                await ReceiveLoopAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                stop = true;
            }
            catch (WebSocketException ex)
            {
                var status = TryGetHandshakeStatus();
                Logging.WriteInfo($"Pulsoid WebSocket failure (HTTP {(int)status}): {ex.Message}");
                if (!handshakeSucceeded)
                    stop = await ReportIfAuthRejectionAsync(status, accessToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
                ConnectionFailed?.Invoke(PulsoidConnectionError.UnexpectedError, ex.Message);
            }
            finally
            {
                if (handshakeSucceeded)
                    ConnectionStateChanged?.Invoke(false);
                DisposeWebSocket();
            }

            if (stop || ct.IsCancellationRequested)
                return;

            attempt++;
            if (attempt >= MaxAttemptsBeforeNotice && !exhaustedReported)
            {
                exhaustedReported = true;
                ConnectionFailed?.Invoke(PulsoidConnectionError.MaxRetriesExhausted,
                    "Can't reach Pulsoid right now — your sign-in is kept and reconnection keeps retrying.");
            }

            int delayMs = Math.Min(MaxRetryDelayMs, MinRetryDelayMs * (int)Math.Pow(2, Math.Min(attempt, 4)));
            delayMs += _jitter.Next(-delayMs / 5, delayMs / 5 + 1);
            Logging.WriteInfo($"Retrying Pulsoid connection in {delayMs}ms (attempt {attempt}).");

            try
            {
                await Task.Delay(delayMs, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private HttpStatusCode TryGetHandshakeStatus()
    {
        try
        {
            return _webSocket?.HttpStatusCode ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    private async Task<bool> ReportIfAuthRejectionAsync(HttpStatusCode status, string accessToken)
    {
        switch ((int)status)
        {
            case 401:
                ConnectionFailed?.Invoke(PulsoidConnectionError.TokenInvalid,
                    "Pulsoid rejected the saved token. Please reconnect.");
                return true;

            case 402:
                ConnectionFailed?.Invoke(PulsoidConnectionError.SubscriptionRequired,
                    "Pulsoid reports that this feature needs a paid plan, so reconnecting has stopped.");
                return true;

            case 0:
                var validation = await _tokenValidator.ValidateTokenAsync(accessToken).ConfigureAwait(false);
                if (validation == PulsoidTokenValidation.Invalid)
                {
                    ConnectionFailed?.Invoke(PulsoidConnectionError.TokenInvalid,
                        "Pulsoid rejected the saved token. Please reconnect.");
                    return true;
                }
                return false;

            default:
                return false;
        }
    }

    public async Task DisconnectAsync()
    {
        if (_webSocket is { State: WebSocketState.Open } socket)
        {
            try
            {
                await socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"WebSocket close error (non-fatal): {ex.Message}");
            }
        }

        DisposeWebSocket();
        ConnectionStateChanged?.Invoke(false);
    }

    public async Task<PulsoidStatisticsResponse?> FetchStatisticsAsync(string accessToken, string timeRange)
    {
        try
        {
            string requestUri = $"{Core.Constants.PulsoidApiBaseUrl}statistics?time_range={timeRange}";

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Add("User-Agent", "Vrcosc-MagicChatbox");
            request.Headers.Add("Accept", "application/json");

            var response = await StatsClient.SendAsync(request).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Logging.WriteInfo($"Error fetching Pulsoid statistics: {response.StatusCode}, Content: {errorContent}");

                switch (response.StatusCode)
                {
                    case HttpStatusCode.Unauthorized:
                    case HttpStatusCode.Forbidden:
                        ConnectionFailed?.Invoke(PulsoidConnectionError.StatisticsUnavailable,
                            "Pulsoid would not return heart-rate statistics for this token. Heart rate itself is unaffected.");
                        break;
                    case HttpStatusCode.PaymentRequired:
                        ConnectionFailed?.Invoke(PulsoidConnectionError.StatisticsUnavailable,
                            "Pulsoid statistics need a paid plan. Heart rate itself is unaffected.");
                        break;
                }

                return null;
            }

            string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonConvert.DeserializeObject<PulsoidStatisticsResponse>(content);
        }
        catch (HttpRequestException ex)
        {
            Logging.WriteInfo($"Pulsoid statistics HTTP error: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DisposeWebSocket();
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[1024];

        while (_webSocket is { State: WebSocketState.Open } socket &&
               !ct.IsCancellationRequested)
        {
            WebSocketReceiveResult result;
            using var messageStream = new MemoryStream();
            try
            {
                do
                {
                    result = await socket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure, "Closing", ct).ConfigureAwait(false);
                        return;
                    }

                    if (result.Count > 0)
                        messageStream.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (WebSocketException wex)
            {
                Logging.WriteInfo($"Pulsoid WebSocket dropped during receive: {wex.Message}");
                return;
            }
            catch (IOException ioex)
            {
                Logging.WriteInfo($"Pulsoid IO error during receive: {ioex.Message}");
                return;
            }

            if (result.MessageType != WebSocketMessageType.Text)
                continue;

            string message = Encoding.UTF8.GetString(messageStream.ToArray());
            if (!string.IsNullOrWhiteSpace(message))
            {
                int hr = ParseHeartRate(message);
                if (hr >= 0)
                    HeartRateReceived?.Invoke(hr);
            }
        }
    }

    private static int ParseHeartRate(string message)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(message))
                return -1;

            var trimmed = message.Trim();
            if (int.TryParse(trimmed, out var plainHr))
                return plainHr;

            var json = JObject.Parse(message);
            var hrToken = json.SelectToken("data.heart_rate");
            if (hrToken == null || hrToken.Type == JTokenType.Null)
                return -1;

            return hrToken.Value<int?>() ?? -1;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return -1;
        }
    }

    private void DisposeWebSocket()
    {
        try { _webSocket?.Dispose(); }
        catch (Exception ex) { Logging.WriteInfo($"WebSocket dispose error (non-fatal): {ex.Message}"); }
        _webSocket = null;
    }
}
