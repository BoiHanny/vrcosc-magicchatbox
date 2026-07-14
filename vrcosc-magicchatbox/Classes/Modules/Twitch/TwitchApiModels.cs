using System.Collections.Generic;

namespace vrcosc_magicchatbox.Classes.Modules.Twitch;

/// <summary>Result of a token validation call.</summary>
public sealed record TwitchTokenValidation(
    bool IsValid,
    string UserId,
    string Login,
    string TokenClientId,
    IReadOnlyList<string> Scopes);

/// <summary>Stream snapshot returned by the Helix API.</summary>
public sealed record TwitchStreamSnapshot(bool IsLive, int ViewerCount, string GameName, string Title);

/// <summary>Result of a follower count query.</summary>
public sealed record TwitchFollowerResult(bool Success, int Count, bool Unauthorized, bool Forbidden, string Message);

/// <summary>Generic success/failure result for Twitch chat actions.</summary>
public sealed record TwitchActionResult(bool Success, string Message);
