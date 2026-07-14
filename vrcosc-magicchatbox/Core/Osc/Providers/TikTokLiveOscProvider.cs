using System;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;

namespace vrcosc_magicchatbox.Core.Osc.Providers;

/// <summary>
/// Adapter: TikTok profile summary / optional LIVE overlay text -> OSC segment.
/// </summary>
public sealed class TikTokLiveOscProvider : IOscProvider
{
    private readonly Lazy<IModuleHost> _modules;
    private readonly IntegrationSettings _integrationSettings;

    public TikTokLiveOscProvider(
        Lazy<IModuleHost> modules,
        ISettingsProvider<IntegrationSettings> integrationSettingsProvider)
    {
        _modules = modules;
        _integrationSettings = integrationSettingsProvider.Value;
    }

    public string SortKey => "TikTokLive";
    public string UiKey => "TikTokLive";
    public int Priority => 52;

    public bool IsEnabledForCurrentMode(bool isVR)
        => _integrationSettings.IntgrTikTokLive
           && (isVR ? _integrationSettings.IntgrTikTokLive_VR : _integrationSettings.IntgrTikTokLive_DESKTOP);

    public OscSegment? TryBuild(OscBuildContext context)
    {
        var tikTokLive = _modules.Value.TikTokLive;
        if (tikTokLive == null)
            return null;

        string text = tikTokLive.GetOutputString();
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return new OscSegment { Text = text };
    }
}
