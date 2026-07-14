using System.Collections.Generic;
using vrcosc_magicchatbox.Classes.Modules;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Provides weather data retrieval, formatting, and refresh management.
/// </summary>
public interface IWeatherService
{
    WeatherSettings Settings { get; }
    void SaveSettings();
    void TriggerRefreshIfNeeded();
    void TriggerManualRefresh();
    string BuildTimeWeatherText(string timeText);
    string BuildWeatherOnlyText();
    IReadOnlyDictionary<int, string> GetDefaultConditionMap();
    IReadOnlyDictionary<int, string> GetDefaultConditionIconMap();
}
