using System;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;

namespace vrcosc_magicchatbox.Core.Osc.Providers;

/// <summary>
/// Adapter: SteamVR tracker battery levels → OSC segment.
/// Wraps <see cref="TrackerBatteryModule.BuildChatboxString"/>.
/// </summary>
public sealed class TrackerBatteryOscProvider : IOscProvider
{
    private readonly Lazy<IModuleHost> _modules;
    private readonly IntegrationSettings _intgr;

    public TrackerBatteryOscProvider(
        Lazy<IModuleHost> modules,
        ISettingsProvider<IntegrationSettings> intgrProvider)
    {
        _modules = modules;
        _intgr = intgrProvider.Value;
    }

    public string SortKey => "TrackerBattery";
    public string UiKey => "TrackerBattery";
    public int Priority => 60;

    public bool IsEnabledForCurrentMode(bool isVR)
        => _intgr.IntgrTrackerBattery && isVR;

    public OscSegment? TryBuild(OscBuildContext context)
    {
        var tracker = _modules.Value.TrackerBattery;
        if (tracker == null) return null;

        string text = tracker.BuildChatboxString();
        if (string.IsNullOrWhiteSpace(text)) return null;

        return new OscSegment { Text = text };
    }
}
