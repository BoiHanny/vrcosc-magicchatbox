using System.Threading.Tasks;

namespace vrcosc_magicchatbox.Classes.Modules.Spotify;

public interface ISpotifyApiClient
{
    Task<SpotifyApiResult<SpotifyProfileSnapshot>> GetProfileAsync(string accessToken);
    Task<SpotifyApiResult<SpotifyPlaybackSnapshot>> GetPlaybackAsync(string accessToken);
    Task<SpotifyApiResult<bool>> IsTrackSavedAsync(string accessToken, string trackId);
    Task<SpotifyApiResult<SpotifyQueueSnapshot>> GetQueueAsync(string accessToken);
    Task<SpotifyApiResult<bool>> PlayAsync(string accessToken);
    Task<SpotifyApiResult<bool>> PauseAsync(string accessToken);
    Task<SpotifyApiResult<bool>> NextAsync(string accessToken);
    Task<SpotifyApiResult<bool>> PreviousAsync(string accessToken);
    Task<SpotifyApiResult<bool>> SetShuffleAsync(string accessToken, bool state);
    Task<SpotifyApiResult<bool>> SetRepeatAsync(string accessToken, string state);
    Task<SpotifyApiResult<bool>> SetVolumeAsync(string accessToken, int volumePercent);
    Task<SpotifyApiResult<bool>> SaveTrackAsync(string accessToken, string trackId);
    Task<SpotifyApiResult<bool>> RemoveTrackAsync(string accessToken, string trackId);
}
