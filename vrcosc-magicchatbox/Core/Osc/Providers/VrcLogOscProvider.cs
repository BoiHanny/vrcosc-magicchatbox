using System;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Services;

namespace vrcosc_magicchatbox.Core.Osc.Providers;

/// <summary>
/// Adapter: VRChat log radar data → OSC segment.
/// Wraps <see cref="VrcLogModule.GetOutputString"/>.
/// </summary>
public sealed class VrcLogOscProvider : IOscProvider
{
    private readonly Lazy<IModuleHost> _modules;
    private readonly IntegrationSettings _intgr;

    public VrcLogOscProvider(
        Lazy<IModuleHost> modules,
        ISettingsProvider<IntegrationSettings> intgrProvider)
    {
        _modules = modules;
        _intgr = intgrProvider.Value;
    }

    public string SortKey => "VrcRadar";
    public string UiKey => "VrcRadar";
    public int Priority => 35;

    public bool IsEnabledForCurrentMode(bool isVR)
        => _intgr.IntgrVrcRadar
           && (isVR ? _intgr.IntgrVrcRadar_VR : _intgr.IntgrVrcRadar_DESKTOP);

    public OscSegment? TryBuild(OscBuildContext context)
    {
        var radar = _modules.Value.VrcRadar;
        if (radar == null || !((IModule)radar).IsRunning) return null;

        string? text = radar.GetOutputString();
        if (string.IsNullOrWhiteSpace(text)) return null;

        return new OscSegment { Text = text };
    }
}
