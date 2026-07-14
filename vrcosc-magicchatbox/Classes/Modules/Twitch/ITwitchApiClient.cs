using System.Threading.Tasks;

namespace vrcosc_magicchatbox.Classes.Modules.Twitch;

/// <summary>
/// Handles all raw Twitch Helix API communication.
/// No UI state, no dispatcher, no settings awareness — pure HTTP.
/// </summary>
public interface ITwitchApiClient
{
    /// <summary>Configure client ID and access token for subsequent requests.</summary>
    void Configure(string clientId, string accessToken);

    /// <summary>Validate an OAuth access token. Returns validity and the associated user ID.</summary>
    Task<TwitchTokenValidation> ValidateTokenAsync(string accessToken);

    /// <summary>Look up a broadcaster ID by channel login name.</summary>
    Task<string> GetBroadcasterIdAsync(string channelLogin);

    /// <summary>Get current stream info for a broadcaster. Returns null if offline.</summary>
    Task<TwitchStreamSnapshot> GetStreamInfoAsync(string broadcasterId);

    /// <summary>Get total follower count for a broadcaster. Requires moderator:read:followers scope.</summary>
    Task<TwitchFollowerResult> GetFollowerCountAsync(string broadcasterId, string moderatorId);

    /// <summary>Send a chat announcement.</summary>
    Task<TwitchActionResult> SendAnnouncementAsync(string broadcasterId, string moderatorId, string message, string color);

    /// <summary>Send a shoutout to another broadcaster.</summary>
    Task<TwitchActionResult> SendShoutoutAsync(string fromBroadcasterId, string toBroadcasterId, string moderatorId);

    /// <summary>Resolve a user login to a user ID.</summary>
    Task<string> ResolveUserIdAsync(string login);
}
