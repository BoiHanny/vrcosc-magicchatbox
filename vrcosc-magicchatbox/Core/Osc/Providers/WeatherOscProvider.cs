using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Services;

namespace vrcosc_magicchatbox.Core.Osc.Providers;

/// <summary>
/// Adapter: Weather conditions → OSC segment.
/// Wraps <see cref="IWeatherService.BuildWeatherOnlyText"/>.
/// </summary>
public sealed class WeatherOscProvider : IOscProvider
{
    private readonly IntegrationSettings _intgr;
    private readonly WeatherSettings _ws;
    private readonly IWeatherService _weather;

    public WeatherOscProvider(
        ISettingsProvider<IntegrationSettings> intgrProvider,
        ISettingsProvider<WeatherSettings> wsProvider,
        IWeatherService weather)
    {
        _intgr = intgrProvider.Value;
        _ws = wsProvider.Value;
        _weather = weather;
    }

    public string SortKey => "Weather";
    public string UiKey => "Weather";
    public int Priority => 85;

    public bool IsEnabledForCurrentMode(bool isVR)
        => _ws.ShowWeatherInTime
           && (isVR ? _intgr.IntgrWeather_VR : _intgr.IntgrWeather_DESKTOP);

    public OscSegment? TryBuild(OscBuildContext context)
    {
        _weather.TriggerRefreshIfNeeded();
        string text = _weather.BuildWeatherOnlyText();
        if (string.IsNullOrWhiteSpace(text)) return null;

        return new OscSegment { Text = text };
    }
}
