using System;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;

namespace vrcosc_magicchatbox.Core.Osc.Providers;

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

        // TikTok is the highest Priority number in the app, so it is the first segment the builder
        // throws away. Shrinking into the room that is left keeps the readout on the line instead of
        // pushing it over and losing the whole thing. The context has already paid for the separator.
        string text = tikTokLive.GetOutputString(context.RemainingCharsIf(string.Empty));
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return new OscSegment { Text = text };
    }
}
