using System;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;

namespace vrcosc_magicchatbox.Core.Osc.Providers;

/// <summary>
/// Adapter: Pulsoid heart rate → OSC segment.
/// Wraps <see cref="PulsoidModule.GetHeartRateString"/>.
/// </summary>
public sealed class HeartRateOscProvider : IOscProvider
{
    private readonly Lazy<IModuleHost> _modules;
    private readonly IntegrationSettings _intgr;
    private readonly IAppState _appState;

    public HeartRateOscProvider(
        Lazy<IModuleHost> modules,
        ISettingsProvider<IntegrationSettings> intgrProvider,
        IAppState appState)
    {
        _modules = modules;
        _intgr = intgrProvider.Value;
        _appState = appState;
    }

    public string SortKey => "HeartRate";
    public string UiKey => "HeartRate";
    public int Priority => 40;

    public bool IsEnabledForCurrentMode(bool isVR)
        => _appState.PulsoidAuthConnected
           && (isVR
               ? _intgr.IntgrHeartRate_VR
               : _intgr.IntgrHeartRate_DESKTOP);

    public OscSegment? TryBuild(OscBuildContext context)
    {
        string output = _modules.Value.Pulsoid.GetHeartRateString();
        if (string.IsNullOrEmpty(output)) return null;

        return new OscSegment { Text = output };
    }
}
