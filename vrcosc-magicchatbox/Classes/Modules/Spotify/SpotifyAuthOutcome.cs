using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using vrcosc_magicchatbox.Core;

namespace vrcosc_magicchatbox.Classes.Modules.Spotify;

public enum SpotifyAuthFailureReason
{
    None = 0,

    ListenerUnavailable,

    TimedOut,

    StateMismatch,

    AuthorizationDenied,

    TokenExchangeRejected,

    MalformedResponse,

    Unexpected,
}

public sealed record SpotifyAuthOutcome
{
    public SpotifyTokenResult? Token { get; init; }

    public SpotifyAuthFailureReason Reason { get; init; }

    public string? SpotifyError { get; init; }

    public string? Detail { get; init; }

    public bool Succeeded => Token != null;

    public static SpotifyAuthOutcome Success(SpotifyTokenResult token)
        => new() { Token = token, Reason = SpotifyAuthFailureReason.None };

    public static SpotifyAuthOutcome Failure(
        SpotifyAuthFailureReason reason,
        string? detail = null,
        string? spotifyError = null)
        => new() { Reason = reason, Detail = detail, SpotifyError = spotifyError };

    public static (string? Error, string? Description) ParseErrorBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return (null, null);

        try
        {
            var json = JObject.Parse(body);

            var error = json["error"];
            if (error is JObject nested)
                return (nested["status"]?.ToString(), nested["message"]?.ToString());

            return (error?.ToString(), json["error_description"]?.ToString());
        }
        catch (Exception)
        {
            return (null, null);
        }
    }

    public string BuildUserMessage()
    {
        if (Succeeded)
            return "Spotify connected.";

        string redirect = Constants.SpotifyOAuthRedirectUri;

        return Reason switch
        {
            SpotifyAuthFailureReason.ListenerUnavailable =>
                $"Couldn't open the local callback at {redirect} — something else is using that port, "
                + $"or it's blocked. Close other copies of MagicChatbox and try again.{Suffix()}",

            SpotifyAuthFailureReason.TimedOut =>
                "The browser didn't come back in time. Finish the Spotify approval in the tab that "
                + "opened, then try again.",

            SpotifyAuthFailureReason.StateMismatch =>
                "The Spotify redirect failed its security check. Close any other pending Spotify "
                + "approval tabs and try again.",

            SpotifyAuthFailureReason.AuthorizationDenied or
            SpotifyAuthFailureReason.TokenExchangeRejected => DescribeSpotifyRejection(redirect),

            SpotifyAuthFailureReason.MalformedResponse =>
                $"Spotify's reply couldn't be read. Try again in a moment.{Suffix()}",

            _ => $"Spotify setup failed.{Suffix()}",
        };
    }

    private string DescribeSpotifyRejection(string redirect)
    {
        string code = SpotifyError ?? string.Empty;
        string description = Detail ?? string.Empty;
        string combined = $"{code} {description}";

        if (combined.Contains("redirect", StringComparison.OrdinalIgnoreCase))
        {
            return "Spotify rejected the redirect URI. Open your app in the Spotify Developer "
                 + $"Dashboard and add exactly: {redirect}{Suffix()}";
        }

        if (code.Equals("invalid_client", StringComparison.OrdinalIgnoreCase))
        {
            return "Spotify didn't recognise that Client ID. Copy it again from your app in the "
                 + $"Spotify Developer Dashboard.{Suffix()}";
        }

        if (code.Equals("access_denied", StringComparison.OrdinalIgnoreCase))
            return "You declined the Spotify permission request, so nothing was connected.";

        if (code.Equals("invalid_grant", StringComparison.OrdinalIgnoreCase))
        {
            return "Spotify rejected the authorization. This usually means the redirect URI in your "
                 + $"app doesn't match exactly: {redirect}{Suffix()}";
        }

        return $"Spotify refused the connection.{Suffix()}";
    }

    private string Suffix()
    {
        string code = string.IsNullOrWhiteSpace(SpotifyError) ? string.Empty : SpotifyError!;
        string detail = string.IsNullOrWhiteSpace(Detail) ? string.Empty : Detail!;
        string combined = string.Join(": ", new[] { code, detail }.Where(part => part.Length > 0));
        return combined.Length == 0 ? string.Empty : $" ({combined})";
    }
}
