using System.Threading.Tasks;

namespace vrcosc_magicchatbox.Classes.Modules.Twitch;

public interface ITwitchApiClient
{
    void Configure(string clientId, string accessToken);

    Task<TwitchTokenValidation> ValidateTokenAsync(string accessToken);

    Task<string> GetBroadcasterIdAsync(string channelLogin);

    Task<TwitchStreamSnapshot> GetStreamInfoAsync(string broadcasterId);

    Task<TwitchFollowerResult> GetFollowerCountAsync(string broadcasterId, string moderatorId);

    Task<TwitchActionResult> SendAnnouncementAsync(string broadcasterId, string moderatorId, string message, string color);

    Task<TwitchActionResult> SendShoutoutAsync(string fromBroadcasterId, string toBroadcasterId, string moderatorId);

    Task<string> ResolveUserIdAsync(string login);
}
