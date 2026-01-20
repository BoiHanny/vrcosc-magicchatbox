using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.Modules;

public static class WeatherService
{
    private const string DefaultCityName = "London";
    private const double DefaultCityLatitude = 51.5074;
    private const double DefaultCityLongitude = -0.1278;
    private const int DefaultRefreshMinutes = 10;
    private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
    private static readonly object SyncLock = new object();

    private static bool _fetchInProgress;
    private static DateTime _lastFetchUtc = DateTime.MinValue;
    private static DateTime _lastSuccessUtc = DateTime.MinValue;
    private static DateTime _lastLocationFetchUtc = DateTime.MinValue;
    private static WeatherLocation _location;
    private static WeatherSnapshot _snapshot;
    private static string _locationCacheKey;
    private static readonly IReadOnlyDictionary<int, string> DefaultConditionMap = new Dictionary<int, string>
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
    private static readonly IReadOnlyDictionary<int, string> DefaultConditionIconMap = new Dictionary<int, string>
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

    static WeatherService()
    {
        Client.DefaultRequestHeaders.UserAgent.ParseAdd("vrcosc-magicchatbox");
    }

    public static void TriggerRefreshIfNeeded()
    {
        if (!ViewModel.Instance.ShowWeatherInTime)
        {
            return;
        }

        StartRefresh(force: false);
    }

    public static void TriggerManualRefresh()
    {
        if (!ViewModel.Instance.ShowWeatherInTime)
        {
            return;
        }

        StartRefresh(force: true);
    }

    private static void StartRefresh(bool force)
    {
        int refreshMinutes = ViewModel.Instance.WeatherUpdateIntervalMinutes;
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

    public static string BuildTimeWeatherText(string timeText)
    {
        if (!ViewModel.Instance.ShowWeatherInTime)
        {
            return timeText;
        }

        string template = NormalizeTemplate(ViewModel.Instance.WeatherTemplate);
        bool hasTemplate = !string.IsNullOrWhiteSpace(template);
        WeatherTokens tokens = BuildWeatherTokens(hasTemplate);
        if (tokens == null)
        {
            if (ViewModel.Instance.WeatherFallbackMode != WeatherFallbackMode.ShowNA)
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
        bool timeFirst = ViewModel.Instance.WeatherOrder == WeatherOrder.TimeFirst;
        return timeFirst ? $"{timeText}{separator}{tokens.Weather}" : $"{tokens.Weather}{separator}{timeText}";
    }

    public static string BuildWeatherOnlyText()
    {
        if (!ViewModel.Instance.ShowWeatherInTime)
        {
            return string.Empty;
        }

        string template = NormalizeTemplate(ViewModel.Instance.WeatherTemplate);
        bool hasTemplate = !string.IsNullOrWhiteSpace(template);
        WeatherTokens tokens = BuildWeatherTokens(hasTemplate);
        if (tokens == null)
        {
            if (ViewModel.Instance.WeatherFallbackMode != WeatherFallbackMode.ShowNA)
            {
                return string.Empty;
            }

            tokens = WeatherTokens.CreatePlaceholder("N/A");
        }

        if (hasTemplate)
        {
            string timeText = ComponentStatsModule.GetTime();
            return ApplyTemplate(template, timeText, tokens);
        }

        return tokens.Weather ?? string.Empty;
    }

    private static WeatherTokens BuildWeatherTokens(bool ignoreCustomSeparators)
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
        if (ViewModel.Instance.ShowWeatherFeelsLike && snapshot.FeelsLikeC.HasValue)
        {
            double feelsConverted = useFahrenheit ? snapshot.FeelsLikeC.Value * 9 / 5 + 32 : snapshot.FeelsLikeC.Value;
            feelsValueRaw = FormatTemperature(feelsConverted);
        }

        string conditionText = GetConditionText(snapshot);
        string humidityText = ViewModel.Instance.ShowWeatherHumidity && snapshot.HumidityPercent.HasValue
            ? $"{Math.Round(snapshot.HumidityPercent.Value)}"
            : string.Empty;
        string windText = string.Empty;
        if (ViewModel.Instance.ShowWeatherWind && snapshot.WindSpeedKph.HasValue)
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

    private static string BuildWeatherText(string tempWithUnit, string condition, string feels, string wind, string humidity, bool ignoreCustomSeparators)
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
        if (ViewModel.Instance.ShowWeatherFeelsLike && !string.IsNullOrWhiteSpace(feels))
        {
            secondaryParts.Add($"{ToSmallText("Feels")} {feels}");
        }
        if (ViewModel.Instance.ShowWeatherWind && !string.IsNullOrWhiteSpace(wind))
        {
            secondaryParts.Add($"{ToSmallText("Wind")} {wind}");
        }
        if (ViewModel.Instance.ShowWeatherHumidity && !string.IsNullOrWhiteSpace(humidity))
        {
            secondaryParts.Add($"{ToSmallText("Hum")} {humidity}");
        }

        string separator = ignoreCustomSeparators ? " " : GetWeatherStatsSeparator();
        string primaryLine = string.Join(separator, primaryParts);
        string secondaryLine = string.Join(separator, secondaryParts);

        if (ViewModel.Instance.WeatherLayoutMode == WeatherLayoutMode.TwoLines &&
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

    private static string FormatTemperature(double value)
    {
        return ViewModel.Instance.WeatherUseDecimal
            ? value.ToString("0.0", CultureInfo.InvariantCulture)
            : Math.Round(value).ToString("0", CultureInfo.InvariantCulture);
    }

    private static string FormatWindSpeed(double value)
    {
        return ViewModel.Instance.WeatherUseDecimal
            ? value.ToString("0.0", CultureInfo.InvariantCulture)
            : Math.Round(value).ToString("0", CultureInfo.InvariantCulture);
    }

    private static double ConvertWindSpeed(double speedKph, string unit)
    {
        return unit == "mph" ? speedKph * 0.621371 : speedKph;
    }

    private static string ResolveWindUnit()
    {
        return ViewModel.Instance.WeatherWindUnitOverride switch
        {
            WeatherWindUnitOverride.KilometersPerHour => "km/h",
            WeatherWindUnitOverride.MilesPerHour => "mph",
            _ => ResolveUnit() == "F" ? "mph" : "km/h"
        };
    }

    private static string GetWeatherStatsSeparator()
    {
        string separator = ViewModel.Instance.WeatherStatsSeparator;
        return string.IsNullOrWhiteSpace(separator) ? " " : separator;
    }

    private static string FormatLastSync(DateTime utc)
    {
        if (utc == DateTime.MinValue)
        {
            return "Last sync: Never";
        }

        DateTime local = utc.ToLocalTime();
        string format = ViewModel.Instance.Time24H ? "HH:mm" : "h:mm tt";
        return $"Last sync: {local.ToString(format, CultureInfo.CurrentCulture)}";
    }

    private static void UpdateLastSync(DateTime utc)
    {
        _lastSuccessUtc = utc;
        string display = FormatLastSync(utc);
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            ViewModel.Instance.WeatherLastSyncDisplay = display;
            return;
        }

        dispatcher.BeginInvoke(new Action(() =>
        {
            ViewModel.Instance.WeatherLastSyncDisplay = display;
        }));
    }

    private static string NormalizeTemplate(string template)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return string.Empty;
        }

        return template.Replace("\\n", "\n").Replace("\\r", "\r");
    }

