using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.Services.Voicemod;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Classes.Modules.Voicemod;

public sealed class VoicemodModule : IModule
{
    private static readonly TimeSpan PortConnectTimeout = TimeSpan.FromMilliseconds(900);
    private static readonly TimeSpan AuthorizationTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(1);
    private static readonly int[] ReconnectDelaySeconds = [1, 2, 4, 8, 15, 30];

    private readonly ISettingsProvider<IntegrationSettings> _integrationSettingsProvider;
    private readonly VoicemodDisplayState _display;
    private readonly IVoicemodClientKeyProvider _clientKeyProvider;
    private readonly IVoicemodSocketFactory _socketFactory;
    private readonly IUiDispatcher _dispatcher;
    private readonly IPrivacyConsentService _consentService;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly SemaphoreSlim _bleepLock = new(1, 1);
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);

    private CancellationTokenSource? _runCancellation;
    private Task? _runTask;
    private IVoicemodSocket? _socket;
    private volatile bool _authorized;
    private bool _isBleeping;
    private bool _disposed;

    private IntegrationSettings Settings => _integrationSettingsProvider.Value;

    public string Name => "Voicemod";
    public bool IsEnabled { get; set; } = true;
    public bool IsRunning => _runTask is { IsCompleted: false };
    public VoicemodDisplayState Display => _display;

    public VoicemodModule(
        ISettingsProvider<IntegrationSettings> integrationSettingsProvider,
        VoicemodDisplayState display,
        IVoicemodClientKeyProvider clientKeyProvider,
        IVoicemodSocketFactory socketFactory,
        IUiDispatcher dispatcher,
        IPrivacyConsentService consentService)
    {
        _integrationSettingsProvider = integrationSettingsProvider;
        _display = display;
        _clientKeyProvider = clientKeyProvider;
        _socketFactory = socketFactory;
        _dispatcher = dispatcher;
        _consentService = consentService;
        _consentService.ConsentChanged += OnConsentChanged;
    }

    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

    public async Task StartAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _lifecycleGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await StartCoreAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_disposed)
            return;

        await _lifecycleGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await StopCoreAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task ReconnectAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _lifecycleGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await StopCoreAsync(ct).ConfigureAwait(false);
            await StartCoreAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task StartCoreAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!Settings.IntgrVoicemod)
        {
            await SetStateAsync(
                VoicemodConnectionState.Disabled,
                "Voicemod control is off",
                clearPort: true).ConfigureAwait(false);
            return;
        }

        if (!_consentService.IsApproved(PrivacyHook.VoicemodControl))
        {
            await SetStateAsync(
                VoicemodConnectionState.PermissionRequired,
                "Permission is required before MagicChatbox can control Voicemod",
                clearPort: true).ConfigureAwait(false);
            return;
        }

        bool hasClientKey = _clientKeyProvider.TryGetClientKey(out _);
        await _dispatcher.InvokeAsync(() => _display.ClientKeyConfigured = hasClientKey).ConfigureAwait(false);
        if (!hasClientKey)
        {
            await SetStateAsync(
                VoicemodConnectionState.NotConfigured,
                "This build does not contain a Voicemod client key",
                "Set MAGICCHATBOX_VOICEMOD_CLIENT_KEY locally or inject VoicemodClientKey at build time.",
                clearPort: true).ConfigureAwait(false);
            return;
        }

        if (_runTask is { IsCompleted: false })
            return;

        _runCancellation?.Dispose();
        _runCancellation = new CancellationTokenSource();
        _runTask = Task.Run(() => RunConnectionLoopAsync(_runCancellation.Token));
    }

    private async Task StopCoreAsync(CancellationToken ct)
    {
        await _bleepLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_authorized && _isBleeping)
            {
                try
                {
                    using var bleepCancellation =
                        CancellationTokenSource.CreateLinkedTokenSource(ct);
                    bleepCancellation.CancelAfter(CloseTimeout);
                    await SendCommandCoreAsync(
                        "setBeepSound",
                        new { badLanguage = 0 },
                        requireAuthorization: true,
                        bleepCancellation.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logging.WriteInfo($"Voicemod: could not clear bleep before disconnect: {ex.Message}");
                }
            }

            _isBleeping = false;
            await _dispatcher.InvokeAsync(() => _display.IsBleeping = false).ConfigureAwait(false);
        }
        finally
        {
            _bleepLock.Release();
        }

        CancellationTokenSource? cancellation = _runCancellation;
        Task? runTask = _runTask;
        IVoicemodSocket? socket = _socket;

        cancellation?.Cancel();
        await TryCloseSocketAsync(socket).ConfigureAwait(false);

        if (runTask != null)
        {
            try
            {
                await runTask.WaitAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellation?.IsCancellationRequested == true || ct.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"Voicemod: connection loop stopped with an error: {ex.Message}");
            }
        }

        if (ReferenceEquals(_runCancellation, cancellation))
        {
            _runCancellation = null;
            _runTask = null;
        }

        cancellation?.Dispose();
        _authorized = false;

        await _dispatcher.InvokeAsync(() =>
        {
            _display.ResetSwitches();
            _display.ConnectionState = Settings.IntgrVoicemod
                ? VoicemodConnectionState.Disconnected
                : VoicemodConnectionState.Disabled;
            _display.StatusText = Settings.IntgrVoicemod
                ? "Voicemod is disconnected"
                : "Voicemod control is off";
            _display.ErrorText = string.Empty;
            _display.ConnectedPort = null;
        }).ConfigureAwait(false);
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        EnsureConnected();
        await SetStateAsync(
            VoicemodConnectionState.Synchronizing,
            "Refreshing Voicemod...",
            port: _display.ConnectedPort).ConfigureAwait(false);

        await SynchronizeAsync(ct).ConfigureAwait(false);

        await SetStateAsync(
            VoicemodConnectionState.Connected,
            ConnectedStatusText(_display.ConnectedPort),
            port: _display.ConnectedPort).ConfigureAwait(false);
    }

    public Task LoadVoiceAsync(string voiceId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(voiceId);
        return SendCommandAsync(
            "loadVoice",
            new
            {
                voiceID = voiceId,
                voiceId,
            },
            ct);
    }

    public Task SelectRandomVoiceAsync(
        VoicemodRandomVoiceMode mode,
        CancellationToken ct = default)
        => SendCommandAsync("selectRandomVoice", new { mode = mode.ToString() }, ct);

    public Task ToggleVoiceChangerAsync(CancellationToken ct = default)
        => SendCommandAsync("toggleVoiceChanger", null, ct);

    public Task ToggleHearMyselfAsync(CancellationToken ct = default)
        => SendCommandAsync("toggleHearMyVoice", null, ct);

    public Task ToggleBackgroundEffectsAsync(CancellationToken ct = default)
        => SendCommandAsync("toggleBackground", null, ct);

    public Task ToggleMicrophoneMuteAsync(CancellationToken ct = default)
        => SendCommandAsync("toggleMuteMic", null, ct);

    public Task ToggleSoundboardMuteForMeAsync(CancellationToken ct = default)
        => SendCommandAsync("toggleMuteMemeForMe", null, ct);

    public async Task SetBleepAsync(bool enabled, CancellationToken ct = default)
    {
        await _bleepLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_isBleeping == enabled)
                return;

            await SendCommandAsync(
                "setBeepSound",
                new { badLanguage = enabled ? 1 : 0 },
                ct).ConfigureAwait(false);

            _isBleeping = enabled;
            await _dispatcher.InvokeAsync(() => _display.IsBleeping = enabled).ConfigureAwait(false);
        }
        finally
        {
            _bleepLock.Release();
        }
    }

    public Task PlaySoundAsync(string soundId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(soundId);
        return SendCommandAsync(
            "playMeme",
            new
            {
                FileName = soundId,
                IsKeyDown = true,
            },
            ct);
    }

    public Task StopAllSoundsAsync(CancellationToken ct = default)
        => SendCommandAsync("stopAllMemeSounds", null, ct);

    public Task SetVoiceParameterAsync(
        VoicemodVoiceParameter parameter,
        double value,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        double clampedValue = Math.Clamp(value, parameter.Minimum, parameter.Maximum);

        return SendCommandAsync(
            "setCurrentVoiceParameter",
            new
            {
                parameterName = parameter.Key,
                parameterValue = new
                {
                    maxValue = parameter.Maximum,
                    minValue = parameter.Minimum,
                    displayNormalized = parameter.DisplayNormalized,
                    value = clampedValue,
                },
            },
            ct);
    }

    public void PropertyChangedHandler(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IntegrationSettings.IntgrVoicemod))
            HandleIntegrationToggle();
    }

    public void SaveSettings() => _integrationSettingsProvider.Save();

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _consentService.ConsentChanged -= OnConsentChanged;
        _runCancellation?.Cancel();
        _runCancellation?.Dispose();
        _bleepLock.Dispose();
        _lifecycleGate.Dispose();
        _sendLock.Dispose();
    }

    private async Task RunConnectionLoopAsync(CancellationToken ct)
    {
        int reconnectAttempt = 0;

        while (!ct.IsCancellationRequested && Settings.IntgrVoicemod)
        {
            if (!_consentService.IsApproved(PrivacyHook.VoicemodControl))
            {
                await SetStateAsync(
                    VoicemodConnectionState.PermissionRequired,
                    "Permission is required before MagicChatbox can control Voicemod",
                    clearPort: true).ConfigureAwait(false);
                return;
            }

            if (!_clientKeyProvider.TryGetClientKey(out string clientKey))
            {
                await _dispatcher.InvokeAsync(() => _display.ClientKeyConfigured = false).ConfigureAwait(false);
                await SetStateAsync(
                    VoicemodConnectionState.NotConfigured,
                    "This build does not contain a Voicemod client key",
                    "Set MAGICCHATBOX_VOICEMOD_CLIENT_KEY locally or inject VoicemodClientKey at build time.",
                    clearPort: true).ConfigureAwait(false);
                return;
            }

            await _dispatcher.InvokeAsync(() => _display.ClientKeyConfigured = true).ConfigureAwait(false);

            bool connectedThisRound = false;
            Exception? lastConnectError = null;

            foreach (int port in VoicemodProtocol.Ports)
            {
                ct.ThrowIfCancellationRequested();
                await SetStateAsync(
                    reconnectAttempt == 0
                        ? VoicemodConnectionState.Connecting
                        : VoicemodConnectionState.Reconnecting,
                    $"Looking for Voicemod on local port {port}...",
                    port: port).ConfigureAwait(false);

                await using IVoicemodSocket socket = _socketFactory.Create();
                try
                {
                    using var connectCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    connectCancellation.CancelAfter(PortConnectTimeout);
                    await socket.ConnectAsync(
                        new Uri($"ws://localhost:{port}/v1"),
                        connectCancellation.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    lastConnectError = new TimeoutException($"Port {port} did not answer in time.");
                    continue;
                }
                catch (Exception ex) when (ex is WebSocketException or IOException or InvalidOperationException)
                {
                    lastConnectError = ex;
                    continue;
                }

                connectedThisRound = true;
                reconnectAttempt = 0;
                _socket = socket;

                var authorization = new TaskCompletionSource<(int Code, string Description)>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                Task receiveTask = ReceiveLoopAsync(socket, authorization, ct);

                try
                {
                    await SetStateAsync(
                        VoicemodConnectionState.Authorizing,
                        $"Authorizing with Voicemod on port {port}...",
                        port: port).ConfigureAwait(false);

                    await SendCommandCoreAsync(
                        "registerClient",
                        new { clientKey },
                        requireAuthorization: false,
                        ct).ConfigureAwait(false);

                    (int code, string description) = await authorization.Task
                        .WaitAsync(AuthorizationTimeout, ct)
                        .ConfigureAwait(false);

                    if (code != 200)
                    {
                        await SetStateAsync(
                            VoicemodConnectionState.Unauthorized,
                            "Voicemod rejected this client key",
                            string.IsNullOrWhiteSpace(description) ? "Unauthorized" : description,
                            port: port).ConfigureAwait(false);
                        return;
                    }

                    _authorized = true;
                    await SetStateAsync(
                        VoicemodConnectionState.Synchronizing,
                        "Connected. Reading voices and soundboards...",
                        port: port).ConfigureAwait(false);

                    await SynchronizeAsync(ct).ConfigureAwait(false);

                    await SetStateAsync(
                        VoicemodConnectionState.Connected,
                        ConnectedStatusText(port),
                        port: port).ConfigureAwait(false);

                    await receiveTask.ConfigureAwait(false);
                    if (!ct.IsCancellationRequested)
                    {
                        await SetStateAsync(
                            VoicemodConnectionState.Disconnected,
                            "The Voicemod connection closed",
                            "MagicChatbox will reconnect automatically.",
                            clearPort: true).ConfigureAwait(false);
                    }
                }
                catch (TimeoutException ex)
                {
                    lastConnectError = ex;
                    Logging.WriteInfo($"Voicemod: authorization timed out on port {port}.");
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex) when (ex is WebSocketException or IOException or InvalidOperationException)
                {
                    lastConnectError = ex;
                    Logging.WriteInfo($"Voicemod: connection on port {port} ended: {ex.Message}");
                }
                finally
                {
                    _authorized = false;
                    _isBleeping = false;
                    authorization.TrySetCanceled(ct);
                    await TryCloseSocketAsync(socket).ConfigureAwait(false);

                    if (!receiveTask.IsCompleted)
                    {
                        try
                        {
                            await receiveTask.WaitAsync(CloseTimeout).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Logging.WriteInfo($"Voicemod: receive loop did not stop cleanly: {ex.Message}");
                        }
                    }
                    else if (receiveTask.IsFaulted)
                    {
                        _ = receiveTask.Exception;
                    }

                    if (ReferenceEquals(_socket, socket))
                        _socket = null;

                    await _dispatcher.InvokeAsync(() => _display.ResetSwitches()).ConfigureAwait(false);
                }

                break;
            }

            if (ct.IsCancellationRequested || !Settings.IntgrVoicemod)
                return;

            reconnectAttempt++;
            int delaySeconds = ReconnectDelaySeconds[
                Math.Min(reconnectAttempt - 1, ReconnectDelaySeconds.Length - 1)];
            string detail = connectedThisRound
                ? "The connection was lost."
                : "Voicemod was not found on any supported local port.";

            if (lastConnectError != null)
                Logging.WriteInfo($"Voicemod: {detail} Last error: {lastConnectError.Message}");

            await SetStateAsync(
                VoicemodConnectionState.Reconnecting,
                $"Voicemod unavailable. Retrying in {delaySeconds} seconds...",
                detail,
                clearPort: true).ConfigureAwait(false);

            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), ct).ConfigureAwait(false);
        }
    }

    private async Task ReceiveLoopAsync(
        IVoicemodSocket socket,
        TaskCompletionSource<(int Code, string Description)> authorization,
        CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                string? message = await socket.ReceiveTextAsync(ct).ConfigureAwait(false);
                if (message == null)
                    return;
                if (string.IsNullOrWhiteSpace(message))
                    continue;

                if (!VoicemodProtocol.TryParseEnvelope(message, out VoicemodEnvelope? envelope, out string? error)
                    || envelope == null)
                {
                    Logging.WriteInfo($"Voicemod: ignored an invalid message: {error}");
                    continue;
                }

                await HandleEnvelopeAsync(envelope, authorization, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        finally
        {
            if (!authorization.Task.IsCompleted)
            {
                authorization.TrySetException(
                    new IOException("The Voicemod connection closed before authorization completed."));
            }
        }
    }

    private async Task HandleEnvelopeAsync(
        VoicemodEnvelope envelope,
        TaskCompletionSource<(int Code, string Description)> authorization,
        CancellationToken ct)
    {
        string action = envelope.Action;

        if (string.Equals(action, "registerClient", StringComparison.OrdinalIgnoreCase))
        {
            (int Code, string Description)? status = VoicemodProtocol.ReadRegistrationStatus(envelope);
            if (status != null)
                authorization.TrySetResult(status.Value);
            else if (envelope.ActionObject.ValueKind == System.Text.Json.JsonValueKind.Object
                     || envelope.Payload.ValueKind == System.Text.Json.JsonValueKind.Object)
                authorization.TrySetResult((200, "Authorized"));

            string? licenseType = VoicemodProtocol.ReadLicenseType(envelope);
            if (!string.IsNullOrWhiteSpace(licenseType))
            {
                await _dispatcher.InvokeAsync(() => _display.LicenseType = licenseType).ConfigureAwait(false);
            }

            return;
        }

        if (!string.IsNullOrWhiteSpace(envelope.AppVersion))
        {
            await _dispatcher.InvokeAsync(() => _display.AppVersion = envelope.AppVersion).ConfigureAwait(false);
        }

        switch (action.ToLowerInvariant())
        {
            case "getuserlicense":
            case "licensetypechanged":
            case "licensetypechangedevent":
            {
                string? licenseType = VoicemodProtocol.ReadLicenseType(envelope);
                if (!string.IsNullOrWhiteSpace(licenseType))
                {
                    await _dispatcher.InvokeAsync(() =>
                    {
                        _display.LicenseType = licenseType;
                        _display.MarkSynchronized(envelope.AppVersion);
                    }).ConfigureAwait(false);
                }

                break;
            }

            case "getvoices":
            {
                var voices = VoicemodProtocol.ReadVoices(envelope);
                if (voices != null)
                {
                    await _dispatcher.InvokeAsync(() =>
                    {
                        _display.ReplaceVoices(voices.Value.Voices, voices.Value.CurrentVoiceId);
                        _display.MarkSynchronized(envelope.AppVersion);
                    }).ConfigureAwait(false);
                }

                break;
            }

            case "getallsoundboard":
            {
                IReadOnlyList<VoicemodSoundboard>? soundboards =
                    VoicemodProtocol.ReadSoundboards(envelope);
                if (soundboards != null)
                {
                    await _dispatcher.InvokeAsync(() =>
                    {
                        _display.ReplaceSoundboards(soundboards);
                        _display.MarkSynchronized(envelope.AppVersion);
                    }).ConfigureAwait(false);
                }

                break;
            }

            case "getactivesoundboardprofile":
            {
                string? soundboardId = VoicemodProtocol.ReadActiveSoundboardId(envelope);
                if (!string.IsNullOrWhiteSpace(soundboardId))
                {
                    await _dispatcher.InvokeAsync(() =>
                    {
                        _display.ActiveSoundboardId = soundboardId;
                        _display.MarkSynchronized(envelope.AppVersion);
                    }).ConfigureAwait(false);
                }

                break;
            }

            case "getcurrentvoice":
            case "setcurrentvoiceparameter":
            case "parameterschangedevent":
            case "parameterchangedevent":
            {
                var currentVoice = VoicemodProtocol.ReadCurrentVoice(envelope);
                if (currentVoice != null)
                {
                    await _dispatcher.InvokeAsync(() =>
                    {
                        _display.ReplaceParameters(
                            currentVoice.Value.VoiceId,
                            currentVoice.Value.Parameters);
                        _display.MarkSynchronized(envelope.AppVersion);
                    }).ConfigureAwait(false);
                }

                break;
            }

            case "voiceparameterupdated":
            {
                var update = VoicemodProtocol.ReadParameterUpdate(envelope);
                if (update != null)
                {
                    await _dispatcher.InvokeAsync(() =>
                    {
                        _display.CurrentVoiceId = update.Value.VoiceId;
                        _display.UpdateVoiceParameter(
                            update.Value.ParameterName,
                            update.Value.Value);
                        _display.MarkSynchronized(envelope.AppVersion);
                    }).ConfigureAwait(false);
                }

                break;
            }

            case "voicechangedevent":
            case "voiceloadedevent":
            {
                string? voiceId = VoicemodProtocol.ReadVoiceId(envelope);
                if (!string.IsNullOrWhiteSpace(voiceId)
                    && !string.Equals(voiceId, "custom", StringComparison.OrdinalIgnoreCase))
                {
                    await _dispatcher.InvokeAsync(() =>
                    {
                        _display.CurrentVoiceId = voiceId;
                        _display.MarkSynchronized(envelope.AppVersion);
                    }).ConfigureAwait(false);

                    if (_authorized)
                        await SendCommandCoreAsync("getCurrentVoice", null, true, ct).ConfigureAwait(false);
                }

                break;
            }

            case "togglevoicemod":
            case "togglevoicechanger":
            {
                bool? value = VoicemodProtocol.ReadBooleanValue(envelope);
                if (value != null)
                    await _dispatcher.InvokeAsync(() => _display.VoiceChangerEnabled = value.Value).ConfigureAwait(false);
                break;
            }

            case "voicechangerenabledevent":
                await _dispatcher.InvokeAsync(() => _display.VoiceChangerEnabled = true).ConfigureAwait(false);
                break;

            case "voicechangerdisabledevent":
                await _dispatcher.InvokeAsync(() => _display.VoiceChangerEnabled = false).ConfigureAwait(false);
                break;

            case "togglehearmyvoice":
            {
                bool? value = VoicemodProtocol.ReadBooleanValue(envelope);
                if (value != null)
                    await _dispatcher.InvokeAsync(() => _display.HearMyselfEnabled = value.Value).ConfigureAwait(false);
                break;
            }

            case "hearmyselfenabledevent":
                await _dispatcher.InvokeAsync(() => _display.HearMyselfEnabled = true).ConfigureAwait(false);
                break;

            case "hearmyselfdisabledevent":
                await _dispatcher.InvokeAsync(() => _display.HearMyselfEnabled = false).ConfigureAwait(false);
                break;

            case "togglebackground":
            {
                bool? value = VoicemodProtocol.ReadBooleanValue(envelope);
                if (value != null)
                    await _dispatcher.InvokeAsync(() => _display.BackgroundEffectsEnabled = value.Value).ConfigureAwait(false);
                break;
            }

            case "backgroundeffectsenabledevent":
                await _dispatcher.InvokeAsync(() => _display.BackgroundEffectsEnabled = true).ConfigureAwait(false);
                break;

            case "backgroundeffectsdisabledevent":
                await _dispatcher.InvokeAsync(() => _display.BackgroundEffectsEnabled = false).ConfigureAwait(false);
                break;

            case "togglemutemic":
            case "togglemute":
            {
                bool? value = VoicemodProtocol.ReadBooleanValue(envelope);
                if (value != null)
                    await _dispatcher.InvokeAsync(() => _display.MicrophoneMuted = value.Value).ConfigureAwait(false);
                break;
            }

            case "mutemicrophoneenabledevent":
                await _dispatcher.InvokeAsync(() => _display.MicrophoneMuted = true).ConfigureAwait(false);
                break;

            case "mutemicrophonedisabledevent":
                await _dispatcher.InvokeAsync(() => _display.MicrophoneMuted = false).ConfigureAwait(false);
                break;

            case "togglemutememeforme":
            {
                bool? value = VoicemodProtocol.ReadBooleanValue(envelope);
                if (value != null)
                    await _dispatcher.InvokeAsync(() => _display.SoundboardMutedForMe = value.Value).ConfigureAwait(false);
                break;
            }

            case "mutememeformeenabledevent":
                await _dispatcher.InvokeAsync(() => _display.SoundboardMutedForMe = true).ConfigureAwait(false);
                break;

            case "mutememeformedisabledevent":
                await _dispatcher.InvokeAsync(() => _display.SoundboardMutedForMe = false).ConfigureAwait(false);
                break;

            case "badlanguageenabledevent":
                _isBleeping = true;
                await _dispatcher.InvokeAsync(() => _display.IsBleeping = true).ConfigureAwait(false);
                break;

            case "badlanguagedisabledevent":
                _isBleeping = false;
                await _dispatcher.InvokeAsync(() => _display.IsBleeping = false).ConfigureAwait(false);
                break;
        }
    }

    private async Task SynchronizeAsync(CancellationToken ct)
    {
        string[] actions =
        [
            "getUserLicense",
            "getVoices",
            "getAllSoundboard",
            "getBackgroundEffectStatus",
            "getHearMyselfStatus",
            "getVoiceChangerStatus",
            "getMuteMemeForMeStatus",
            "getMuteMicStatus",
            "getCurrentVoice",
            "getActiveSoundboardProfile",
        ];

        foreach (string action in actions)
            await SendCommandCoreAsync(action, null, requireAuthorization: true, ct).ConfigureAwait(false);
    }

    private Task SendCommandAsync(string action, object? payload, CancellationToken ct)
    {
        EnsureConnected();
        return SendCommandCoreAsync(action, payload, requireAuthorization: true, ct);
    }

    private async Task SendCommandCoreAsync(
        string action,
        object? payload,
        bool requireAuthorization,
        CancellationToken ct)
    {
        IVoicemodSocket? socket = _socket;
        if (socket == null || socket.State != WebSocketState.Open)
            throw new InvalidOperationException("Voicemod is not connected.");
        if (requireAuthorization && !_authorized)
            throw new InvalidOperationException("Voicemod has not authorized this client yet.");

        string message = VoicemodProtocol.CreateMessage(action, payload);
        await _sendLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await socket.SendTextAsync(message, ct).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private void EnsureConnected()
    {
        if (!_authorized || _socket?.State != WebSocketState.Open)
            throw new InvalidOperationException("Voicemod is not connected.");
    }

    private async void HandleIntegrationToggle()
    {
        try
        {
            if (Settings.IntgrVoicemod)
                await StartAsync().ConfigureAwait(false);
            else
                await StopAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            await SetStateAsync(
                VoicemodConnectionState.Faulted,
                "Voicemod could not change connection state",
                ex.Message,
                clearPort: true).ConfigureAwait(false);
        }
    }

    private async void OnConsentChanged(object? sender, ConsentChangedEventArgs e)
    {
        if (e.Hook != PrivacyHook.VoicemodControl)
            return;

        try
        {
            if (e.NewState == ConsentState.Approved && Settings.IntgrVoicemod)
                await StartAsync().ConfigureAwait(false);
            else if (e.NewState != ConsentState.Approved)
            {
                await StopAsync().ConfigureAwait(false);
                await SetStateAsync(
                    VoicemodConnectionState.PermissionRequired,
                    "Permission is required before MagicChatbox can control Voicemod",
                    clearPort: true).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
    }

    private async Task SetStateAsync(
        VoicemodConnectionState state,
        string status,
        string error = "",
        int? port = null,
        bool clearPort = false)
    {
        await _dispatcher.InvokeAsync(() =>
        {
            _display.ConnectionState = state;
            _display.StatusText = status;
            _display.ErrorText = error;
            if (clearPort)
                _display.ConnectedPort = null;
            else if (port != null)
                _display.ConnectedPort = port;
        }).ConfigureAwait(false);
    }

    private static string ConnectedStatusText(int? port)
        => port == null ? "Connected to Voicemod" : $"Connected to Voicemod on port {port}";

    private static async Task TryCloseSocketAsync(IVoicemodSocket? socket)
    {
        if (socket == null)
            return;

        try
        {
            using var cancellation = new CancellationTokenSource(CloseTimeout);
            await socket.CloseAsync(cancellation.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException or InvalidOperationException)
        {
            Logging.WriteInfo($"Voicemod: socket close was not clean: {ex.Message}");
        }
    }
}
