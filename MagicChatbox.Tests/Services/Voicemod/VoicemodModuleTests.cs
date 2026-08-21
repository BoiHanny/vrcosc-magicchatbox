using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.Modules.Voicemod;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services.Voicemod;
using vrcosc_magicchatbox.ViewModels.State;
using Xunit;

namespace MagicChatbox.Tests.Services.Voicemod;

public sealed class VoicemodModuleTests
{
    [Fact]
    public async Task RegistrationIsFirst_ThenTheClientSynchronizesAllControlState()
    {
        var socket = ScriptedSocket.Authorized();
        var (module, display) = CreateModule(new QueueSocketFactory(socket));

        await module.StartAsync();
        await WaitForStateAsync(display, VoicemodConnectionState.Connected);

        string[] actions = socket.SentMessages.Select(ReadAction).ToArray();
        Assert.Equal("registerClient", actions[0]);
        Assert.Contains("getVoices", actions);
        Assert.Contains("getAllSoundboard", actions);
        Assert.Contains("getVoiceChangerStatus", actions);
        Assert.Contains("getCurrentVoice", actions);

        await module.StopAsync();
        module.Dispose();
    }

    [Fact]
    public async Task UnauthorizedClient_DoesNotSendControlCommandsOrRetryForever()
    {
        var socket = ScriptedSocket.Unauthorized();
        var (module, display) = CreateModule(new QueueSocketFactory(socket));

        await module.StartAsync();
        await WaitForStateAsync(display, VoicemodConnectionState.Unauthorized);

        Assert.Equal(new[] { "registerClient" }, socket.SentMessages.Select(ReadAction));

        await module.StopAsync();
        module.Dispose();
    }

    [Fact]
    public async Task PortDiscovery_AdvancesToTheNextOfficialPort()
    {
        var refused = ScriptedSocket.Refused();
        var accepted = ScriptedSocket.Authorized();
        var factory = new QueueSocketFactory(refused, accepted);
        var (module, display) = CreateModule(factory);

        await module.StartAsync();
        await WaitForStateAsync(display, VoicemodConnectionState.Connected);

        Assert.Equal(59129, refused.ConnectedUri!.Port);
        Assert.Equal(20000, accepted.ConnectedUri!.Port);
        Assert.Equal(20000, display.ConnectedPort);

        await module.StopAsync();
        module.Dispose();
    }

    [Fact]
    public async Task BleepRelease_WaitsForPressAndAlwaysSendsTheStopValueLast()
    {
        var socket = ScriptedSocket.Authorized(sendDelay: TimeSpan.FromMilliseconds(30));
        var (module, display) = CreateModule(new QueueSocketFactory(socket));

        await module.StartAsync();
        await WaitForStateAsync(display, VoicemodConnectionState.Connected);

        Task press = module.SetBleepAsync(true);
        Task release = module.SetBleepAsync(false);
        await Task.WhenAll(press, release);

        JsonElement[] bleepPayloads = socket.SentMessages
            .Where(message => ReadAction(message) == "setBeepSound")
            .Select(ReadPayload)
            .ToArray();

        Assert.Equal(2, bleepPayloads.Length);
        Assert.Equal(1, bleepPayloads[0].GetProperty("badLanguage").GetInt32());
        Assert.Equal(0, bleepPayloads[1].GetProperty("badLanguage").GetInt32());
        Assert.False(display.IsBleeping);

        await module.StopAsync();
        module.Dispose();
    }

    [Fact]
    public async Task StopThenStart_IsSerializedSoTheOldStopCannotResetTheNewConnection()
    {
        var first = ScriptedSocket.Authorized(closeDelay: TimeSpan.FromMilliseconds(100));
        var second = ScriptedSocket.Authorized();
        var (module, display) = CreateModule(new QueueSocketFactory(first, second));

        await module.StartAsync();
        await WaitForStateAsync(display, VoicemodConnectionState.Connected);

        Task stop = module.StopAsync();
        await Task.Delay(10);
        Task start = module.StartAsync();
        await Task.WhenAll(stop, start);
        await WaitForStateAsync(display, VoicemodConnectionState.Connected);

        Assert.Equal("registerClient", ReadAction(second.SentMessages[0]));
        Assert.True(display.IsConnected);

        await module.StopAsync();
        module.Dispose();
    }

