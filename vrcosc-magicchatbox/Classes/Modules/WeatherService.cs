using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Utilities;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Service that fetches weather data from the Open-Meteo API and formats it for display,
/// supporting city name, GPS coordinates, and IP-based location lookup.
/// </summary>
public class WeatherService : IWeatherService
{
    private const string DefaultCityName = "London";
    private const double DefaultCityLatitude = 51.5074;
    private const double DefaultCityLongitude = -0.1278;
    private const int DefaultRefreshMinutes = 10;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISettingsProvider<WeatherSettings> _settingsProvider;
    private readonly IntegrationDisplayState _integrationDisplay;
    private readonly ComponentStatsSettings _componentStatsSettings;
    private readonly TimeSettings _timeSettings;
    private readonly ITimeFormattingService _timeFormatting;

    private readonly IUiDispatcher _dispatcher;
    private HttpClient _client;
    private HttpClient Client => _client ??= _httpClientFactory.CreateClient("Weather");
    private readonly object SyncLock = new object();

    private IntegrationDisplayState IntDisplay => _integrationDisplay;

    public WeatherSettings Settings => _settingsProvider.Value;

    private TimeSettings TS => _timeSettings;
    public void SaveSettings() => _settingsProvider.Save();

    private bool _fetchInProgress;
    private DateTime _lastFetchUtc = DateTime.MinValue;
    private DateTime _lastSuccessUtc = DateTime.MinValue;
    private DateTime _lastLocationFetchUtc = DateTime.MinValue;
    private WeatherLocation _location;
    private WeatherSnapshot _snapshot;
    private string _locationCacheKey;
    private readonly IReadOnlyDictionary<int, string> DefaultConditionMap = new Dictionary<int, string>
    {
        { 0, "Clear" },
        { 1, "Mostly clear" },
        { 2, "Partly cloudy" },
        { 3, "Overcast" },
        { 45, "Fog" },
        { 48, "Fog" },
        { 51, "Drizzle" },
        { 53, "Drizzle" },
        { 55, "Drizzle" },
        { 56, "Freezing drizzle" },
        { 57, "Freezing drizzle" },
        { 61, "Rain" },
        { 63, "Rain" },
        { 65, "Rain" },
        { 66, "Freezing rain" },
        { 67, "Freezing rain" },
        { 71, "Snow" },
        { 73, "Snow" },
        { 75, "Snow" },
        { 77, "Snow grains" },
        { 80, "Showers" },
        { 81, "Showers" },
        { 82, "Showers" },
        { 85, "Snow showers" },
        { 86, "Snow showers" },
        { 95, "Thunderstorm" },
        { 96, "Hailstorm" },
        { 99, "Hailstorm" }
    };
    private readonly IReadOnlyDictionary<int, string> DefaultConditionIconMap = new Dictionary<int, string>
    {
        { 0, "☀" },
        { 1, "🌤" },
        { 2, "⛅" },
        { 3, "☁" },
        { 45, "🌫" },
        { 48, "🌫" },
        { 51, "🌦" },
        { 53, "🌦" },
        { 55, "🌦" },
        { 56, "🌧" },
        { 57, "🌧" },
        { 61, "🌧" },
        { 63, "🌧" },
        { 65, "🌧" },
        { 66, "🌧" },
        { 67, "🌧" },
        { 71, "❄" },
        { 73, "❄" },
        { 75, "❄" },
        { 77, "❄" },
        { 80, "🌧" },
        { 81, "🌧" },
        { 82, "🌧" },
        { 85, "🌨" },
        { 86, "🌨" },
        { 95, "⛈" },
        { 96, "⛈" },
        { 99, "⛈" }
    };

