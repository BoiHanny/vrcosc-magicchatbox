using System;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Core.Osc.Providers;

public sealed class SpotifyOscProvider : IOscProvider
{
    private readonly Lazy<IModuleHost> _modules;
    private readonly IntegrationSettings _integrationSettings;
    private readonly SpotifySettings _settings;
    private readonly SpotifyDisplayState _display;

    public SpotifyOscProvider(
        Lazy<IModuleHost> modules,
        ISettingsProvider<IntegrationSettings> integrationSettingsProvider,
        ISettingsProvider<SpotifySettings> settingsProvider,
        SpotifyDisplayState display)
    {
        _modules = modules;
        _integrationSettings = integrationSettingsProvider.Value;
        _settings = settingsProvider.Value;
        _display = display;
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

        if (context.AllowExternalRefresh)
            spotify.TriggerRefreshIfNeeded();

        if (!TransientWindow.ShouldShow(
                _settings.ShowOnlyOnChange,
                _display.LastTrackChangeUtc,
                DateTime.UtcNow,
                _settings.TransientDuration))
            return null;

        string text = spotify.BuildOutputString(context);
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return new OscSegment { Text = text };
    }
}
