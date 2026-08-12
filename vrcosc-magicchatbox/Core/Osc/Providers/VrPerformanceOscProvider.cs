using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Core.Osc.Providers;

public sealed class VrPerformanceOscProvider : IOscProvider
{
    private readonly IntegrationSettings _intgr;
    private readonly IntegrationDisplayState _display;

    public VrPerformanceOscProvider(
        ISettingsProvider<IntegrationSettings> intgrProvider,
        IntegrationDisplayState display)
    {
        _intgr = intgrProvider.Value;
        _display = display;
    }

    public string SortKey => "VrPerformance";
    public string UiKey => "VrPerformance";

    public int Priority => 65;

    public bool IsEnabledForCurrentMode(bool isVR)
        => _intgr.IntgrVrPerformance && isVR;

    public OscSegment? TryBuild(OscBuildContext context)
    {
        if (!_intgr.IntgrVrPerformance
            || !_display.VrPerformanceRunning
            || string.IsNullOrWhiteSpace(_display.VrPerformanceCombined))
        {
            return null;
        }

        return new OscSegment { Text = _display.VrPerformanceCombined };
    }
}
