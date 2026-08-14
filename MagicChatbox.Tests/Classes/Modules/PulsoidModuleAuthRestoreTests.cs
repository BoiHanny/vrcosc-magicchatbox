using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.State;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules;

/// <summary>
/// Drives the real <see cref="PulsoidModule"/> against a stubbed Pulsoid, because the restart bug
/// lived in the module's decision table, not in a helper that could be tested in isolation.
/// </summary>
public sealed class PulsoidModuleAuthRestoreTests : IDisposable
{
    private const string StoredToken = "b7d1f2e3-4444-5555-6666-0a1b2c3d4e5f";

    // ---- doubles -------------------------------------------------------------------------------

    private sealed class TempEnvironment : IEnvironmentService
    {
        public TempEnvironment(string root) => DataPath = root;
        public string DataPath { get; }
        public string LogPath => Path.Combine(DataPath, "logs");
        public string VrcPath => DataPath;
        public void SetCustomProfile(int profileNumber) => throw new NotSupportedException();
    }

    private sealed class FakeAppState : IAppState
    {
        public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
        public bool MasterSwitch { get; set; } = true;
        public bool IsVRRunning { get; set; }
        public bool BussyBoysMode { get; set; }
        public bool Egg_Dev { get; set; }
        public bool PulsoidAuthConnected
        {
            get => PulsoidAuthState is PulsoidAuthState.Authenticated
                                    or PulsoidAuthState.Unverified
                                    or PulsoidAuthState.Unreachable;
            set => PulsoidAuthState = value ? PulsoidAuthState.Authenticated : PulsoidAuthState.NoToken;
        }
        public PulsoidAuthState PulsoidAuthState { get; set; } = PulsoidAuthState.NoToken;
        public int MainWindowBlurEffect { get; set; }
    }

    /// <summary>Runs everything inline: xUnit has no WPF dispatcher to marshal to.</summary>
    private sealed class InlineDispatcher : IUiDispatcher
    {
        public void Invoke(Action action) => action();
        public T Invoke<T>(Func<T> func) => func();
        public Task InvokeAsync(Action action) { action(); return Task.CompletedTask; }
        public Task<T> InvokeAsync<T>(Func<T> func) => Task.FromResult(func());
        public bool CheckAccess() => true;
        public void BeginInvoke(Action action) => action();
        public void Shutdown() { }
    }

    private sealed class NoOpOscSender : IOscSender
    {
        public Task<bool> SendOSCMessage(bool fx, int delay = 0, bool force = false, string? explicitText = null) => Task.FromResult(true);
        public void SendOscParam(string address, float value) { }
        public void SendOscParam(string address, int value) { }
        public void SendOscParam(string address, bool value) { }
        public void SendTypingIndicatorAsync() { }
        public void StopTypingIndicator() { }
        public Task SentClearMessage(int delay) => Task.CompletedTask;
        public Task ToggleVoice(bool force = false) => Task.CompletedTask;
    }

    private sealed class NoOpNavigation : INavigationService
    {
        public bool OpenUrl(string url) => true;
        public bool OpenUrl(string url, string[] allowedDomains) => true;
        public bool OpenFolder(string folderPath) => true;
        public bool OpenFileInExplorer(string filePath) => true;
    }

    private sealed class RecordingPulsoidClient : IPulsoidClient
    {
        public event Action<int>? HeartRateReceived { add { } remove { } }
        public event Action<PulsoidConnectionError, string>? ConnectionFailed { add { } remove { } }
        public event Action<bool>? ConnectionStateChanged { add { } remove { } }

        public bool IsConnected => false;
        public int ConnectAttempts;
        public string? LastToken;

        public Task ConnectAsync(string accessToken, CancellationToken ct)
        {
            Interlocked.Increment(ref ConnectAttempts);
            LastToken = accessToken;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync() => Task.CompletedTask;
        public Task<PulsoidStatisticsResponse> FetchStatisticsAsync(string accessToken, string timeRange) => Task.FromResult<PulsoidStatisticsResponse>(null!);
        public void Dispose() { }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public int Calls;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult(_responder(request));
        }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public StubHttpClientFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    // ---- fixture -------------------------------------------------------------------------------

    private readonly string _dir;
    private readonly TempEnvironment _env;
    private readonly List<IDisposable> _disposables = new();

    public PulsoidModuleAuthRestoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "MagicChatboxTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _env = new TempEnvironment(_dir);
    }

