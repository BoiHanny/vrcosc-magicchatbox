using System.Collections.Generic;
using System.Net;

namespace vrcosc_magicchatbox.Classes.Modules.Spotify;

public sealed record SpotifyApiResult<T>(
    bool Success,
    T? Value,
    HttpStatusCode StatusCode,
    string Message,
    bool Unauthorized = false,
    bool Forbidden = false,
    bool RateLimited = false);

public sealed record SpotifyTokenResult(
    string AccessToken,
    string? RefreshToken,
    int ExpiresIn,
    string? Scope);

public sealed record SpotifyProfileSnapshot(string Id, string DisplayName);

public sealed record SpotifyTrackSnapshot(
    string Id,
    string Uri,
    string ExternalUrl,
    string Title,
    string Artist,
    string Album,
    bool Explicit,
    int DurationMs);

public sealed record SpotifyPlaybackSnapshot(
    bool HasPlayback,
    bool IsPlaying,
    int ProgressMs,
    bool ShuffleState,
    string RepeatState,
    string DeviceName,
    int VolumePercent,
    SpotifyTrackSnapshot? Track);

public sealed record SpotifyQueueSnapshot(IReadOnlyList<string> UpcomingTracks);