    [Fact]
    public async Task Stop_WaitsForAnInFlightBleepPressAndSendsReleaseBeforeClosing()
    {
        var socket = ScriptedSocket.Authorized(sendDelay: TimeSpan.FromMilliseconds(40));
        var (module, display) = CreateModule(new QueueSocketFactory(socket));

        await module.StartAsync();
        await WaitForStateAsync(display, VoicemodConnectionState.Connected);

        Task press = module.SetBleepAsync(true);
        await Task.Delay(5);
        Task stop = module.StopAsync();
        await Task.WhenAll(press, stop);

        JsonElement[] bleepPayloads = socket.SentMessages
            .Where(message => ReadAction(message) == "setBeepSound")
            .Select(ReadPayload)
            .ToArray();

        Assert.Equal(2, bleepPayloads.Length);
        Assert.Equal(1, bleepPayloads[0].GetProperty("badLanguage").GetInt32());
        Assert.Equal(0, bleepPayloads[1].GetProperty("badLanguage").GetInt32());
        Assert.Equal(WebSocketState.Closed, socket.State);

        module.Dispose();
    }

    [Fact]
    public async Task PlayingAndStoppingASound_UpdatesTheChatboxPlaybackState()
    {
        var socket = ScriptedSocket.Authorized();
        var (module, display) = CreateModule(new QueueSocketFactory(socket));

        await module.StartAsync();
        await WaitForStateAsync(display, VoicemodConnectionState.Connected);

        await module.PlaySoundAsync("sound-id", "Air horn");

        Assert.Equal("Air horn", display.LastPlayedSoundName);
        Assert.NotEqual(default, display.LastSoundPlaybackStartedUtc);
        Assert.Contains(socket.SentMessages, message => ReadAction(message) == "playMeme");

        await module.StopAllSoundsAsync();

        Assert.Empty(display.LastPlayedSoundName);
        Assert.Equal(default, display.LastSoundPlaybackStartedUtc);
        Assert.Contains(socket.SentMessages, message => ReadAction(message) == "stopAllMemeSounds");

        await module.StopAsync();
        module.Dispose();
    }

    [Fact]
    public async Task SynchronizeResponses_PopulateTheCatalogEndToEnd()
    {
        var socket = ScriptedSocket.Authorized()
            .Replying("getVoices", """
                {
                  "actionType": "getVoices",
                  "actionObject": {
                    "currentVoice": "robot",
                    "voices": [
                      { "id": "robot", "friendlyName": "Robot", "enabled": true },
                      { "id": "cave", "friendlyName": "Cave", "enabled": false }
                    ]
                  }
                }
                """)
            .Replying("getAllSoundboard", """
                {
                  "actionType": "getAllSoundboard",
                  "actionObject": {
                    "soundboards": [
                      {
                        "id": "board-1",
                        "name": "Favourites",
                        "enabled": true,
                        "sounds": [ { "id": "airhorn", "name": "Air horn", "enabled": true } ]
                      }
                    ]
                  }
                }
                """)
            .Replying("getUserLicense", """
                { "actionType": "getUserLicense", "actionObject": { "licenseType": "pro" } }
                """);

        var (module, display) = CreateModule(new QueueSocketFactory(socket));

        await module.StartAsync();
        await WaitForStateAsync(display, VoicemodConnectionState.Connected);
        await WaitForAsync(() => display.Voices.Count == 2 && display.Soundboards.Count == 1);

        Assert.Equal("robot", display.CurrentVoiceId);
        Assert.Equal("Robot", display.CurrentVoiceName);
        Assert.Equal("pro", display.LicenseType);
        Assert.Single(display.Soundboards[0].Sounds);
        Assert.False(display.IsFreeLicense);

        await module.StopAsync();
        module.Dispose();
    }

