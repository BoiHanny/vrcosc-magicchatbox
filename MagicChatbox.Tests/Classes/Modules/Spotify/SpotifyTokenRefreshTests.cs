using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.Modules.Spotify;
using vrcosc_magicchatbox.Services;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules.Spotify;

public sealed class SpotifyTokenRefreshTests
{
    private sealed class StubHandler(Func<HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(responseFactory());
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class NoOpNavigation : INavigationService
    {
        public bool OpenUrl(string url) => true;
        public bool OpenUrl(string url, string[] allowedDomains) => true;
        public bool OpenFolder(string folderPath) => true;
        public bool OpenFileInExplorer(string filePath) => true;
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Temporary_endpoint_failures_do_not_require_reauthentication(HttpStatusCode statusCode)
    {
        using var handler = new StubHandler(() => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent("""{"error":"temporarily_unavailable"}"""),
        });
        using var oauth = new SpotifyOAuthHandler(
            new NoOpNavigation(),
            new StubHttpClientFactory(handler));

        SpotifyTokenRefreshOutcome outcome = await oauth.RefreshTokenAsync("client", "refresh");

        Assert.False(outcome.Succeeded);
        Assert.Equal(SpotifyTokenRefreshFailureReason.Transient, outcome.FailureReason);
        Assert.Equal(statusCode, outcome.StatusCode);
    }

    [Fact]
    public async Task Invalid_grants_still_require_reauthentication()
    {
        using var handler = new StubHandler(() => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                """{"error":"invalid_grant","error_description":"Refresh token revoked"}"""),
        });
        using var oauth = new SpotifyOAuthHandler(
            new NoOpNavigation(),
            new StubHttpClientFactory(handler));

        SpotifyTokenRefreshOutcome outcome = await oauth.RefreshTokenAsync("client", "refresh");

        Assert.Equal(SpotifyTokenRefreshFailureReason.ReauthenticationRequired, outcome.FailureReason);
        Assert.Equal("invalid_grant", outcome.SpotifyError);
    }

    [Fact]
    public async Task OAuth_temporary_errors_are_transient_even_when_returned_as_bad_requests()
    {
        using var handler = new StubHandler(() => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"error":"temporarily_unavailable"}"""),
        });
        using var oauth = new SpotifyOAuthHandler(
            new NoOpNavigation(),
            new StubHttpClientFactory(handler));

        SpotifyTokenRefreshOutcome outcome = await oauth.RefreshTokenAsync("client", "refresh");

        Assert.Equal(SpotifyTokenRefreshFailureReason.Transient, outcome.FailureReason);
    }

    [Fact]
    public async Task A_successful_refresh_returns_the_new_token()
    {
        using var handler = new StubHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"access_token":"new-access","refresh_token":"new-refresh","expires_in":3600}"""),
        });
        using var oauth = new SpotifyOAuthHandler(
            new NoOpNavigation(),
            new StubHttpClientFactory(handler));

        SpotifyTokenRefreshOutcome outcome = await oauth.RefreshTokenAsync("client", "refresh");

        Assert.True(outcome.Succeeded);
        Assert.Equal("new-access", outcome.Token!.AccessToken);
    }
}
