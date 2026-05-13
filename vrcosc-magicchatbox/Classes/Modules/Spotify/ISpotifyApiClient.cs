using System.Threading;
using System.Threading.Tasks;

namespace vrcosc_magicchatbox.Classes.Modules.Spotify;

public interface ISpotifyApiClient
{
    Task<SpotifyApiResult<SpotifyProfileSnapshot>> GetProfileAsync(string accessToken, CancellationToken cancellationToken = default);
    Task<SpotifyApiResult<SpotifyPlaybackSnapshot>> GetPlaybackAsync(string accessToken, CancellationToken cancellationToken = default);
    Task<SpotifyApiResult<bool>> IsTrackSavedAsync(string accessToken, string trackId, CancellationToken cancellationToken = default);
    Task<SpotifyApiResult<SpotifyQueueSnapshot>> GetQueueAsync(string accessToken, CancellationToken cancellationToken = default);
    Task<SpotifyApiResult<bool>> PlayAsync(string accessToken, CancellationToken cancellationToken = default);
    Task<SpotifyApiResult<bool>> PauseAsync(string accessToken, CancellationToken cancellationToken = default);
    Task<SpotifyApiResult<bool>> NextAsync(string accessToken, CancellationToken cancellationToken = default);
    Task<SpotifyApiResult<bool>> PreviousAsync(string accessToken, CancellationToken cancellationToken = default);
    Task<SpotifyApiResult<bool>> SetShuffleAsync(string accessToken, bool state, CancellationToken cancellationToken = default);
    Task<SpotifyApiResult<bool>> SetRepeatAsync(string accessToken, string state, CancellationToken cancellationToken = default);
    Task<SpotifyApiResult<bool>> SetVolumeAsync(string accessToken, int volumePercent, CancellationToken cancellationToken = default);
    Task<SpotifyApiResult<bool>> SaveTrackAsync(string accessToken, string trackId, CancellationToken cancellationToken = default);
    Task<SpotifyApiResult<bool>> RemoveTrackAsync(string accessToken, string trackId, CancellationToken cancellationToken = default);
}
