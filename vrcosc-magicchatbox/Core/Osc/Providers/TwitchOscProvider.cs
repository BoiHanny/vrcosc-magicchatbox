using System;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;

namespace vrcosc_magicchatbox.Core.Osc.Providers;

/// <summary>
/// Adapter: Twitch stream info → OSC segment.
/// Wraps <see cref="TwitchModule.GetOutputString"/>.
/// </summary>
public sealed class TwitchOscProvider : IOscProvider
{
    private readonly Lazy<IModuleHost> _modules;
    private readonly IntegrationSettings _intgr;

    public TwitchOscProvider(
        Lazy<IModuleHost> modules,
        ISettingsProvider<IntegrationSettings> intgrProvider)
    {
        _modules = modules;
        _intgr = intgrProvider.Value;
    }

    public string SortKey => "Twitch";
    public string UiKey => "Twitch";
    public int Priority => 50;

    public bool IsEnabledForCurrentMode(bool isVR)
        => _intgr.IntgrTwitch
           && (isVR ? _intgr.IntgrTwitch_VR : _intgr.IntgrTwitch_DESKTOP);

    public OscSegment? TryBuild(OscBuildContext context)
    {
        var twitch = _modules.Value.Twitch;
        if (twitch == null) return null;

        twitch.TriggerRefreshIfNeeded();
        string text = twitch.GetOutputString();
        if (string.IsNullOrWhiteSpace(text)) return null;

        return new OscSegment { Text = text };
    }
}
