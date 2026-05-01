using System;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;

namespace vrcosc_magicchatbox.Core.Osc.Providers;

/// <summary>
/// Adapter: Spotify Web API playback state -> independent OSC segment.
/// </summary>
public sealed class SpotifyOscProvider : IOscProvider
{
    private readonly Lazy<IModuleHost> _modules;
    private readonly IntegrationSettings _integrationSettings;

    public SpotifyOscProvider(
        Lazy<IModuleHost> modules,
        ISettingsProvider<IntegrationSettings> integrationSettingsProvider)
    {
        _modules = modules;
        _integrationSettings = integrationSettingsProvider.Value;
    }

    public string SortKey => "Spotify";
    public string UiKey => "Spotify";
    public int Priority => 25;

    public bool IsEnabledForCurrentMode(bool isVR)
        => _integrationSettings.IntgrSpotify
           && (isVR ? _integrationSettings.IntgrSpotify_VR : _integrationSettings.IntgrSpotify_DESKTOP);

    public OscSegment? TryBuild(OscBuildContext context)
    {
        var spotify = _modules.Value.Spotify;
        if (spotify == null)
            return null;

        spotify.TriggerRefreshIfNeeded();
        string text = spotify.BuildOutputString(context);
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return new OscSegment { Text = text };
    }
}
