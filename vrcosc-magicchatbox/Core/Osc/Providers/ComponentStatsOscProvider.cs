using System;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Core.Osc.Providers;

public sealed class ComponentStatsOscProvider : IOscProvider
{
    private readonly Lazy<IModuleHost> _modules;
    private readonly IntegrationSettings _intgr;
    private readonly IntegrationDisplayState _display;

    public ComponentStatsOscProvider(
        Lazy<IModuleHost> modules,
        ISettingsProvider<IntegrationSettings> intgrProvider,
        IntegrationDisplayState display)
    {
        _modules = modules;
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

        var stats = _modules.Value.ComponentStats;
        if (stats == null)
            return null;

        // An empty candidate measures the room left over including the separator this segment is
        // about to need, so the writer must not subtract it a second time. With every option on
        // this readout runs to well over the whole line on its own; it now writes what fits.
        string text = stats.WriteWithin(context.RemainingCharsIf(string.Empty));
        if (string.IsNullOrEmpty(text))
            return null;

        return new OscSegment { Text = text };
    }
}
