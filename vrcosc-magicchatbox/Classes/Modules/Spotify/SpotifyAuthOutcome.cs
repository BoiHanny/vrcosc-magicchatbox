using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using vrcosc_magicchatbox.Core;

namespace vrcosc_magicchatbox.Classes.Modules.Spotify;

/// <summary>Why a Spotify OAuth attempt did not produce a token.</summary>
public enum SpotifyAuthFailureReason
{
    /// <summary>No failure — a token was returned.</summary>
    None = 0,

    /// <summary>The local callback listener could not start (port in use, URL ACL, firewall).</summary>
    ListenerUnavailable,

    /// <summary>The browser never returned to the callback within the allotted window.</summary>
    TimedOut,

    /// <summary>The redirect carried a different <c>state</c> than the one we sent.</summary>
    StateMismatch,

    /// <summary>Spotify reported an error on the authorize step, or sent no code.</summary>
    AuthorizationDenied,

    /// <summary>Spotify rejected the authorization-code-for-token exchange.</summary>
    TokenExchangeRejected,

    /// <summary>Spotify replied with something that could not be parsed.</summary>
    MalformedResponse,

    /// <summary>Anything not covered above.</summary>
    Unexpected,
}

/// <summary>
/// Result of a Spotify OAuth attempt, carrying enough detail to tell the user what to fix.
/// <para>
/// Previously every failure collapsed into a null return and a single generic message, so a
/// mis-registered redirect URI, an unrecognised Client ID and an occupied callback port were
/// indistinguishable to the person trying to connect.
/// </para>
/// </summary>
public sealed record SpotifyAuthOutcome
{
    /// <summary>The token, when the flow succeeded.</summary>
    public SpotifyTokenResult? Token { get; init; }

    /// <summary>Which step failed. <see cref="SpotifyAuthFailureReason.None"/> on success.</summary>
    public SpotifyAuthFailureReason Reason { get; init; }

    /// <summary>Spotify's machine-readable error code, e.g. <c>invalid_client</c>.</summary>
    public string? SpotifyError { get; init; }

    /// <summary>Spotify's own description, or a local diagnostic. Never contains a code or token.</summary>
    public string? Detail { get; init; }

    public bool Succeeded => Token != null;

    public static SpotifyAuthOutcome Success(SpotifyTokenResult token)
        => new() { Token = token, Reason = SpotifyAuthFailureReason.None };

    public static SpotifyAuthOutcome Failure(
        SpotifyAuthFailureReason reason,
        string? detail = null,
        string? spotifyError = null)
        => new() { Reason = reason, Detail = detail, SpotifyError = spotifyError };

    /// <summary>
    /// Pulls <c>error</c> / <c>error_description</c> out of a Spotify error body. Spotify puts the
    /// actual cause there (for example <c>invalid_grant: Invalid redirect URI</c>) and the app used
    /// to throw the whole body away, keeping only the status code.
    /// </summary>
    public static (string? Error, string? Description) ParseErrorBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return (null, null);

        try
        {
            var json = JObject.Parse(body);

            // Token endpoint: {"error":"invalid_grant","error_description":"..."}
            // Web API:        {"error":{"status":401,"message":"..."}}
            var error = json["error"];
            if (error is JObject nested)
                return (nested["status"]?.ToString(), nested["message"]?.ToString());

            return (error?.ToString(), json["error_description"]?.ToString());
        }
        catch (Exception)
        {
            // Not JSON — a proxy or captive portal can return HTML here.
            return (null, null);
        }
    }

    /// <summary>An actionable sentence for the connect dialog and toast.</summary>
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