    public WeatherService(
        IHttpClientFactory httpClientFactory,
        ISettingsProvider<WeatherSettings> settingsProvider,
        ISettingsProvider<TimeSettings> timeSettingsProvider,
        IntegrationDisplayState integrationDisplay,
        ISettingsProvider<ComponentStatsSettings> componentStatsSettingsProvider,
        IUiDispatcher dispatcher,
        ITimeFormattingService timeFormatting)
    {
        _httpClientFactory = httpClientFactory;
        _settingsProvider = settingsProvider;
        _timeSettings = timeSettingsProvider.Value;
        _integrationDisplay = integrationDisplay;
        _componentStatsSettings = componentStatsSettingsProvider.Value;
        _dispatcher = dispatcher;
        _timeFormatting = timeFormatting;
        Client.DefaultRequestHeaders.UserAgent.ParseAdd("vrcosc-magicchatbox");
    }

    public void TriggerRefreshIfNeeded()
    {
        if (!Settings.ShowWeatherInTime)
        {
            return;
        }

        StartRefresh(force: false);
    }

    public void TriggerManualRefresh()
    {
        if (!Settings.ShowWeatherInTime)
        {
            return;
        }

        StartRefresh(force: true);
    }

    private void StartRefresh(bool force)
    {
        int refreshMinutes = Settings.WeatherUpdateIntervalMinutes;
        if (refreshMinutes <= 0)
        {
            refreshMinutes = DefaultRefreshMinutes;
        }

        lock (SyncLock)
        {
            if (_fetchInProgress)
            {
                return;
            }

            if (!force && DateTime.UtcNow - _lastFetchUtc < TimeSpan.FromMinutes(refreshMinutes))
            {
                return;
            }

            _fetchInProgress = true;
            _lastFetchUtc = DateTime.UtcNow;
        }

        _ = Task.Run(RefreshAsync);
    }

    public string BuildTimeWeatherText(string timeText)
    {
        if (!Settings.ShowWeatherInTime)
        {
            return timeText;
        }

        string template = NormalizeTemplate(Settings.WeatherTemplate);
        bool hasTemplate = !string.IsNullOrWhiteSpace(template);
        WeatherTokens tokens = BuildWeatherTokens(hasTemplate);
        if (tokens == null)
        {
            if (Settings.WeatherFallbackMode != WeatherFallbackMode.ShowNA)
            {
                return timeText;
            }

            tokens = WeatherTokens.CreatePlaceholder("N/A");
        }

        if (hasTemplate)
        {
            return ApplyTemplate(template, timeText, tokens);
        }

        if (string.IsNullOrWhiteSpace(tokens.Weather))
        {
            return timeText;
        }

        string separator = GetSeparator();
        bool timeFirst = Settings.WeatherOrder == WeatherOrder.TimeFirst;
        return timeFirst ? $"{timeText}{separator}{tokens.Weather}" : $"{tokens.Weather}{separator}{timeText}";
    }

    public string BuildWeatherOnlyText()
    {
        if (!Settings.ShowWeatherInTime)
        {
            return string.Empty;
        }

        string template = NormalizeTemplate(Settings.WeatherTemplate);
        bool hasTemplate = !string.IsNullOrWhiteSpace(template);
        WeatherTokens tokens = BuildWeatherTokens(hasTemplate);
        if (tokens == null)
        {
            if (Settings.WeatherFallbackMode != WeatherFallbackMode.ShowNA)
            {
                return string.Empty;
            }

            tokens = WeatherTokens.CreatePlaceholder("N/A");
        }

        if (hasTemplate)
        {
            string timeText = _timeFormatting.GetFormattedCurrentTime();
            return ApplyTemplate(template, timeText, tokens);
        }

        return tokens.Weather ?? string.Empty;
    }

