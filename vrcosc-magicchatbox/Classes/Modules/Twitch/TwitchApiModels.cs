using System.Collections.Generic;

namespace vrcosc_magicchatbox.Classes.Modules.Twitch;

public sealed record TwitchTokenValidation(
    bool IsValid,
    string UserId,
    string Login,
    string TokenClientId,
    IReadOnlyList<string> Scopes);

public sealed record TwitchStreamSnapshot(bool IsLive, int ViewerCount, string GameName, string Title);

public sealed record TwitchFollowerResult(bool Success, int Count, bool Unauthorized, bool Forbidden, string Message);

public sealed record TwitchActionResult(bool Success, string Message);