    [Fact]
    public async Task UnsolicitedEvents_UpdateTheLiveSwitches()
    {
        var socket = ScriptedSocket.Authorized();
        var (module, display) = CreateModule(new QueueSocketFactory(socket));

        await module.StartAsync();
        await WaitForStateAsync(display, VoicemodConnectionState.Connected);

        await socket.PushAsync("""{ "actionType": "voiceChangerEnabledEvent" }""");
        await WaitForAsync(() => display.VoiceChangerEnabled);

        await socket.PushAsync("""{ "actionType": "badLanguageEnabledEvent" }""");
        await WaitForAsync(() => display.IsBleeping);

        await socket.PushAsync("""
            { "actionType": "toggleMuteMic", "actionObject": { "value": true } }
            """);
        await WaitForAsync(() => display.MicrophoneMuted);

        await module.StopAsync();
        module.Dispose();
    }

    [Fact]
    public async Task DisabledFeature_DropsItsEventsAndSkipsItsQueries()
    {
        VoicemodSettings features = AllFeatures();
        features.SoundboardControlEnabled = false;
        var socket = ScriptedSocket.Authorized();
        var (module, display) = CreateModule(
            new QueueSocketFactory(socket), out _, out _, out _, features);

        await module.StartAsync();
        await WaitForStateAsync(display, VoicemodConnectionState.Connected);

        string[] actions = socket.SentMessages.Select(ReadAction).ToArray();
        Assert.DoesNotContain("getAllSoundboard", actions);
        Assert.DoesNotContain("getMemes", actions);
        Assert.Contains("getVoices", actions);

        await socket.PushAsync("""
            { "actionType": "toggleMuteMemeForMe", "actionObject": { "value": true } }
            """);
        await Task.Delay(60);
        Assert.False(display.SoundboardMutedForMe);

        await module.StopAsync();
        module.Dispose();
    }

    [Fact]
    public async Task ServerHangUp_ClearsTheCatalogAndReconnects()
    {
        var first = ScriptedSocket.Authorized();
        var second = ScriptedSocket.Authorized();
        var (module, display) = CreateModule(new QueueSocketFactory(first, second));

        await module.StartAsync();
        await WaitForStateAsync(display, VoicemodConnectionState.Connected);

        await first.PushAsync("""
            {
              "actionType": "getVoices",
              "actionObject": { "currentVoice": "robot", "voices": [ { "id": "robot", "friendlyName": "Robot", "enabled": true } ] }
            }
            """);
        await WaitForAsync(() => display.Voices.Count == 1);

        first.HangUp();

        // A stale list read as live is the specific failure here, so assert the clear, not just the state.
        await WaitForAsync(() => display.Voices.Count == 0);
        await WaitForStateAsync(display, VoicemodConnectionState.Reconnecting);

        await module.StopAsync();
        module.Dispose();
    }

    [Fact]
    public async Task TurningTheIntegrationOffMidConnection_DisconnectsCleanly()
    {
        var socket = ScriptedSocket.Authorized();
        var (module, display) = CreateModule(
            new QueueSocketFactory(socket), out IntegrationSettings integrations, out _, out _);

        await module.StartAsync();
        await WaitForStateAsync(display, VoicemodConnectionState.Connected);

        integrations.IntgrVoicemod = false;
        module.PropertyChangedHandler(
            integrations,
            new System.ComponentModel.PropertyChangedEventArgs(nameof(IntegrationSettings.IntgrVoicemod)));

        await WaitForStateAsync(display, VoicemodConnectionState.Disabled);
        Assert.False(module.IsRunning);
        Assert.Empty(display.Voices);

        module.Dispose();
    }

    [Fact]
    public async Task RevokingConsentMidConnection_StopsAndReportsPermissionRequired()
    {
        var socket = ScriptedSocket.Authorized();
        var (module, display) = CreateModule(
            new QueueSocketFactory(socket), out _, out _, out ApprovedConsentService consent);

        await module.StartAsync();
        await WaitForStateAsync(display, VoicemodConnectionState.Connected);

        consent.Revoke();

        await WaitForStateAsync(display, VoicemodConnectionState.PermissionRequired);
        Assert.False(module.IsRunning);

        module.Dispose();
    }

