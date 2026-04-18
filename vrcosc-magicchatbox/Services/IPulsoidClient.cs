using System;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.Modules;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Describes why a Pulsoid WebSocket connection was terminated.
/// </summary>
public enum PulsoidConnectionError
{
    /// <summary>The OAuth token was revoked or expired.</summary>
    TokenInvalid,
    /// <summary>All reconnection attempts were exhausted.</summary>
    MaxRetriesExhausted,
    /// <summary>An unexpected/unrecoverable error occurred.</summary>
    UnexpectedError
}

/// <summary>
/// Pure network client for the Pulsoid API (WebSocket streaming + REST statistics).
/// Encapsulates connection lifecycle, exponential-backoff reconnection, and JSON parsing.
/// Business logic (throttling, OSC dispatch, UI updates) stays in PulsoidModule.
/// </summary>
public interface IPulsoidClient : IDisposable
{
    /// <summary>
    /// Raised when a raw heart rate value is received from the WebSocket.
    /// Fires on a background thread — subscribers must marshal to UI thread if needed.
    /// </summary>
    event Action<int> HeartRateReceived;

    /// <summary>
    /// Raised when the connection is lost after all retries are exhausted
    /// or the token is found to be invalid. Fires on a background thread.
    /// </summary>
    event Action<PulsoidConnectionError, string> ConnectionFailed;

    /// <summary>
    /// Raised when the WebSocket connection state changes.
    /// true = connected, false = disconnected. Fires on a background thread.
    /// </summary>
    event Action<bool> ConnectionStateChanged;

    /// <summary>Whether the WebSocket is currently open.</summary>
    bool IsConnected { get; }

    /// <summary>
    /// Connect to the Pulsoid WebSocket with automatic reconnection.
    /// This is a self-contained session: it connects, receives, retries on failure,
    /// and only returns when cancelled or terminally failed.
    /// </summary>
    Task ConnectAsync(string accessToken, CancellationToken ct);

    /// <summary>Gracefully disconnect from the WebSocket.</summary>
    Task DisconnectAsync();

    /// <summary>
    /// Fetch heart rate statistics from the Pulsoid REST API.
    /// </summary>
    /// <param name="accessToken">OAuth access token.</param>
    /// <param name="timeRange">Time range description (e.g. "24h", "7d", "30d").</param>
    /// <returns>Statistics response, or null on failure.</returns>
    Task<PulsoidStatisticsResponse> FetchStatisticsAsync(string accessToken, string timeRange);
}