    private WeatherTokens BuildWeatherTokens(bool ignoreCustomSeparators)
    {
        WeatherSnapshot snapshot;
        lock (SyncLock)
        {
            snapshot = _snapshot;
        }

        if (snapshot == null)
        {
            return null;
        }

        string unit = ResolveUnit();
        bool useFahrenheit = unit == "F";

        string tempValueRaw = FormatTemperature(useFahrenheit ? snapshot.TemperatureC * 9 / 5 + 32 : snapshot.TemperatureC);
        string feelsValueRaw = string.Empty;
        if (Settings.ShowWeatherFeelsLike && snapshot.FeelsLikeC.HasValue)
        {
            double feelsConverted = useFahrenheit ? snapshot.FeelsLikeC.Value * 9 / 5 + 32 : snapshot.FeelsLikeC.Value;
            feelsValueRaw = FormatTemperature(feelsConverted);
        }

        string conditionText = GetConditionText(snapshot);
        string humidityText = Settings.ShowWeatherHumidity && snapshot.HumidityPercent.HasValue
            ? $"{Math.Round(snapshot.HumidityPercent.Value)}"
            : string.Empty;
        string windText = string.Empty;
        if (Settings.ShowWeatherWind && snapshot.WindSpeedKph.HasValue)
        {
            string windUnit = ResolveWindUnit();
            double windValue = ConvertWindSpeed(snapshot.WindSpeedKph.Value, windUnit);
            string windValueRaw = FormatWindSpeed(windValue);
            windText = $"{windValueRaw}{ToSmallText(windUnit)}";
        }

        string tempValue = tempValueRaw;
        string unitText = ToSmallText(unit);
        string tempWithUnit = $"{tempValueRaw}{unitText}";
        string feelsValue = string.IsNullOrWhiteSpace(feelsValueRaw) ? string.Empty : $"{feelsValueRaw}{unitText}";
        string conditionSmall = ToSmallTextPreserveEmoji(conditionText);
        string humiditySmall = string.IsNullOrWhiteSpace(humidityText) ? string.Empty : humidityText;
        string windSmall = string.IsNullOrWhiteSpace(windText) ? string.Empty : windText;

        string weatherText = BuildWeatherText(tempWithUnit, conditionSmall, feelsValue, windSmall, humiditySmall, ignoreCustomSeparators);

        return new WeatherTokens(
            tempValue,
            unitText,
            tempWithUnit,
            conditionSmall,
            humiditySmall,
            windSmall,
            feelsValue,
            weatherText);
    }

    private string BuildWeatherText(string tempWithUnit, string condition, string feels, string wind, string humidity, bool ignoreCustomSeparators)
    {
        var primaryParts = new List<string>();
        var secondaryParts = new List<string>();
        if (TryExtractConditionIcon(condition, out string iconPrefix, out string conditionText) &&
            !string.IsNullOrWhiteSpace(tempWithUnit))
        {
            tempWithUnit = $"{iconPrefix} {tempWithUnit}";
            condition = conditionText;
        }
        if (!string.IsNullOrWhiteSpace(tempWithUnit))
        {
            primaryParts.Add(tempWithUnit);
        }
        if (!string.IsNullOrWhiteSpace(condition))
        {
            primaryParts.Add(condition);
        }
        if (Settings.ShowWeatherFeelsLike && !string.IsNullOrWhiteSpace(feels))
        {
            secondaryParts.Add($"{ToSmallText("Feels")} {feels}");
        }
        if (Settings.ShowWeatherWind && !string.IsNullOrWhiteSpace(wind))
        {
            secondaryParts.Add($"{ToSmallText("Wind")} {wind}");
        }
        if (Settings.ShowWeatherHumidity && !string.IsNullOrWhiteSpace(humidity))
        {
            secondaryParts.Add($"{ToSmallText("Hum")} {humidity}");
        }

        string separator = ignoreCustomSeparators ? " " : GetWeatherStatsSeparator();
        string primaryLine = string.Join(separator, primaryParts);
        string secondaryLine = string.Join(separator, secondaryParts);

        if (Settings.WeatherLayoutMode == WeatherLayoutMode.TwoLines &&
            !string.IsNullOrWhiteSpace(primaryLine) &&
            !string.IsNullOrWhiteSpace(secondaryLine))
        {
            return $"{primaryLine}\n{secondaryLine}";
        }

        if (string.IsNullOrWhiteSpace(primaryLine))
        {
            return secondaryLine;
        }

        if (string.IsNullOrWhiteSpace(secondaryLine))
        {
            return primaryLine;
        }

        return string.Join(separator, primaryParts.Concat(secondaryParts));
    }

    private string FormatTemperature(double value)
    {
        return Settings.WeatherUseDecimal
            ? value.ToString("0.0", CultureInfo.InvariantCulture)
            : Math.Round(value).ToString("0", CultureInfo.InvariantCulture);
    }