    [Fact]
    public async Task EveryOfficialPortIsTried_BeforeGivingUp()
    {
        ScriptedSocket[] refused = VoicemodProtocol.Ports
            .Select(_ => ScriptedSocket.Refused())
            .ToArray();
        var (module, display) = CreateModule(new QueueSocketFactory(refused));

        await module.StartAsync();
        await WaitForStateAsync(display, VoicemodConnectionState.Reconnecting);

        Assert.Equal(
            VoicemodProtocol.Ports.ToArray(),
            refused.Select(socket => socket.ConnectedUri!.Port).ToArray());

        await module.StopAsync();
        module.Dispose();
    }

    [Fact]
    public async Task DisposeIsIdempotent()
    {
        var socket = ScriptedSocket.Authorized();
        var (module, display) = CreateModule(new QueueSocketFactory(socket));

        await module.StartAsync();
        await WaitForStateAsync(display, VoicemodConnectionState.Connected);
        await module.StopAsync();

        module.Dispose();
        module.Dispose();
    }

    [Fact]
    public async Task BitmapResponse_LandsInTheArtworkCache()
    {
        // A 1x1 PNG - the smallest thing that proves the base64 actually decoded.
        const string OnePixelPng =
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

        var artwork = new VoicemodArtworkCache();
        var socket = ScriptedSocket.Authorized();
        var (module, display) = CreateModule(
            new QueueSocketFactory(socket), out _, out _, out _, artwork: artwork);

        await module.StartAsync();
        await WaitForStateAsync(display, VoicemodConnectionState.Connected);

        await socket.PushAsync($$"""
            { "actionType": "getBitmap", "actionObject": { "voiceID": "robot", "result": "{{OnePixelPng}}" } }
            """);

        await WaitForAsync(() => artwork.Contains("voice", "robot"));
        Assert.NotNull(artwork.Get("voice", "robot"));

        await module.StopAsync();
        module.Dispose();
    }

    [Fact]
    public void BuildSynchronizeActions_AsksOnlyForWhatIsSwitchedOn()
    {
        var soundboardOnly = new VoicemodSettings
        {
            VoiceControlEnabled = false,
            MicControlEnabled = false,
        };

        IReadOnlyList<string> actions = VoicemodModule.BuildSynchronizeActions(soundboardOnly);

        Assert.Contains("getAllSoundboard", actions);
        Assert.Contains("getMemes", actions);
        Assert.DoesNotContain("getVoices", actions);
        Assert.DoesNotContain("getMuteMicStatus", actions);
        Assert.Contains("getUserLicense", actions);
    }

    [Theory]
    [InlineData("getVoices", true, false, false, true)]
    [InlineData("getVoices", false, true, true, false)]
    [InlineData("getAllSoundboard", false, true, false, true)]
    [InlineData("toggleMuteMic", false, false, true, true)]
    [InlineData("badLanguageEnabledEvent", false, false, false, false)]
    [InlineData("getUserLicense", false, false, false, true)]
    public void IsActionEnabled_FollowsTheFeatureSwitches(
        string action,
        bool voice,
        bool soundboard,
        bool mic,
        bool expected)
    {
        var features = new VoicemodSettings
        {
            VoiceControlEnabled = voice,
            SoundboardControlEnabled = soundboard,
            MicControlEnabled = mic,
        };

        Assert.Equal(expected, VoicemodModule.IsActionEnabled(action, features));
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (!condition())
            await Task.Delay(10, timeout.Token);
    }

    private static VoicemodSettings AllFeatures() => new()
    {
        VoiceControlEnabled = true,
        SoundboardControlEnabled = true,
        MicControlEnabled = true,
    };

    private static (VoicemodModule Module, VoicemodDisplayState Display) CreateModule(
        IVoicemodSocketFactory socketFactory)
        => CreateModule(socketFactory, out _, out _, out _, AllFeatures());