    public void Dispose()
    {
        foreach (var d in _disposables)
        {
            try { d.Dispose(); } catch { }
        }
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    /// <summary>Writes a token to disk exactly as a previous run of the app would have.</summary>
    private void SeedStoredToken(string token)
    {
        var writer = new JsonSettingsProvider<PulsoidModuleSettings>(_env);
        writer.Value.AccessTokenOAuth = token;
        writer.FlushPendingSave();
        writer.Dispose();
    }

    private (PulsoidModule module, FakeAppState state, RecordingPulsoidClient client, StubHandler handler) BuildModule(
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        bool heartRateOscEnabled = true)
    {
        var handler = new StubHandler(responder);
        var oauth = new PulsoidOAuthHandler(new StubHttpClientFactory(handler), new NoOpNavigation());
        var provider = new JsonSettingsProvider<PulsoidModuleSettings>(_env);
        var state = new FakeAppState();
        var client = new RecordingPulsoidClient();
        var integrations = new IntegrationSettings { IntgrHeartRate_OSC = heartRateOscEnabled };

        var module = new PulsoidModule(
            state,
            client,
            new InlineDispatcher(),
            new NoOpOscSender(),
            integrations,
            oauth,
            provider);

        _disposables.Add(module);
        _disposables.Add(provider);
        _disposables.Add(oauth);
        _disposables.Add(handler);

        return (module, state, client, handler);
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string body)
        => new(code) { Content = new StringContent(body) };

    private static HttpResponseMessage ValidTokenResponse()
        => Json(HttpStatusCode.OK, "{\"scopes\":[\"data:heart_rate:read\",\"data:statistics:read\"],\"expires_in\":90000}");

    // ---- the regression ------------------------------------------------------------------------

    [Fact]
    public void Constructing_WithAStoredToken_RestoresTheSignedInStateWithoutTouchingTheNetwork()
    {
        SeedStoredToken(StoredToken);

        var (_, state, _, handler) = BuildModule(_ => throw new InvalidOperationException("no HTTP expected during construction"));

        Assert.Equal(PulsoidAuthState.Unverified, state.PulsoidAuthState);
        Assert.True(state.PulsoidAuthConnected);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public void Constructing_WithNoStoredToken_ReportsNoToken()
    {
        var (_, state, _, _) = BuildModule(_ => ValidTokenResponse());

        Assert.Equal(PulsoidAuthState.NoToken, state.PulsoidAuthState);
        Assert.False(state.PulsoidAuthConnected);
    }

    [Fact]
    public async Task Startup_WhenPulsoidIsUnreachable_KeepsTheSignInAndStillConnects()
    {
        SeedStoredToken(StoredToken);

        // HttpClient's own timeout surfaces as TaskCanceledException; this used to escape the
        // HttpRequestException guard, return "invalid", and latch "Expired access token" forever.
        var (module, state, client, _) = BuildModule(_ => throw new TaskCanceledException("timeout"));

        await module.StartAsync();

        Assert.Equal(PulsoidAuthState.Unreachable, state.PulsoidAuthState);
        Assert.True(state.PulsoidAuthConnected);
        Assert.Equal(StoredToken, module.Settings.AccessTokenOAuth);
        Assert.Equal(1, client.ConnectAttempts);
        Assert.Contains("reach Pulsoid", module.PulsoidAccessErrorTxt, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task Startup_WithATransientOrNonAuthStatus_NeverSignsTheUserOut(HttpStatusCode code)
    {
        SeedStoredToken(StoredToken);

        var (module, state, client, _) = BuildModule(_ => Json(code, "{\"error_code\":\"7012\",\"error_message\":\"nope\"}"));

        await module.StartAsync();

        Assert.NotEqual(PulsoidAuthState.Rejected, state.PulsoidAuthState);
        Assert.True(state.PulsoidAuthConnected);
        Assert.Equal(StoredToken, module.Settings.AccessTokenOAuth);
        Assert.Equal(1, client.ConnectAttempts);
    }

    [Fact]
    public async Task Startup_WithAHealthyToken_ConfirmsTheSignInAndConnects()
    {
        SeedStoredToken(StoredToken);

        var (module, state, client, _) = BuildModule(_ => ValidTokenResponse());

        await module.StartAsync();

        Assert.Equal(PulsoidAuthState.Authenticated, state.PulsoidAuthState);
        Assert.False(module.PulsoidAccessError);
        Assert.Equal(StoredToken, client.LastToken);
    }

    [Fact]
    public async Task Startup_WithAMissingStatisticsScope_StillCountsAsSignedIn()
    {
        SeedStoredToken(StoredToken);

        // Only data:heart_rate:read actually gates the feature; demanding profile:read and
        // data:statistics:read turned a narrowed-but-working grant into a permanent lockout.
        var (module, state, _, _) = BuildModule(_ => Json(HttpStatusCode.OK, "{\"scopes\":[\"data:heart_rate:read\"]}"));

        await module.StartAsync();

        Assert.Equal(PulsoidAuthState.Authenticated, state.PulsoidAuthState);
    }

    [Fact]
    public async Task Startup_WhenPulsoidReturns401_RejectsTheTokenButKeepsItStored()
    {
        SeedStoredToken(StoredToken);

        var (module, state, client, _) = BuildModule(_ =>
            Json(HttpStatusCode.Unauthorized, "{\"error_code\":\"7006\",\"error_message\":\"token_expired\"}"));

        await module.StartAsync();

        Assert.Equal(PulsoidAuthState.Rejected, state.PulsoidAuthState);
        Assert.False(state.PulsoidAuthConnected);
        Assert.True(module.PulsoidAccessError);
        Assert.Contains("rejected", module.PulsoidAccessErrorTxt, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, client.ConnectAttempts);

        // Rejected is a message to the user, not a licence to wipe the credential; only the
        // explicit Disconnect command clears it.
        Assert.Equal(StoredToken, module.Settings.AccessTokenOAuth);
    }

    [Fact]
    public async Task Startup_WithNoStoredToken_ReportsNoTokenRatherThanRejection()
    {
        var (module, state, client, _) = BuildModule(_ => ValidTokenResponse());

        await module.StartAsync();

        Assert.Equal(PulsoidAuthState.NoToken, state.PulsoidAuthState);
        Assert.Equal(0, client.ConnectAttempts);
    }

    [Fact]
    public void SaveSettings_WritesTheTokenToDiskImmediately()
    {
        var (module, _, _, _) = BuildModule(_ => ValidTokenResponse());

        module.Settings.AccessTokenOAuth = "written-right-now";
        module.SaveSettings();

        string file = Path.Combine(_dir, $"{nameof(PulsoidModuleSettings)}.json");
        Assert.True(File.Exists(file));

        var reloaded = new JsonSettingsProvider<PulsoidModuleSettings>(_env);
        Assert.Equal("written-right-now", reloaded.Value.AccessTokenOAuth);
        reloaded.Dispose();
    }
}
