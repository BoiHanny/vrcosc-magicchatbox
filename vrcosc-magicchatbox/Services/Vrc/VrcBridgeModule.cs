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
    private readonly AvatarSenseStore _senses = new();
    private readonly AvatarSchemaStore _schema;
    private readonly AvatarIdentityResolver _identity;
    private VrcAvatarEpoch? _epoch;

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

        _schema = new AvatarSchemaStore(
            () =>
            {
                lock (_lock) return _epoch?.Current;
            },
            () =>
            {
                lock (_lock) return _epoch?.CurrentAvatarId;
            });

        _identity = new AvatarIdentityResolver(
            () => CurrentAvatarId,
            () => _schema.Current);

    }

    public AvatarCommandReceiver Receiver => _receiver;

    public AvatarSenseStore Senses => _senses;

    public AvatarSchemaStore Schema => _schema;

    public string CurrentAvatarId
    {
        get { lock (_lock) return _epoch?.CurrentAvatarId ?? string.Empty; }
    }

    public AvatarIdentity Identity => _identity.Resolve();

    public int OscReceivePort
    {
        get { lock (_lock) return _transport?.OscReceivePort ?? 0; }
    }

    public int HttpPort
    {
        get { lock (_lock) return _transport?.HttpPort ?? 0; }
    }

    public long ParametersReceived
    {
        get { lock (_lock) return _transport?.Ingress.Counters.Parameters ?? 0; }
    }

    public IReadOnlyList<string> DescribeNeighbours()
    {
        VrcTransport? transport;
        lock (_lock) transport = _transport;

        if (transport == null)
            return Array.Empty<string>();

        try
        {
            var rows = new List<string>();
            foreach (var neighbour in transport.DescribeNeighbours())
                rows.Add(neighbour.ToString() ?? string.Empty);

            return rows;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return Array.Empty<string>();
        }
    }

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
                _senses.Clear();
                _schema.Clear();

                _transport = VrcTransport.Create(
                    new AppWorldPolicy(Settings, _currentWorld, _isPublicInstance),
                    new AppProfanityPolicy(Settings),
                    observations: new CompositeVrcObservationSink(_receiver, _senses),
                    options: options,
                    schema: _schema);

                _epoch = _transport.AvatarEpoch;
                _epoch.Invalidated += OnAvatarInvalidated;

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

    private void OnAvatarInvalidated(VrcAvatarInvalidated invalidated)
    {
        try
        {
            _senses.Clear();
            _schema.Clear();
            _receiver.ResetForNewAvatar();
            _pump.ForgetAvatar(AvatarParameterContract.IsKnownName);

            StatusMessage = string.IsNullOrEmpty(invalidated.AvatarId)
                ? "Avatar changed"
                : "Avatar changed, re-sending values";
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
    }

    private void TearDownLocked()
    {
        if (_epoch != null)
        {
            _epoch.Invalidated -= OnAvatarInvalidated;
            _epoch = null;
        }

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
