using System;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.Modules;

namespace vrcosc_magicchatbox.Services;

public enum PulsoidConnectionError
{
    TokenInvalid,

    MaxRetriesExhausted,

    SubscriptionRequired,

    StatisticsUnavailable,

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

    Task<PulsoidStatisticsResponse?> FetchStatisticsAsync(string accessToken, string timeRange);
}
