using System.Net;

namespace vrcosc_magicchatbox.Classes.Modules.Spotify;

public enum SpotifyTokenRefreshFailureReason
{
    None = 0,
    ReauthenticationRequired,
    Transient,
}

public sealed record SpotifyTokenRefreshOutcome
{
    public SpotifyTokenResult? Token { get; init; }

    public SpotifyTokenRefreshFailureReason FailureReason { get; init; }

    public HttpStatusCode? StatusCode { get; init; }

    public string? SpotifyError { get; init; }

    public string? Detail { get; init; }

    public bool Succeeded => Token != null;

    public static SpotifyTokenRefreshOutcome Success(SpotifyTokenResult token)
        => new() { Token = token };

    public static SpotifyTokenRefreshOutcome Failure(
        SpotifyTokenRefreshFailureReason reason,
        HttpStatusCode? statusCode = null,
        string? spotifyError = null,
        string? detail = null)
        => new()
        {
            FailureReason = reason,
            StatusCode = statusCode,
            SpotifyError = spotifyError,
            Detail = detail,
        };
}
