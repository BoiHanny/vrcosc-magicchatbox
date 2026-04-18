using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Pure network client for the Pulsoid API.
/// Handles WebSocket streaming (with exponential-backoff reconnection) and REST statistics.
/// All events fire on background threads — callers must marshal to UI if needed.
/// </summary>
public sealed class PulsoidApiClient : IPulsoidClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IPulsoidTokenValidator _tokenValidator;
    private ClientWebSocket _webSocket;
    private HttpClient _statsClient;
    private bool _disposed;

    private HttpClient StatsClient => _statsClient ??= _httpClientFactory.CreateClient("Pulsoid");

    public event Action<int> HeartRateReceived;
    public event Action<PulsoidConnectionError, string> ConnectionFailed;
    public event Action<bool> ConnectionStateChanged;

    public bool IsConnected => _webSocket?.State == WebSocketState.Open;

    public PulsoidApiClient(IHttpClientFactory httpClientFactory, IPulsoidTokenValidator tokenValidator)
    {
        _httpClientFactory = httpClientFactory;
        _tokenValidator = tokenValidator;
    }

    public async Task ConnectAsync(string accessToken, CancellationToken ct)
    {
        int attempt = 0;
        const int maxAttempts = 10;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                _webSocket = new ClientWebSocket();
                _webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(5);

                var wsUri = new Uri(
                    $"wss://dev.pulsoid.net/api/v1/data/real_time?access_token={Uri.EscapeDataString(accessToken)}");
                await _webSocket.ConnectAsync(wsUri, ct).ConfigureAwait(false);

                ConnectionStateChanged?.Invoke(true);

                // Receive loop — blocks until connection drops or is cancelled
                await ReceiveLoopAsync(accessToken, ct).ConfigureAwait(false);
                break; // Clean exit from receive loop
            }
            catch (OperationCanceledException)
            {
                break; // Cancellation requested
            }
            catch (WebSocketException ex)
            {
                attempt++;
                Logging.WriteInfo($"WebSocket connection attempt {attempt} failed: {ex.Message}");
                DisposeWebSocket();

                bool tokenValid = await _tokenValidator.ValidateTokenAsync(accessToken).ConfigureAwait(false);
                if (!tokenValid)
                {
                    ConnectionFailed?.Invoke(PulsoidConnectionError.TokenInvalid,
                        "Access token invalid or revoked. Please reconnect.");
                    return;
                }

                if (attempt >= maxAttempts)
                {
                    ConnectionFailed?.Invoke(PulsoidConnectionError.MaxRetriesExhausted,
                        "Failed to connect after multiple attempts.");
                    return;
                }

                int delayMs = Math.Min(10_000, 2000 * (int)Math.Pow(2, attempt));
                Logging.WriteInfo($"Retrying connection in {delayMs}ms...");
                await Task.Delay(delayMs, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex);
                DisposeWebSocket();
                ConnectionFailed?.Invoke(PulsoidConnectionError.UnexpectedError, ex.Message);
                return;
            }
        }
    }

    public async Task DisconnectAsync()
    {
        if (_webSocket != null && _webSocket.State == WebSocketState.Open)
        {
            try
            {
                await _webSocket.CloseAsync(
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

    public async Task<PulsoidStatisticsResponse> FetchStatisticsAsync(string accessToken, string timeRange)
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

    /// <summary>
    /// Reads WebSocket messages until the connection dropsor is cancelled.
    /// On recoverable failures, validates token and attempts reconnection from within ConnectAsync's retry loop.
    /// </summary>
    private async Task ReceiveLoopAsync(string accessToken, CancellationToken ct)
    {
        var buffer = new byte[1024];
        bool shouldAttemptReconnect = true;

        try
        {
            while (_webSocket != null &&
                   _webSocket.State == WebSocketState.Open &&
                   !ct.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                using var messageStream = new MemoryStream();
                try
                {
                    do
                    {
                        result = await _webSocket.ReceiveAsync(
                            new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await _webSocket.CloseAsync(
                                WebSocketCloseStatus.NormalClosure, "Closing", ct).ConfigureAwait(false);
                            return;
                        }

                        if (result.Count > 0)
                            messageStream.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);
                }
                catch (WebSocketException wex)
                {
                    Logging.WriteInfo($"WebSocket exception during receive: {wex.Message}");
                    bool tokenValid = await _tokenValidator.ValidateTokenAsync(accessToken).ConfigureAwait(false);
                    if (!tokenValid)
                    {
                        ConnectionFailed?.Invoke(PulsoidConnectionError.TokenInvalid,
                            "Access token invalid or revoked. Please reconnect.");
                        shouldAttemptReconnect = false;
                        return;
                    }
                    break; // Break to reconnect via ConnectAsync retry loop
                }
                catch (OperationCanceledException)
                {
                    shouldAttemptReconnect = false;
                    break;
                }
                catch (IOException ioex)
                {
                    Logging.WriteInfo($"IO exception during receive: {ioex.Message}");
                    break;
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
        finally
        {
            ConnectionStateChanged?.Invoke(false);

            if (shouldAttemptReconnect && !ct.IsCancellationRequested)
            {
                bool valid = await _tokenValidator.ValidateTokenAsync(accessToken).ConfigureAwait(false);
                if (valid)
                {
                    Logging.WriteInfo("WebSocket connection lost, attempting reconnection...");
                    await Task.Delay(5000, ct).ConfigureAwait(false);
                    // Recurse into ConnectAsync for a fresh retry session
                    await ConnectAsync(accessToken, ct).ConfigureAwait(false);
                }
                else
                {
                    ConnectionFailed?.Invoke(PulsoidConnectionError.TokenInvalid,
                        "Access token invalid or revoked. Please reconnect.");
                }
            }
        }
    }

    /// <summary>
    /// Parses raw heart rate from a Pulsoid WebSocket message (plain int or JSON).
    /// </summary>
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
