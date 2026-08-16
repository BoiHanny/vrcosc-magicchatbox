using CommunityToolkit.Mvvm.ComponentModel;
using MagicChatbox.Vrc;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Vrc;

namespace vrcosc_magicchatbox.Services.Vrc;

public partial class VrcBridgeModule : ObservableObject, IModule
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(2);

    private readonly ISettingsProvider<VrcBridgeSettings> _settingsProvider;
    private readonly Func<string?> _currentWorld;
    private readonly Func<bool> _isPublicInstance;
    private readonly object _lock = new();

    private readonly AvatarParameterPump _pump = new();
    private readonly AvatarCommandReceiver _receiver;

    private VrcTransport? _transport;
    private CancellationTokenSource? _cts;
    private Task? _runLoop;
    private bool _disposed;

    public VrcBridgeSettings Settings => _settingsProvider.Value;

    public AvatarParameterPump Pump => _pump;

    [ObservableProperty] private string _statusMessage = "Not started";

    public string Name => "VrcBridge";
    public bool IsEnabled { get; set; } = true;
    public bool IsRunning { get; private set; }

    public IVrcEgress? Egress
    {
        get { lock (_lock) return _transport?.Egress; }
    }

    public VrcBridgeModule(
        ISettingsProvider<VrcBridgeSettings> settingsProvider,
        Func<string?> currentWorld,
        Func<bool> isPublicInstance,
        IEnumerable<InboundCommand>? commands = null,
        Action<Action>? marshal = null)
    {
        _settingsProvider = settingsProvider;
        _currentWorld = currentWorld;
        _isPublicInstance = isPublicInstance;

        _receiver = new AvatarCommandReceiver(
            commands ?? Array.Empty<InboundCommand>(),
            () => Settings.EnableBridge && Settings.EnableParameterInput,
            marshal ?? (action => Task.Run(action)));
    }

    public AvatarCommandReceiver Receiver => _receiver;

    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task StartAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_disposed || IsRunning)
                return Task.CompletedTask;

            if (!Settings.EnableBridge)
            {
                StatusMessage = "Turned off";
                return Task.CompletedTask;
            }

            try
            {
                var options = new VrcTransportOptions
                {
                    ServiceName = "MagicChatbox",
                    Address = IPAddress.Loopback,
                    OscReceivePort = Math.Max(0, Settings.OscReceivePort),
                };

                _receiver.ResetForNewAvatar();

                _transport = VrcTransport.Create(
                    new AppWorldPolicy(Settings, _currentWorld, _isPublicInstance),
                    new AppProfanityPolicy(Settings),
                    observations: _receiver,
                    options: options);

                var cts = new CancellationTokenSource();
                CancellationToken token = cts.Token;
                VrcTransport transport = _transport;

                _cts = cts;
                _runLoop = Task.Run(() => RunAsync(transport, token), CancellationToken.None);

                _pump.Start(transport.Egress);

                IsRunning = true;
                StatusMessage = "Looking for VRChat";
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
                StatusMessage = "Could not start";
                TearDownLocked();
            }
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        Task? runLoop;
        CancellationTokenSource? cts;

        lock (_lock)
        {
            if (!IsRunning && _transport == null)
                return;

            cts = _cts;
            runLoop = _runLoop;
            IsRunning = false;
        }

        await _pump.StopAsync(StopTimeout).ConfigureAwait(false);

        try
        {
            cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        if (runLoop != null)
        {
            try
            {
                await runLoop.WaitAsync(StopTimeout, CancellationToken.None).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                Logging.WriteInfo("[VrcBridge] Transport did not stop within the shutdown budget.");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        lock (_lock)
        {
            TearDownLocked();
            StatusMessage = "Stopped";
        }
    }

    public void SaveSettings() => _settingsProvider.FlushPendingSave();

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            _disposed = true;
        }

        try
        {
            StopAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }

        _pump.Dispose();
    }

    private async Task RunAsync(VrcTransport transport, CancellationToken token)
    {
        try
        {
            await transport.RunAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);

            lock (_lock)
            {
                IsRunning = false;
                StatusMessage = "Stopped after an error";
            }
        }
    }

    private void TearDownLocked()
    {
        try
        {
            _transport?.Dispose();
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }

        try
        {
            _cts?.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }

        _transport = null;
        _cts = null;
        _runLoop = null;
        IsRunning = false;
    }
}