    // The out parameters are what lets a test reach in and flip a setting, revoke consent, or read
    // the artwork cache while the module is mid-connection - none of which the module exposes.
    private static (VoicemodModule Module, VoicemodDisplayState Display) CreateModule(
        IVoicemodSocketFactory socketFactory,
        out IntegrationSettings integrationSettings,
        out VoicemodSettings features,
        out ApprovedConsentService consent,
        VoicemodSettings? initialFeatures = null,
        IVoicemodArtworkCache? artwork = null)
    {
        var display = new VoicemodDisplayState();
        integrationSettings = new IntegrationSettings { IntgrVoicemod = true };
        features = initialFeatures ?? new VoicemodSettings();
        consent = new ApprovedConsentService();
        var module = new VoicemodModule(
            new StubSettingsProvider<IntegrationSettings>(integrationSettings),
            new StubSettingsProvider<VoicemodSettings>(features),
            display,
            new StubClientKeyProvider(),
            socketFactory,
            new InlineDispatcher(),
            consent,
            artwork ?? new VoicemodArtworkCache());
        return (module, display);
    }

    private static async Task WaitForStateAsync(
        VoicemodDisplayState display,
        VoicemodConnectionState expected)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (display.ConnectionState != expected)
            await Task.Delay(10, timeout.Token);
    }

    private static string ReadAction(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("action").GetString()!;
    }

    private static JsonElement ReadPayload(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("payload").Clone();
    }

    private sealed class StubSettingsProvider<T> : ISettingsProvider<T> where T : class, new()
    {
        public T Value { get; }
        public event EventHandler? SettingsChanged;

        public StubSettingsProvider(T value) => Value = value;
        public void Save() => SettingsChanged?.Invoke(this, EventArgs.Empty);
        public void FlushPendingSave() { }
        public void Reload() { }
    }

    private sealed class StubClientKeyProvider : IVoicemodClientKeyProvider
    {
        public bool HasLocalClientKey => false;

        public bool TryGetClientKey(out string clientKey)
        {
            clientKey = "test-client-key";
            return true;
        }

        public void SaveLocalClientKey(string clientKey) { }

        public void ClearLocalClientKey() { }
    }

    private sealed class ApprovedConsentService : IPrivacyConsentService
    {
        private bool _voicemodApproved = true;

        public event EventHandler<ConsentChangedEventArgs>? ConsentChanged;

        public void Revoke()
        {
            _voicemodApproved = false;
            ConsentChanged?.Invoke(
                this,
                new ConsentChangedEventArgs(PrivacyHook.VoicemodControl, ConsentState.Denied));
        }

        public bool IsApproved(PrivacyHook hook) =>
            hook == PrivacyHook.VoicemodControl && _voicemodApproved;
        public ConsentState GetState(PrivacyHook hook) =>
            IsApproved(hook) ? ConsentState.Approved : ConsentState.Denied;
        public void Approve(PrivacyHook hook) =>
            ConsentChanged?.Invoke(this, new ConsentChangedEventArgs(hook, ConsentState.Approved));
        public void Deny(PrivacyHook hook) =>
            ConsentChanged?.Invoke(this, new ConsentChangedEventArgs(hook, ConsentState.Denied));
        public void Reset(PrivacyHook hook) =>
            ConsentChanged?.Invoke(this, new ConsentChangedEventArgs(hook, ConsentState.Unknown));
        public IReadOnlyList<PrivacyHook> GetHooksRequiringConsent(IEnumerable<PrivacyHook> hooks) =>
            hooks.Where(hook => !IsApproved(hook)).ToArray();
    }

    private sealed class InlineDispatcher : IUiDispatcher
    {
        public void Invoke(Action action) => action();
        public T Invoke<T>(Func<T> func) => func();
        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }

        public Task<T> InvokeAsync<T>(Func<T> func) => Task.FromResult(func());
        public bool CheckAccess() => true;
        public void BeginInvoke(Action action) => action();
        public void Shutdown() { }
    }

    private sealed class QueueSocketFactory : IVoicemodSocketFactory
    {
        private readonly Queue<ScriptedSocket> _sockets;

        public QueueSocketFactory(params ScriptedSocket[] sockets) =>
            _sockets = new Queue<ScriptedSocket>(sockets);

        public IVoicemodSocket Create()
        {
            if (_sockets.Count == 0)
                return ScriptedSocket.Refused();

            return _sockets.Dequeue();
        }
    }

    private sealed class ScriptedSocket : IVoicemodSocket
    {
        private readonly Channel<string> _incoming = Channel.CreateUnbounded<string>();
        private readonly int _authorizationCode;
        private readonly bool _refuseConnection;
        private readonly TimeSpan _sendDelay;
        private readonly TimeSpan _closeDelay;

        // Canned replies keyed by the outbound action that should trigger them. Without this the
        // double could only ever answer registerClient, which left the whole inbound dispatch table
        // unreachable from a module-level test.
        private readonly Dictionary<string, string> _replies =
            new(StringComparer.OrdinalIgnoreCase);

        public List<string> SentMessages { get; } = new();
        public Uri? ConnectedUri { get; private set; }
        public WebSocketState State { get; private set; } = WebSocketState.None;

        public ScriptedSocket Replying(string action, string json)
        {
            _replies[action] = json;
            return this;
        }

        /// <summary>Delivers a message the client never asked for, the way a real event arrives.</summary>
        public ValueTask PushAsync(string json) => _incoming.Writer.WriteAsync(json);

        /// <summary>Ends the receive loop the way a server-side hang-up would.</summary>
        public void HangUp()
        {
            State = WebSocketState.Closed;
            _incoming.Writer.TryComplete();
        }

        private ScriptedSocket(
            int authorizationCode,
            bool refuseConnection,
            TimeSpan sendDelay,
            TimeSpan closeDelay)
        {
            _authorizationCode = authorizationCode;
            _refuseConnection = refuseConnection;
            _sendDelay = sendDelay;
            _closeDelay = closeDelay;
        }

        public static ScriptedSocket Authorized(
            TimeSpan? sendDelay = null,
            TimeSpan? closeDelay = null) =>
            new(
                200,
                refuseConnection: false,
                sendDelay ?? TimeSpan.Zero,
                closeDelay ?? TimeSpan.Zero);

        public static ScriptedSocket Unauthorized() =>
            new(401, refuseConnection: false, TimeSpan.Zero, TimeSpan.Zero);

        public static ScriptedSocket Refused() =>
            new(0, refuseConnection: true, TimeSpan.Zero, TimeSpan.Zero);

        public Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
        {
            ConnectedUri = uri;
            if (_refuseConnection)
                throw new WebSocketException("Connection refused.");

            State = WebSocketState.Open;
            return Task.CompletedTask;
        }

        public async Task SendTextAsync(string message, CancellationToken cancellationToken)
        {
            if (_sendDelay > TimeSpan.Zero)
                await Task.Delay(_sendDelay, cancellationToken);

            SentMessages.Add(message);

            string action = ReadAction(message);
            if (_replies.TryGetValue(action, out string? reply))
                await _incoming.Writer.WriteAsync(reply, cancellationToken);

            if (action != "registerClient")
                return;

            string description = _authorizationCode == 200 ? "Authorized" : "Unauthorized";
            await _incoming.Writer.WriteAsync(
                $$"""
                  {
                    "action": "registerClient",
                    "payload": {
                      "status": {
                        "code": {{_authorizationCode}},
                        "description": "{{description}}"
                      }
                    }
                  }
                  """,
                cancellationToken);
        }

        public async Task<string?> ReceiveTextAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await _incoming.Reader.ReadAsync(cancellationToken);
            }
            catch (ChannelClosedException)
            {
                return null;
            }
        }

        public async Task CloseAsync(CancellationToken cancellationToken)
        {
            if (_closeDelay > TimeSpan.Zero)
                await Task.Delay(_closeDelay, cancellationToken);

            State = WebSocketState.Closed;
            _incoming.Writer.TryComplete();
        }

        public ValueTask DisposeAsync()
        {
            State = WebSocketState.Closed;
            _incoming.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }
}
