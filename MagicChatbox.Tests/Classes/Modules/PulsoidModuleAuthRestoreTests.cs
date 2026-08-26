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
        public event Action<int>? HeartRateReceived;
        public event Action<PulsoidConnectionError, string>? ConnectionFailed;
        public event Action<bool>? ConnectionStateChanged;

        public bool IsConnected => false;
        public int ConnectAttempts;
        public string? LastToken;

        /// <summary>
        /// Models the real client, whose connect loop only returns on cancellation or a definitive
        /// rejection. Off by default so the existing single-shot tests keep their shape.
        /// </summary>
        public bool BlockUntilCancelled;

        public async Task ConnectAsync(string accessToken, CancellationToken ct)
        {
            Interlocked.Increment(ref ConnectAttempts);
            LastToken = accessToken;

            if (!BlockUntilCancelled)
                return;

            var cancelled = new TaskCompletionSource();
            using (ct.Register(() => cancelled.TrySetResult()))
                await cancelled.Task.ConfigureAwait(false);
        }

        public Task DisconnectAsync() => Task.CompletedTask;
        public Task<PulsoidStatisticsResponse?> FetchStatisticsAsync(string accessToken, string timeRange) => Task.FromResult<PulsoidStatisticsResponse?>(null);
        public void Dispose() { }

        public void RaiseConnectionFailed(PulsoidConnectionError error, string message)
            => ConnectionFailed?.Invoke(error, message);

        public void RaiseHeartRate(int hr) => HeartRateReceived?.Invoke(hr);

        public void RaiseConnectionStateChanged(bool connected) => ConnectionStateChanged?.Invoke(connected);
    }

    /// <summary>Hands the module a settings instance the test built, rather than one from disk.</summary>
    private sealed class FixedSettingsProvider : ISettingsProvider<PulsoidModuleSettings>
    {
        public FixedSettingsProvider(PulsoidModuleSettings value) => Value = value;
        public event EventHandler? SettingsChanged { add { } remove { } }
        public PulsoidModuleSettings Value { get; }
        public void Save() { }
        public void FlushPendingSave() { }
        public void Reload() { }
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

    /// <summary>Writes a ciphertext that DPAPI will refuse to unprotect, as a foreign profile would.</summary>
    private void SeedUndecryptableToken()
    {
        var seed = new PulsoidModuleSettings
        {
            AccessTokenOAuthEncrypted = Convert.ToBase64String(new byte[] { 1, 0, 0, 0, 9, 9, 9, 9 })
        };
        File.WriteAllText(
            Path.Combine(_dir, $"{nameof(PulsoidModuleSettings)}.json"),
            Newtonsoft.Json.JsonConvert.SerializeObject(seed));
    }

    private static async Task WaitFor(Func<bool> condition, string because)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline && !condition())
            await Task.Delay(10);

        Assert.True(condition(), because);
    }

    private (PulsoidModule module, FakeAppState state, RecordingPulsoidClient client, StubHandler handler) BuildModule(
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        bool heartRateOscEnabled = true,
        PulsoidModuleSettings? settings = null)
    {
        var handler = new StubHandler(responder);
        var oauth = new PulsoidOAuthHandler(new StubHttpClientFactory(handler), new NoOpNavigation());
        ISettingsProvider<PulsoidModuleSettings> provider = settings is null
            ? new JsonSettingsProvider<PulsoidModuleSettings>(_env)
            : new FixedSettingsProvider(settings);
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
        if (provider is IDisposable disposableProvider)
            _disposables.Add(disposableProvider);
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
    public async Task Startup_WhenPulsoidIsUnreachable_LeavesTheCredentialOnDiskForTheNextLaunch()
    {
        SeedStoredToken(StoredToken);

        var (module, state, _, _) = BuildModule(_ => throw new TaskCanceledException("timeout"));

        await module.StartAsync();

        Assert.Equal(PulsoidAuthState.Unreachable, state.PulsoidAuthState);
        Assert.True(state.PulsoidAuthConnected);

        // The outage must not have touched what is stored: a fresh "launch" still finds the token.
        var afterOutage = new JsonSettingsProvider<PulsoidModuleSettings>(_env);
        _disposables.Add(afterOutage);
        Assert.Equal(StoredToken, afterOutage.Value.AccessTokenOAuth);
    }

    // ---- the stored token cannot be read on this account ---------------------------------------

    [Fact]
    public async Task Startup_WithAnUndecryptableStoredToken_BlocksAndAsksForAReconnect()
    {
        SeedUndecryptableToken();

        var (module, state, client, _) = BuildModule(_ => ValidTokenResponse());

        Assert.Equal(PulsoidAuthState.Unreadable, state.PulsoidAuthState);

        await module.StartAsync();

        Assert.Equal(PulsoidAuthState.Unreadable, state.PulsoidAuthState);
        Assert.False(state.PulsoidAuthConnected);
        Assert.Equal(0, client.ConnectAttempts);
    }

    [Fact]
    public async Task Startup_WhenTheTokenCouldNotBeEncrypted_StillConnectsWithTheInMemoryToken()
    {
        // The credential is live and usable; only writing it to disk failed. Treating that as
        // "could not be decrypted" disabled heart rate for the whole session on a good token.
        var settings = new UnprotectablePulsoidSettings { AccessTokenOAuth = StoredToken };
        Assert.True(settings.TokenEncryptionFailed);
        Assert.False(settings.StoredTokenUnreadable);

        var (module, state, client, _) = BuildModule(_ => ValidTokenResponse(), settings: settings);

        await module.StartAsync();

        Assert.Equal(PulsoidAuthState.Authenticated, state.PulsoidAuthState);
        Assert.True(state.PulsoidAuthConnected);
        Assert.Equal(1, client.ConnectAttempts);
        Assert.Equal(StoredToken, client.LastToken);
    }

    // ---- a statistics failure is not an auth verdict --------------------------------------------

    [Fact]
    public async Task AStatisticsFailure_LeavesTheSignInAlone()
    {
        SeedStoredToken(StoredToken);

        var (module, state, client, _) = BuildModule(_ => ValidTokenResponse());
        await module.StartAsync();
        Assert.Equal(PulsoidAuthState.Authenticated, state.PulsoidAuthState);

        // The statistics endpoint used to raise TokenInvalid, which signed the user out of heart
        // rate while the socket was still streaming beats — and re-latched it every 30 seconds.
        client.RaiseConnectionFailed(PulsoidConnectionError.StatisticsUnavailable,
            "Pulsoid would not return heart-rate statistics for this token. Heart rate itself is unaffected.");

        Assert.Equal(PulsoidAuthState.Authenticated, state.PulsoidAuthState);
        Assert.True(state.PulsoidAuthConnected);
        Assert.False(module.PulsoidAccessError);
    }

    [Fact]
    public async Task APlanProblem_ReportsTheErrorWithoutPretendingItWillKeepRetrying()
    {
        SeedStoredToken(StoredToken);

        var (module, state, client, _) = BuildModule(_ => ValidTokenResponse());
        await module.StartAsync();

        // 402 stops the connect loop for good, so it must not land in the "unreachable, still
        // retrying" state whose status text promises the opposite.
        client.RaiseConnectionFailed(PulsoidConnectionError.SubscriptionRequired,
            "Pulsoid reports that this feature needs a paid plan, so reconnecting has stopped.");

        Assert.NotEqual(PulsoidAuthState.Unreachable, state.PulsoidAuthState);
        Assert.Equal(PulsoidAuthState.Authenticated, state.PulsoidAuthState);
        Assert.True(module.PulsoidAccessError);
        Assert.Contains("paid plan", module.PulsoidAccessErrorTxt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ARejectedTokenFromTheSocket_StillSignsTheUserOut()
    {
        SeedStoredToken(StoredToken);

        var (_, state, client, _) = BuildModule(_ => ValidTokenResponse());

        client.RaiseConnectionFailed(PulsoidConnectionError.TokenInvalid,
            "Pulsoid rejected the saved token. Please reconnect.");

        Assert.Equal(PulsoidAuthState.Rejected, state.PulsoidAuthState);
        Assert.False(state.PulsoidAuthConnected);
    }

    // ---- re-authenticating mid-outage ----------------------------------------------------------

    [Fact]
    public async Task ReAuthenticatingDuringAnOutage_RestartsTheClientWithTheNewToken()
    {
        SeedStoredToken(StoredToken);

        // Unreachable at launch: the sign-in still counts as connected, so the derived boolean
        // never changes when the user re-authenticates. The token itself has to be the trigger.
        var (module, state, client, _) = BuildModule(_ => throw new TaskCanceledException("timeout"));
        client.BlockUntilCancelled = true;

        var startup = module.StartAsync();
        await WaitFor(() => client.ConnectAttempts >= 1, "the module should connect with the stored token");
        Assert.Equal(StoredToken, client.LastToken);
        Assert.Equal(PulsoidAuthState.Unreachable, state.PulsoidAuthState);
        Assert.True(state.PulsoidAuthConnected);

        module.Settings.AccessTokenOAuth = "freshly-authenticated-token";

        await WaitFor(() => client.LastToken == "freshly-authenticated-token",
            "the retry loop must be torn down and restarted with the new token, not left holding the dead one");
        Assert.True(client.ConnectAttempts >= 2);

        await startup;
    }

    [Fact]
    public async Task DisconnectingClearsTheToken_AndStopsTheClientFromRetrying()
    {
        SeedStoredToken(StoredToken);

        var (module, state, client, _) = BuildModule(_ => throw new TaskCanceledException("timeout"));
        client.BlockUntilCancelled = true;

        var startup = module.StartAsync();
        await WaitFor(() => client.ConnectAttempts >= 1, "the module should connect with the stored token");

        module.Settings.ClearStoredToken();

        await WaitFor(() => state.PulsoidAuthState == PulsoidAuthState.NoToken,
            "clearing the credential must tear the loop down and report no token");
        Assert.Equal(string.Empty, module.Settings.AccessTokenOAuth);
        Assert.Equal(string.Empty, module.Settings.AccessTokenOAuthEncrypted);

        await startup;
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
