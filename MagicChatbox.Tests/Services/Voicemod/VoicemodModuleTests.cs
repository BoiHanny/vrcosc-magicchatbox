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

    private static (VoicemodModule Module, VoicemodDisplayState Display) CreateModule(
        IVoicemodSocketFactory socketFactory)
    {
        var display = new VoicemodDisplayState();
        var settings = new IntegrationSettings { IntgrVoicemod = true };
        var module = new VoicemodModule(
            new StubSettingsProvider<IntegrationSettings>(settings),
            display,
            new StubClientKeyProvider(),
            socketFactory,
            new InlineDispatcher(),
            new ApprovedConsentService());
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
        public bool TryGetClientKey(out string clientKey)
        {
            clientKey = "test-client-key";
            return true;
        }
    }

    private sealed class ApprovedConsentService : IPrivacyConsentService
    {
        public event EventHandler<ConsentChangedEventArgs>? ConsentChanged;

        public bool IsApproved(PrivacyHook hook) => hook == PrivacyHook.VoicemodControl;
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

        public List<string> SentMessages { get; } = new();
        public Uri? ConnectedUri { get; private set; }
        public WebSocketState State { get; private set; } = WebSocketState.None;

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
            if (ReadAction(message) != "registerClient")
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
