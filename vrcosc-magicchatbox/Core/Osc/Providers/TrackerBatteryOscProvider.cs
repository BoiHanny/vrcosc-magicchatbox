using System;
using System.ComponentModel;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;

namespace vrcosc_magicchatbox.Core.Osc.Providers;

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
        _intgr.PropertyChanged += OnIntegrationSettingsChanged;
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

        _ = tracker.StartAsync();

        string text = tracker.BuildChatboxString();
        if (string.IsNullOrWhiteSpace(text)) return null;

        return new OscSegment { Text = text };
    }

    private void OnIntegrationSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(IntegrationSettings.IntgrTrackerBattery), StringComparison.Ordinal))
            return;

        if (_intgr.IntgrTrackerBattery)
            return;

        _ = _modules.Value.TrackerBattery?.StopAsync();
    }
}
