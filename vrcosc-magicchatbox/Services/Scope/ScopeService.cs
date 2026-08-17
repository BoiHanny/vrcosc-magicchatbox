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

    private Timer _timer;
    private bool _started;
    private bool _disposed;
    private Core.Vrc.AvatarSchemaStore _watchedSchema;
    private VrcLogModule _watchedRadar;

    public ScopeService(
        ScopeFactSource facts,
        ScopeRuntime runtime,
        ISettingsProvider<ScopeSettings> settingsProvider,
        Func<Services.Vrc.VrcBridgeModule> bridge,
        Func<VrcLogModule> radar)
    {
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

        _settingsProvider.SettingsChanged += OnSettingsChanged;
        _runtime.SyncGroups();

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
            _facts.Refresh();

            if (_runtime.IsUnsettled)
                _runtime.Evaluate();
        }
        catch (Exception ex)
        {
            Classes.DataAndSecurity.Logging.WriteException(ex, MSGBox: false);
        }
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

    private void OnSchemaChanged(Core.Vrc.AvatarSchemaSnapshot snapshot) => _facts.Refresh();

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
