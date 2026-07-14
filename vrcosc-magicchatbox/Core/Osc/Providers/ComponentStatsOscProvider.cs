using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Core.Osc.Providers;

/// <summary>
/// Adapter: CPU/GPU/RAM stats → OSC segment.
/// Reads pre-formatted text from <see cref="IntegrationDisplayState.ComponentStatCombined"/>.
/// </summary>
public sealed class ComponentStatsOscProvider : IOscProvider
{
    private readonly IntegrationSettings _intgr;
    private readonly IntegrationDisplayState _display;

    public ComponentStatsOscProvider(
        ISettingsProvider<IntegrationSettings> intgrProvider,
        IntegrationDisplayState display)
    {
        _intgr = intgrProvider.Value;
        _display = display;
    }

    public string SortKey => "Component";
    public string UiKey => "ComponentStat";
    public int Priority => 70;

    public bool IsEnabledForCurrentMode(bool isVR)
        => _intgr.IntgrComponentStats && (isVR ? _intgr.IntgrComponentStats_VR : _intgr.IntgrComponentStats_DESKTOP);

    public OscSegment? TryBuild(OscBuildContext context)
    {
        if (!_intgr.IntgrComponentStats
            || string.IsNullOrEmpty(_display.ComponentStatCombined)
            || !_display.ComponentStatsRunning)
            return null;

        return new OscSegment { Text = _display.ComponentStatCombined };
    }
}