    private static string ApplyTemplate(string template, string timeText, WeatherTokens tokens)
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

    private static string ToSmallText(string text)
    {
        return string.IsNullOrWhiteSpace(text) ? string.Empty : DataController.TransformToSuperscript(text);
    }

    private static string ToSmallTextPreserveEmoji(string text)
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

    private static bool TryExtractConditionIcon(string condition, out string iconPrefix, out string conditionText)
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

    private static string ResolveUnit()
    {
        return ViewModel.Instance.WeatherUnitOverride switch
        {
            WeatherUnitOverride.Celsius => "C",
            WeatherUnitOverride.Fahrenheit => "F",
            _ => ViewModel.Instance.TemperatureUnit
        };
    }

    private static string GetSeparator()
    {
        if (ViewModel.Instance.WeatherLayoutMode == WeatherLayoutMode.TwoLines)
        {
            return "\n";
        }

        return string.IsNullOrWhiteSpace(ViewModel.Instance.WeatherSeparator) ? " | " : ViewModel.Instance.WeatherSeparator;
    }

    private static string GetConditionText(WeatherSnapshot snapshot)
    {
        string condition = ViewModel.Instance.ShowWeatherCondition ? MapWeatherCode(snapshot.WeatherCode) : string.Empty;
        if (!ViewModel.Instance.ShowWeatherEmoji)
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

    private static async Task RefreshAsync()
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

    private static async Task<WeatherLocation> GetLocationAsync()
    {
        string cacheKey = BuildLocationCacheKey();
        lock (SyncLock)
        {
            if (_location != null &&
                _locationCacheKey == cacheKey &&
                DateTime.UtcNow - _lastLocationFetchUtc < TimeSpan.FromHours(12))
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

    private static string BuildLocationCacheKey()
    {
        var vm = ViewModel.Instance;
        return vm.WeatherLocationMode switch
        {
            WeatherLocationMode.CustomCoordinates => $"coords:{vm.WeatherLocationLatitude.ToString(CultureInfo.InvariantCulture)},{vm.WeatherLocationLongitude.ToString(CultureInfo.InvariantCulture)}",
            WeatherLocationMode.CustomCity => $"city:{vm.WeatherLocationCity}",
            WeatherLocationMode.IPBased => vm.WeatherAllowIPLocation ? "ip:true" : $"ip-fallback:{vm.WeatherLocationCity}",
            _ => $"city:{vm.WeatherLocationCity}"
        };
    }

    private static async Task<WeatherLocation> ResolveLocationAsync()
    {
        var vm = ViewModel.Instance;
        switch (vm.WeatherLocationMode)
        {
            case WeatherLocationMode.CustomCoordinates:
                return BuildLocationFromCoordinates(vm.WeatherLocationLatitude, vm.WeatherLocationLongitude);
            case WeatherLocationMode.CustomCity:
                return await BuildLocationFromCityAsync(vm.WeatherLocationCity).ConfigureAwait(false);
            case WeatherLocationMode.IPBased:
                if (!vm.WeatherAllowIPLocation)
                {
                    return await BuildLocationFromCityAsync(vm.WeatherLocationCity).ConfigureAwait(false);
                }
                return await BuildLocationFromIPAsync().ConfigureAwait(false);
            default:
                return await BuildLocationFromCityAsync(vm.WeatherLocationCity).ConfigureAwait(false);
        }
    }

    private static WeatherLocation BuildLocationFromCoordinates(double latitude, double longitude)
    {
        if (!IsValidCoordinate(latitude, longitude))
        {
            return null;
        }

        return new WeatherLocation(latitude, longitude);
    }

    private static async Task<WeatherLocation> BuildLocationFromCityAsync(string cityName)
    {
        string city = string.IsNullOrWhiteSpace(cityName) ? DefaultCityName : cityName.Trim();
        if (string.Equals(city, DefaultCityName, StringComparison.OrdinalIgnoreCase))
        {
            return BuildLocationFromCoordinates(DefaultCityLatitude, DefaultCityLongitude);
        }
        string url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(city)}&count=1&language=en&format=json";
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

    private static async Task<WeatherLocation> BuildLocationFromIPAsync()
    {
        string response = await Client.GetStringAsync("https://ipapi.co/json/").ConfigureAwait(false);
        var json = JObject.Parse(response);
        double? latitude = json.Value<double?>("latitude");
        double? longitude = json.Value<double?>("longitude");
        if (!latitude.HasValue || !longitude.HasValue)
        {
            return null;
        }

        return new WeatherLocation(latitude.Value, longitude.Value);
    }

    private static bool IsValidCoordinate(double latitude, double longitude)
    {
        return latitude >= -90 && latitude <= 90 && longitude >= -180 && longitude <= 180;
    }

    private static string BuildWeatherUrl(double latitude, double longitude)
    {
        string lat = latitude.ToString(CultureInfo.InvariantCulture);
        string lon = longitude.ToString(CultureInfo.InvariantCulture);
        return $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=temperature_2m,weather_code,relative_humidity_2m,wind_speed_10m,apparent_temperature&temperature_unit=celsius&timezone=auto";
    }

    internal static IReadOnlyDictionary<int, string> GetDefaultConditionMap()
    {
        return DefaultConditionMap;
    }

    internal static IReadOnlyDictionary<int, string> GetDefaultConditionIconMap()
    {
        return DefaultConditionIconMap;
    }

    private static string MapWeatherCode(int code)
    {
        if (!DefaultConditionMap.TryGetValue(code, out string defaultValue))
        {
            defaultValue = string.Empty;
        }

        return ApplyConditionOverride(code, defaultValue);
    }

    private static string ApplyConditionOverride(int code, string defaultValue)
    {
        if (!ViewModel.Instance.WeatherCustomOverridesEnabled)
        {
            return defaultValue;
        }

        string overrides = ViewModel.Instance.WeatherConditionOverrides;
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

    private static string ApplyConditionIconOverride(int code, string defaultIcon)
    {
        if (!ViewModel.Instance.WeatherCustomOverridesEnabled)
        {
            return defaultIcon;
        }

        string overrides = ViewModel.Instance.WeatherConditionOverrides;
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

    private static bool TryGetConditionOverride(string overrides, int code, out string iconOverride, out string textOverride)
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

    private static string MapWeatherEmoji(int code)
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