    private string FormatWindSpeed(double value)
    {
        return Settings.WeatherUseDecimal
            ? value.ToString("0.0", CultureInfo.InvariantCulture)
            : Math.Round(value).ToString("0", CultureInfo.InvariantCulture);
    }

    private double ConvertWindSpeed(double speedKph, string unit)
    {
        return unit == "mph" ? speedKph * 0.621371 : speedKph;
    }

    private string ResolveWindUnit()
    {
        return Settings.WeatherWindUnitOverride switch
        {
            WeatherWindUnitOverride.KilometersPerHour => "km/h",
            WeatherWindUnitOverride.MilesPerHour => "mph",
            _ => ResolveUnit() == "F" ? "mph" : "km/h"
        };
    }

    private string GetWeatherStatsSeparator()
    {
        string separator = Settings.WeatherStatsSeparator;
        return string.IsNullOrWhiteSpace(separator) ? " " : separator;
    }

    private string FormatLastSync(DateTime utc)
    {
        if (utc == DateTime.MinValue)
        {
            return "Last sync: Never";
        }

        DateTime local = utc.ToLocalTime();
        string format = TS.Time24H ? "HH:mm" : "h:mm tt";
        return $"Last sync: {local.ToString(format, CultureInfo.CurrentCulture)}";
    }

    private void UpdateLastSync(DateTime utc)
    {
        _lastSuccessUtc = utc;
        string display = FormatLastSync(utc);
        if (_dispatcher.CheckAccess())
        {
            IntDisplay.WeatherLastSyncDisplay = display;
            return;
        }

        _dispatcher.Invoke(() => IntDisplay.WeatherLastSyncDisplay = display);
    }

