using System;
using System.Threading;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Services.Scope;

public sealed class ScopeService : IDisposable
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(1);

    private readonly ScopeFactSource _facts;
    private readonly ScopeRuntime _runtime;
    private readonly ISettingsProvider<ScopeSettings> _settingsProvider;
    private readonly Func<Services.Vrc.VrcBridgeModule> _bridge;
    private readonly Func<VrcLogModule> _radar;
    private readonly Services.Vrc.AvatarPresetAutopilot _autopilot;

    private Timer _timer;
    private bool _started;
    private bool _disposed;
    private Core.Vrc.AvatarSchemaStore _watchedSchema;
    private VrcLogModule _watchedRadar;
    private bool _bridgeWasRunning;

    public ScopeService(
        ScopeFactSource facts,
        ScopeRuntime runtime,
        ISettingsProvider<ScopeSettings> settingsProvider,
        Func<Services.Vrc.VrcBridgeModule> bridge,
        Func<VrcLogModule> radar,
        Services.Vrc.AvatarPresetAutopilot autopilot = null)
    {
        _autopilot = autopilot;
        _facts = facts ?? throw new ArgumentNullException(nameof(facts));
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _radar = radar ?? throw new ArgumentNullException(nameof(radar));
    }

    public void Start()
    {
        if (_started || _disposed)
            return;

        _started = true;

        _runtime.SyncGroups();
        _settingsProvider.SettingsChanged += OnSettingsChanged;

        _timer = new Timer(_ => Sample(), null, TimeSpan.Zero, SampleInterval);
    }

    private void OnSettingsChanged(object sender, EventArgs e)
    {
        _runtime.SyncGroups();
        _runtime.Evaluate();
    }

    private void Sample()
    {
        if (_disposed)
            return;

        try
        {
            Subscribe();
            NoteBridgeRestart();
            _facts.Refresh();

            if (_runtime.IsUnsettled)
                _runtime.Evaluate();
        }
        catch (Exception ex)
        {
            Classes.DataAndSecurity.Logging.WriteException(ex, MSGBox: false);
        }
    }

    private void NoteBridgeRestart()
    {
        bool running = _bridge()?.IsRunning == true;

        if (running && !_bridgeWasRunning)
            _autopilot?.ForgetAvatar();

        _bridgeWasRunning = running;
    }

    private void Subscribe()
    {
        Core.Vrc.AvatarSchemaStore schema = _bridge()?.Schema;
        if (schema != null && !ReferenceEquals(schema, _watchedSchema))
        {
            if (_watchedSchema != null)
                _watchedSchema.SchemaChanged -= OnSchemaChanged;

            schema.SchemaChanged += OnSchemaChanged;
            _watchedSchema = schema;
        }

        VrcLogModule radar = _radar();
        if (radar != null && !ReferenceEquals(radar, _watchedRadar))
        {
            if (_watchedRadar != null)
                _watchedRadar.OnInstanceChanged -= OnInstanceChanged;

            radar.OnInstanceChanged += OnInstanceChanged;
            _watchedRadar = radar;
        }
    }

    private void OnSchemaChanged(Core.Vrc.AvatarSchemaSnapshot snapshot)
    {
        _facts.Refresh();

        if (_autopilot == null)
            return;

        try
        {
            Services.Vrc.VrcBridgeModule bridge = _bridge();
            if (bridge is { IsRunning: true })
                _autopilot.OnSchema(bridge.CurrentAvatarId, snapshot, bridge.Pump);
        }
        catch (Exception ex)
        {
            Classes.DataAndSecurity.Logging.WriteException(ex, MSGBox: false);
        }
    }

    private void OnInstanceChanged() => _facts.Refresh();

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _settingsProvider.SettingsChanged -= OnSettingsChanged;

        if (_watchedSchema != null)
            _watchedSchema.SchemaChanged -= OnSchemaChanged;

        if (_watchedRadar != null)
            _watchedRadar.OnInstanceChanged -= OnInstanceChanged;

        _timer?.Dispose();
        _timer = null;
    }
}
