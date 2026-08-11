using vrcosc_magicchatbox.Classes.Modules.Spotify;
using vrcosc_magicchatbox.Core;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules.Spotify;

/// <summary>
/// Every OAuth failure used to collapse into "Connection cancelled or timed out. Check the Client
/// ID and redirect URI." — which named neither the real cause nor the fix.
/// </summary>
public class SpotifyAuthOutcomeTests
{
    // ---- Error-body parsing ---------------------------------------------

    [Fact]
    public void ParseErrorBody_ReadsTheTokenEndpointShape()
    {
        var (error, description) = SpotifyAuthOutcome.ParseErrorBody(
            """{"error":"invalid_grant","error_description":"Invalid redirect URI"}""");

        Assert.Equal("invalid_grant", error);
        Assert.Equal("Invalid redirect URI", description);
    }

    [Fact]
    public void ParseErrorBody_ReadsTheWebApiShape()
    {
        var (error, description) = SpotifyAuthOutcome.ParseErrorBody(
            """{"error":{"status":401,"message":"The access token expired"}}""");

        Assert.Equal("401", error);
        Assert.Equal("The access token expired", description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<html><body>502 Bad Gateway</body></html>")]
    public void ParseErrorBody_SurvivesNonJson(string? body)
    {
        var (error, description) = SpotifyAuthOutcome.ParseErrorBody(body);

        Assert.Null(error);
        Assert.Null(description);
    }

    // ---- Message mapping -------------------------------------------------

    [Fact]
    public void RedirectUriRejection_NamesTheExactUriToRegister()
    {
        var outcome = SpotifyAuthOutcome.Failure(
            SpotifyAuthFailureReason.TokenExchangeRejected,
            "Invalid redirect URI",
            "invalid_grant");

        string message = outcome.BuildUserMessage();

        Assert.Contains(Constants.SpotifyOAuthRedirectUri, message);
        Assert.Contains("Developer", message);
    }

    [Fact]
    public void InvalidClient_PointsAtTheClientId()
    {
        var outcome = SpotifyAuthOutcome.Failure(
            SpotifyAuthFailureReason.TokenExchangeRejected,
            spotifyError: "invalid_client");

        Assert.Contains("Client ID", outcome.BuildUserMessage());
    }

    [Fact]
    public void AccessDenied_SaysTheUserDeclined()
    {
        var outcome = SpotifyAuthOutcome.Failure(
            SpotifyAuthFailureReason.AuthorizationDenied,
            spotifyError: "access_denied");

        Assert.Contains("declined", outcome.BuildUserMessage());
    }

    [Fact]
    public void InvalidGrantWithoutDetail_StillBlamesTheRedirectUri()
    {
        var outcome = SpotifyAuthOutcome.Failure(
            SpotifyAuthFailureReason.TokenExchangeRejected,
            spotifyError: "invalid_grant");

        Assert.Contains(Constants.SpotifyOAuthRedirectUri, outcome.BuildUserMessage());
    }

    [Fact]
    public void ListenerUnavailable_MentionsThePortAndTheLocalCause()
    {
        var outcome = SpotifyAuthOutcome.Failure(
            SpotifyAuthFailureReason.ListenerUnavailable,
            "Failed to listen on prefix (Win32 183)");

        string message = outcome.BuildUserMessage();

        Assert.Contains(Constants.SpotifyOAuthRedirectUri, message);
        Assert.Contains("Win32 183", message);
    }

    [Fact]
    public void TimedOut_TellsTheUserToFinishInTheBrowser()
    {
        var outcome = SpotifyAuthOutcome.Failure(SpotifyAuthFailureReason.TimedOut);

        Assert.Contains("browser", outcome.BuildUserMessage());
    }

    [Fact]
    public void Success_ExposesTheToken()
    {
        var token = new SpotifyTokenResult("access", "refresh", 3600, "scope");
        var outcome = SpotifyAuthOutcome.Success(token);

        Assert.True(outcome.Succeeded);
        Assert.Same(token, outcome.Token);
        Assert.Equal(SpotifyAuthFailureReason.None, outcome.Reason);
    }

    [Fact]
    public void FailureNeverExposesSecrets()
    {
        // Only Spotify's own error fields flow into the message; the code and verifier must not.
        var outcome = SpotifyAuthOutcome.Failure(
            SpotifyAuthFailureReason.TokenExchangeRejected,
            "Invalid redirect URI",
            "invalid_grant");

        Assert.False(outcome.Succeeded);
        Assert.Null(outcome.Token);
    }
}
