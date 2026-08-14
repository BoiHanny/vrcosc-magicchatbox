using System;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.Modules;

namespace vrcosc_magicchatbox.Services;

public enum PulsoidConnectionError
{
    /// <summary>Pulsoid unambiguously refused the credential (HTTP 401 on the handshake or on validate).</summary>
    TokenInvalid,

    /// <summary>Repeated connection failures. Transient: the client keeps retrying in the background.</summary>
    MaxRetriesExhausted,

    /// <summary>Pulsoid answered HTTP 402 — the account's plan does not cover this.</summary>
    SubscriptionRequired,

    /// <summary>
    /// Pulsoid will not serve the optional statistics endpoint for this token (missing
    /// data:statistics:read, a plan limit, a revoked scope). Heart rate is unaffected and the
    /// sign-in must never be demoted because of it.
    /// </summary>
    StatisticsUnavailable,

    /// <summary>Anything else. Transient by default; never a reason to sign the user out.</summary>
    UnexpectedError
}

public interface IPulsoidClient : IDisposable
{
    event Action<int> HeartRateReceived;

    event Action<PulsoidConnectionError, string> ConnectionFailed;

    event Action<bool> ConnectionStateChanged;

    bool IsConnected { get; }

    Task ConnectAsync(string accessToken, CancellationToken ct);

    Task DisconnectAsync();

    Task<PulsoidStatisticsResponse> FetchStatisticsAsync(string accessToken, string timeRange);
}
