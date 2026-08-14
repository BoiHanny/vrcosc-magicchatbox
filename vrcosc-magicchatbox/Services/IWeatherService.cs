using System.Collections.Generic;
using vrcosc_magicchatbox.Classes.Modules;

namespace vrcosc_magicchatbox.Services;

public interface IWeatherService
{
    WeatherSettings Settings { get; }
    void SaveSettings();
    void TriggerRefreshIfNeeded();
    void TriggerManualRefresh();
    string BuildTimeWeatherText(string timeText);
    string BuildWeatherOnlyText();

    /// <summary>
    /// The same line, built from fixed plausible readings instead of live ones, so the settings
    /// page can show what a template will produce before any weather has been fetched.
    /// </summary>
    string BuildSampleWeatherText();
    IReadOnlyDictionary<int, string> GetDefaultConditionMap();
    IReadOnlyDictionary<int, string> GetDefaultConditionIconMap();
}