    private string NormalizeTemplate(string template)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return string.Empty;
        }

        return template.Replace("\\n", "\n").Replace("\\r", "\r");
    }

    private string ApplyTemplate(string template, string timeText, WeatherTokens tokens)
    {
        return template
            .Replace("{time}", timeText ?? string.Empty)
            .Replace("{weather}", tokens.Weather ?? string.Empty)
            .Replace("{temp}", tokens.Temp ?? string.Empty)
            .Replace("{unit}", tokens.Unit ?? string.Empty)
            .Replace("{tempWithUnit}", tokens.TempWithUnit ?? string.Empty)
            .Replace("{condition}", tokens.Condition ?? string.Empty)
            .Replace("{humidity}", tokens.Humidity ?? string.Empty)
            .Replace("{wind}", tokens.Wind ?? string.Empty)
            .Replace("{feels}", tokens.FeelsLike ?? string.Empty);
    }

    private string ToSmallText(string text)
    {
        return string.IsNullOrWhiteSpace(text) ? string.Empty : TextUtilities.TransformToSuperscript(text);
    }

    private string ToSmallTextPreserveEmoji(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        if (!char.IsLetterOrDigit(text[0]))
        {
            int spaceIndex = text.IndexOf(' ');
            if (spaceIndex > 0)
            {
                string prefix = text.Substring(0, spaceIndex);
                string rest = text.Substring(spaceIndex + 1);
                string smallRest = ToSmallText(rest);
                return string.IsNullOrWhiteSpace(smallRest) ? prefix : $"{prefix} {smallRest}";
            }

            return text;
        }

        return ToSmallText(text);
    }

    private bool TryExtractConditionIcon(string condition, out string iconPrefix, out string conditionText)
    {
        iconPrefix = string.Empty;
        conditionText = condition ?? string.Empty;
        if (string.IsNullOrWhiteSpace(conditionText))
        {
            return false;
        }

        if (!char.IsLetterOrDigit(conditionText[0]))
        {
            int spaceIndex = conditionText.IndexOf(' ');
            if (spaceIndex > 0)
            {
                iconPrefix = conditionText.Substring(0, spaceIndex);
                conditionText = conditionText.Substring(spaceIndex + 1).Trim();
            }
            else
            {
                iconPrefix = conditionText;
                conditionText = string.Empty;
            }

            return !string.IsNullOrWhiteSpace(iconPrefix);
        }

        return false;
    }

    private string ResolveUnit()
    {
        return Settings.WeatherUnitOverride switch
        {
            WeatherUnitOverride.Celsius => "C",
            WeatherUnitOverride.Fahrenheit => "F",
            _ => _componentStatsSettings.TemperatureUnit
        };
    }

    private string GetSeparator()
    {
        if (Settings.WeatherLayoutMode == WeatherLayoutMode.TwoLines)
        {
            return "\n";
        }

        return string.IsNullOrWhiteSpace(Settings.WeatherSeparator) ? " | " : Settings.WeatherSeparator;
    }

    private string GetConditionText(WeatherSnapshot snapshot)
    {
        string condition = Settings.ShowWeatherCondition ? MapWeatherCode(snapshot.WeatherCode) : string.Empty;
        if (!Settings.ShowWeatherEmoji)
        {
            return condition;
        }

        string emoji = MapWeatherEmoji(snapshot.WeatherCode);
        if (string.IsNullOrWhiteSpace(emoji))
        {
            return condition;
        }

        return string.IsNullOrWhiteSpace(condition) ? emoji : $"{emoji} {condition}";
    }

    private async Task RefreshAsync()
    {
        try
        {
            WeatherLocation location = await GetLocationAsync().ConfigureAwait(false);
            if (location == null)
            {
                return;
            }

            string url = BuildWeatherUrl(location.Latitude, location.Longitude);
            string response = await Client.GetStringAsync(url).ConfigureAwait(false);
            var json = JObject.Parse(response);
            var current = json["current"];
            if (current == null)
            {
                return;
            }

            double? temperatureC = current.Value<double?>("temperature_2m");
            int? weatherCode = current.Value<int?>("weather_code");
            double? humidity = current.Value<double?>("relative_humidity_2m");
            double? windSpeed = current.Value<double?>("wind_speed_10m");
            double? feelsLike = current.Value<double?>("apparent_temperature");
            if (!temperatureC.HasValue)
            {
                return;
            }

            int weatherCodeValue = weatherCode ?? -1;
            string condition = weatherCodeValue >= 0 ? MapWeatherCode(weatherCodeValue) : string.Empty;

            lock (SyncLock)
            {
                _snapshot = new WeatherSnapshot(
                    temperatureC.Value,
                    condition,
                    weatherCodeValue,
                    humidity,
                    windSpeed,
                    feelsLike);
            }

            UpdateLastSync(DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
        finally
        {
            lock (SyncLock)
            {
                _fetchInProgress = false;
            }
        }
    }

    private async Task<WeatherLocation> GetLocationAsync()
    {
        string cacheKey = BuildLocationCacheKey();
        lock (SyncLock)
        {
            if (_location != null &&
                _locationCacheKey == cacheKey &&
                DateTime.UtcNow - _lastLocationFetchUtc < Core.Constants.WeatherCacheExpiry)
            {
                return _location;
            }
        }

        WeatherLocation location = await ResolveLocationAsync().ConfigureAwait(false);
        if (location == null)
        {
            return null;
        }

        lock (SyncLock)
        {
            _location = location;
            _locationCacheKey = cacheKey;
            _lastLocationFetchUtc = DateTime.UtcNow;
        }

        return location;
    }

    private string BuildLocationCacheKey()
    {
        return Settings.WeatherLocationMode switch
        {
            WeatherLocationMode.CustomCoordinates => $"coords:{Settings.WeatherLocationLatitude.ToString(CultureInfo.InvariantCulture)},{Settings.WeatherLocationLongitude.ToString(CultureInfo.InvariantCulture)}",
            WeatherLocationMode.CustomCity => $"city:{Settings.WeatherLocationCity}",
            WeatherLocationMode.IPBased => Settings.WeatherAllowIPLocation ? "ip:true" : $"ip-fallback:{Settings.WeatherLocationCity}",
            _ => $"city:{Settings.WeatherLocationCity}"
        };
    }

    private async Task<WeatherLocation> ResolveLocationAsync()
    {
        switch (Settings.WeatherLocationMode)
        {
            case WeatherLocationMode.CustomCoordinates:
                return BuildLocationFromCoordinates(Settings.WeatherLocationLatitude, Settings.WeatherLocationLongitude);
            case WeatherLocationMode.CustomCity:
                return await BuildLocationFromCityAsync(Settings.WeatherLocationCity).ConfigureAwait(false);
            case WeatherLocationMode.IPBased:
                if (!Settings.WeatherAllowIPLocation)
                {
                    return await BuildLocationFromCityAsync(Settings.WeatherLocationCity).ConfigureAwait(false);
                }
                return await BuildLocationFromIPAsync().ConfigureAwait(false);
            default:
                return await BuildLocationFromCityAsync(Settings.WeatherLocationCity).ConfigureAwait(false);
        }
    }

    private WeatherLocation BuildLocationFromCoordinates(double latitude, double longitude)
    {
        if (!IsValidCoordinate(latitude, longitude))
        {
            return null;
        }

        return new WeatherLocation(latitude, longitude);
    }

    private async Task<WeatherLocation> BuildLocationFromCityAsync(string cityName)
    {
        string city = string.IsNullOrWhiteSpace(cityName) ? DefaultCityName : cityName.Trim();
        if (string.Equals(city, DefaultCityName, StringComparison.OrdinalIgnoreCase))
        {
            return BuildLocationFromCoordinates(DefaultCityLatitude, DefaultCityLongitude);
        }
        string url = $"{Core.Constants.OpenMeteoGeocodingUrl}?name={Uri.EscapeDataString(city)}&count=1&language=en&format=json";
        string response = await Client.GetStringAsync(url).ConfigureAwait(false);
        var json = JObject.Parse(response);
        var result = json["results"]?.First;
        if (result == null)
        {
            return null;
        }

        double? latitude = result.Value<double?>("latitude");
        double? longitude = result.Value<double?>("longitude");
        if (!latitude.HasValue || !longitude.HasValue)
        {
            return null;
        }

        return new WeatherLocation(latitude.Value, longitude.Value);
    }

    private async Task<WeatherLocation> BuildLocationFromIPAsync()
    {
        string response = await Client.GetStringAsync(Core.Constants.IpGeoLocationUrl).ConfigureAwait(false);
        var json = JObject.Parse(response);
        double? latitude = json.Value<double?>("latitude");
        double? longitude = json.Value<double?>("longitude");
        if (!latitude.HasValue || !longitude.HasValue)
        {
            return null;
        }

        return new WeatherLocation(latitude.Value, longitude.Value);
    }

    private bool IsValidCoordinate(double latitude, double longitude)
    {
        return latitude >= -90 && latitude <= 90 && longitude >= -180 && longitude <= 180;
    }

    private string BuildWeatherUrl(double latitude, double longitude)
    {
        string lat = latitude.ToString(CultureInfo.InvariantCulture);
        string lon = longitude.ToString(CultureInfo.InvariantCulture);
        return $"{Core.Constants.OpenMeteoForecastUrl}?latitude={lat}&longitude={lon}&current=temperature_2m,weather_code,relative_humidity_2m,wind_speed_10m,apparent_temperature&temperature_unit=celsius&timezone=auto";
    }

    public IReadOnlyDictionary<int, string> GetDefaultConditionMap()
    {
        return DefaultConditionMap;
    }

    public IReadOnlyDictionary<int, string> GetDefaultConditionIconMap()
    {
        return DefaultConditionIconMap;
    }

    private string MapWeatherCode(int code)
    {
        if (!DefaultConditionMap.TryGetValue(code, out string defaultValue))
        {
            defaultValue = string.Empty;
        }

        return ApplyConditionOverride(code, defaultValue);
    }

    private string ApplyConditionOverride(int code, string defaultValue)
    {
        if (!Settings.WeatherCustomOverridesEnabled)
        {
            return defaultValue;
        }

        string overrides = Settings.WeatherConditionOverrides;
        if (string.IsNullOrWhiteSpace(overrides))
        {
            return defaultValue;
        }

        if (TryGetConditionOverride(overrides, code, out string iconOverride, out string textOverride) &&
            !string.IsNullOrWhiteSpace(textOverride))
        {
            return textOverride;
        }

        return defaultValue;
    }

    private string ApplyConditionIconOverride(int code, string defaultIcon)
    {
        if (!Settings.WeatherCustomOverridesEnabled)
        {
            return defaultIcon;
        }

        string overrides = Settings.WeatherConditionOverrides;
        if (string.IsNullOrWhiteSpace(overrides))
        {
            return defaultIcon;
        }

        if (TryGetConditionOverride(overrides, code, out string iconOverride, out string textOverride) &&
            !string.IsNullOrWhiteSpace(iconOverride))
        {
            return iconOverride;
        }

        return defaultIcon;
    }

    private bool TryGetConditionOverride(string overrides, int code, out string iconOverride, out string textOverride)
    {
        iconOverride = string.Empty;
        textOverride = string.Empty;
        var separators = new[] { '\n', ';' };
        var entries = overrides.Split(separators, StringSplitOptions.RemoveEmptyEntries);
        foreach (string entry in entries)
        {
            string trimmed = entry.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            int separatorIndex = trimmed.IndexOf('=');
            if (separatorIndex < 0)
            {
                separatorIndex = trimmed.IndexOf(':');
            }

            if (separatorIndex < 0)
            {
                continue;
            }

            string codeText = trimmed.Substring(0, separatorIndex).Trim();
            if (!int.TryParse(codeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedCode))
            {
                continue;
            }

            string value = trimmed.Substring(separatorIndex + 1).Trim();
            if (parsedCode == code)
            {
                int iconSeparatorIndex = value.IndexOf('|');
                if (iconSeparatorIndex >= 0)
                {
                    iconOverride = value.Substring(0, iconSeparatorIndex).Trim();
                    textOverride = value.Substring(iconSeparatorIndex + 1).Trim();
                }
                else
                {
                    textOverride = value;
                }

                return !string.IsNullOrWhiteSpace(iconOverride) || !string.IsNullOrWhiteSpace(textOverride);
            }
        }

        return false;
    }

    private string MapWeatherEmoji(int code)
    {
        if (!DefaultConditionIconMap.TryGetValue(code, out string defaultIcon))
        {
            defaultIcon = string.Empty;
        }

        return ApplyConditionIconOverride(code, defaultIcon);
    }

    private sealed class WeatherTokens
    {
        public WeatherTokens(string weather)
        {
            Weather = weather;
        }

        public WeatherTokens(
            string temp,
            string unit,
            string tempWithUnit,
            string condition,
            string humidity,
            string wind,
            string feelsLike,
            string weather)
        {
            Temp = temp;
            Unit = unit;
            TempWithUnit = tempWithUnit;
            Condition = condition;
            Humidity = humidity;
            Wind = wind;
            FeelsLike = feelsLike;
            Weather = weather;
        }

        public static WeatherTokens CreatePlaceholder(string weather)
        {
            return new WeatherTokens(weather);
        }

        public string Temp { get; }
        public string Unit { get; }
        public string TempWithUnit { get; }
        public string Condition { get; }
        public string Humidity { get; }
        public string Wind { get; }
        public string FeelsLike { get; }
        public string Weather { get; }
    }

    private sealed class WeatherLocation
    {
        public WeatherLocation(double latitude, double longitude)
        {
            Latitude = latitude;
            Longitude = longitude;
        }

        public double Latitude { get; }
        public double Longitude { get; }
    }

    private sealed class WeatherSnapshot
    {
        public WeatherSnapshot(
            double temperatureC,
            string condition,
            int weatherCode,
            double? humidityPercent,
            double? windSpeedKph,
            double? feelsLikeC)
        {
            TemperatureC = temperatureC;
            Condition = condition;
            WeatherCode = weatherCode;
            HumidityPercent = humidityPercent;
            WindSpeedKph = windSpeedKph;
            FeelsLikeC = feelsLikeC;
        }

        public double TemperatureC { get; }
        public string Condition { get; }
        public int WeatherCode { get; }
        public double? HumidityPercent { get; }
        public double? WindSpeedKph { get; }
        public double? FeelsLikeC { get; }
    }
}
