using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Services;
using Xunit;

namespace MagicChatbox.Tests.Services;

/// <summary>
/// The statistics endpoint is optional: <see cref="PulsoidOAuthHandler"/> deliberately accepts a
/// token without data:statistics:read, and the live socket is the authoritative liveness signal.
/// A failure here therefore disables statistics and nothing else — raising TokenInvalid used to
/// sign the user out of heart rate while beats were still arriving.
/// </summary>
public sealed class PulsoidApiClientStatisticsTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public StubHttpClientFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private sealed class NeverAskedValidator : IPulsoidTokenValidator
    {
        public Task<PulsoidTokenValidation> ValidateTokenAsync(string accessToken)
            => throw new InvalidOperationException("the statistics path must not consult the validator");
    }

    private static async Task<List<PulsoidConnectionError>> FetchAndCollectErrors(HttpStatusCode status)
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent("{\"error_code\":\"7006\",\"error_message\":\"nope\"}")
        });
        using var client = new PulsoidApiClient(new StubHttpClientFactory(handler), new NeverAskedValidator());

        var errors = new List<PulsoidConnectionError>();
        client.ConnectionFailed += (error, _) => errors.Add(error);

        var stats = await client.FetchStatisticsAsync("some-token", "24h");

        Assert.Null(stats);
        return errors;
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.PaymentRequired)]
    public async Task ARefusedStatisticsRequest_ReportsStatisticsUnavailable_NotAnInvalidToken(HttpStatusCode status)
    {
        var errors = await FetchAndCollectErrors(status);

        Assert.Equal(new[] { PulsoidConnectionError.StatisticsUnavailable }, errors);
        Assert.DoesNotContain(PulsoidConnectionError.TokenInvalid, errors);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task ATransientStatisticsFailure_ReportsNothingAtAll(HttpStatusCode status)
    {
        var errors = await FetchAndCollectErrors(status);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task AHealthyStatisticsResponse_IsDeserialized()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"minimum_beats_per_minute\":54,\"maximum_beats_per_minute\":150,\"average_beats_per_minute\":88}")
        });
        using var client = new PulsoidApiClient(new StubHttpClientFactory(handler), new NeverAskedValidator());

        var stats = await client.FetchStatisticsAsync("some-token", "24h");

        Assert.NotNull(stats);
        Assert.Equal(54, stats!.minimum_beats_per_minute);
        Assert.Equal(150, stats.maximum_beats_per_minute);
        Assert.Equal(88, stats.average_beats_per_minute);
    }
}
