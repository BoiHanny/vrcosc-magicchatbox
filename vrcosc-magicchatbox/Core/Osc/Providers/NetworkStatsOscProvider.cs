using System;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Core.Osc.Providers;

/// <summary>
/// Adapter: Network upload/download stats → OSC segment.
/// Wraps <see cref="NetworkStatisticsModule.GenerateDescription"/>.
/// </summary>
public sealed class NetworkStatsOscProvider : IOscProvider
{
    private readonly Lazy<NetworkStatisticsModule> _netStats;
    private readonly IntegrationSettings _intgr;

    public NetworkStatsOscProvider(
        Lazy<NetworkStatisticsModule> netStats,
        ISettingsProvider<IntegrationSettings> intgrProvider)
    {
        _netStats = netStats;
        _intgr = intgrProvider.Value;
    }

    public string SortKey => "Network";
    public string UiKey => "NetworkStatistics";
    public int Priority => 80;

    public bool IsEnabledForCurrentMode(bool isVR)
        => isVR ? _intgr.IntgrNetworkStatistics_VR : _intgr.IntgrNetworkStatistics_DESKTOP;

    public OscSegment? TryBuild(OscBuildContext context)
    {
        if (!_intgr.IntgrNetworkStatistics) return null;

        string text = _netStats.Value.GenerateDescription();
        if (string.IsNullOrEmpty(text)) return null;

        return new OscSegment { Text = text };
    }
}
