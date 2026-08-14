using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Osc.Text;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.Modules;

// The global temperature unit can be time-based, so both units have to come from one resolution
// per render: the wind unit is derived from the temperature unit rather than resolved again.
public static class WeatherUnitResolver
{
    public static string Temperature(WeatherUnitOverride unitOverride, string globalUnit)
        => unitOverride switch
        {
            WeatherUnitOverride.Celsius => "C",
            WeatherUnitOverride.Fahrenheit => "F",
            _ => globalUnit
        };

    public static string Wind(WeatherWindUnitOverride windOverride, string temperatureUnit)
        => windOverride switch
        {
            WeatherWindUnitOverride.KilometersPerHour => "km/h",
            WeatherWindUnitOverride.MilesPerHour => "mph",
            _ => temperatureUnit == "F" ? "mph" : "km/h"
        };
}

/// <summary>
/// What Weather is allowed to spend of the chatbox line.
/// </summary>
/// <remarks>
/// Weather is on for almost everyone, so every character it takes is taken from something else.
/// Its one unbounded input is the user's template: nothing capped it, and a long one pushed every
/// integration after it off the line rather than shortening itself.
/// </remarks>
public static class WeatherBudget
{
    /// <summary>A template longer than the whole line cannot render, it can only crowd the line out.</summary>
    public const int MaxTemplateLength = Core.Constants.OscMaxMessageLength;

    /// <summary>Half the line. Past this Weather shortens itself instead of squeezing its neighbours.</summary>
    public const int MaxSegmentLength = 72;

    /// <summary>What the segment may spend: the room actually left, and never more than the share.</summary>
    public static string Bound(string? text, int roomOnTheLine)
        => SegmentWriter.Truncate(text, Math.Min(MaxSegmentLength, roomOnTheLine));

    /// <summary>
    /// Caps stored input. No ellipsis and no trimming - this is text the user is still editing, and
    /// it is bounded where it is stored so the worst case cannot be authored in the first place.
    /// </summary>
    public static string CapTemplate(string? text)
    {
        string value = text ?? string.Empty;
        if (value.Length <= MaxTemplateLength)
            return value;

        int cut = MaxTemplateLength;
        if (char.IsHighSurrogate(value[cut - 1]))
            cut--;

        return value[..cut];
    }
}

public partial class WeatherSettings : VersionedSettings
{
    public static IEnumerable<WeatherLayoutMode> AvailableLayoutModes { get; } = Enum.GetValues(typeof(WeatherLayoutMode)).Cast<WeatherLayoutMode>().ToList();
    public static IEnumerable<WeatherOrder> AvailableOrders { get; } = Enum.GetValues(typeof(WeatherOrder)).Cast<WeatherOrder>().ToList();
    public static IEnumerable<WeatherUnitOverride> AvailableUnitOverrides { get; } = Enum.GetValues(typeof(WeatherUnitOverride)).Cast<WeatherUnitOverride>().ToList();
    public static IEnumerable<WeatherWindUnitOverride> AvailableWindUnitOverrides { get; } = Enum.GetValues(typeof(WeatherWindUnitOverride)).Cast<WeatherWindUnitOverride>().ToList();
    public static IEnumerable<WeatherFallbackMode> AvailableFallbackModes { get; } = Enum.GetValues(typeof(WeatherFallbackMode)).Cast<WeatherFallbackMode>().ToList();
    public static IEnumerable<WeatherLocationMode> AvailableLocationModes { get; } = Enum.GetValues(typeof(WeatherLocationMode)).Cast<WeatherLocationMode>().ToList();

    [ObservableProperty] private bool _showWeatherInTime = true;
    [ObservableProperty] private bool _showWeatherCondition = false;
    [ObservableProperty] private bool _showWeatherEmoji = false;
    [ObservableProperty] private bool _weatherUseDecimal = false;
    [ObservableProperty] private bool _showWeatherHumidity = false;
    [ObservableProperty] private bool _showWeatherWind = false;
    [ObservableProperty] private bool _showWeatherFeelsLike = false;
    [ObservableProperty] private string _weatherSeparator = " | ";
    [ObservableProperty] private string _weatherStatsSeparator = " ";
    [ObservableProperty] private string _weatherConditionOverrides = string.Empty;
    [ObservableProperty] private bool _weatherCustomOverridesEnabled = false;
    [ObservableProperty] private WeatherLayoutMode _weatherLayoutMode = WeatherLayoutMode.SingleLine;
    [ObservableProperty] private WeatherOrder _weatherOrder = WeatherOrder.TimeFirst;
    [ObservableProperty] private WeatherUnitOverride _weatherUnitOverride = WeatherUnitOverride.UseGlobal;
    [ObservableProperty] private WeatherWindUnitOverride _weatherWindUnitOverride = WeatherWindUnitOverride.UseGlobal;
    [ObservableProperty] private WeatherFallbackMode _weatherFallbackMode = WeatherFallbackMode.Hide;
    [ObservableProperty] private WeatherLocationMode _weatherLocationMode = WeatherLocationMode.CustomCity;
    [ObservableProperty] private bool _weatherAllowIPLocation = false;
    [ObservableProperty] private bool _weatherLocationEditing = false;
    [ObservableProperty] private double _weatherLocationLatitude = 0;
    [ObservableProperty] private double _weatherLocationLongitude = 0;

    private int _weatherUpdateIntervalMinutes = 10;
    public int WeatherUpdateIntervalMinutes
    {
        get => _weatherUpdateIntervalMinutes;
        set
        {
            if (value < 1) value = 10;
            if (SetProperty(ref _weatherUpdateIntervalMinutes, value)) { }
        }
    }

    // Hand-written rather than generated because the value is capped on the way in - the generated
    // setter has nowhere to do that.
    private string _weatherTemplate = string.Empty;
    public string WeatherTemplate
    {
        get => _weatherTemplate;
        set
        {
            string capped = WeatherBudget.CapTemplate(value);
            if (SetProperty(ref _weatherTemplate, capped))
            {
                OnPropertyChanged(nameof(WeatherTemplateIsEmpty));
                OnPropertyChanged(nameof(WeatherTemplateHasValue));
            }
            else if (capped.Length != value?.Length)
            {
                // The editor is holding text that was not stored, so it has to be told to re-read.
                OnPropertyChanged();
            }
        }
    }

    private string _weatherLocationCityEncrypted = string.Empty;
    private string _weatherLocationCity = "London";

    [JsonIgnore]
    public string WeatherLocationCity
    {
        get => _weatherLocationCity;
        set
        {
            if (SetProperty(ref _weatherLocationCity, value ?? string.Empty))
            {
                EncryptionMethods.TryProcessToken(ref _weatherLocationCity, ref _weatherLocationCityEncrypted, true);
                OnPropertyChanged(nameof(WeatherLocationCityEncrypted));
            }
        }
    }

    public string WeatherLocationCityEncrypted
    {
        get => _weatherLocationCityEncrypted;
        set
        {
            if (SetProperty(ref _weatherLocationCityEncrypted, value ?? string.Empty))
            {
                EncryptionMethods.TryProcessToken(ref _weatherLocationCityEncrypted, ref _weatherLocationCity, false);
                if (_weatherLocationCity == null) _weatherLocationCity = string.Empty;
                OnPropertyChanged(nameof(WeatherLocationCity));
            }
        }
    }

    [JsonIgnore] public bool WeatherTemplateIsEmpty => string.IsNullOrWhiteSpace(WeatherTemplate);
    [JsonIgnore] public bool WeatherTemplateHasValue => !string.IsNullOrWhiteSpace(WeatherTemplate);
    [JsonIgnore] public bool WeatherLocationModeIsCustomCity => WeatherLocationMode == WeatherLocationMode.CustomCity;
    [JsonIgnore] public bool WeatherLocationModeIsCustomCoordinates => WeatherLocationMode == WeatherLocationMode.CustomCoordinates;
    [JsonIgnore] public bool WeatherLocationModeIsIPBased => WeatherLocationMode == WeatherLocationMode.IPBased;
    [JsonIgnore] public bool WeatherIpConsentMissing => WeatherLocationMode == WeatherLocationMode.IPBased && !WeatherAllowIPLocation;
    [JsonIgnore] public bool WeatherLocationModeUsesCity => WeatherLocationMode == WeatherLocationMode.CustomCity || WeatherLocationMode == WeatherLocationMode.IPBased;

    partial void OnWeatherLocationModeChanged(WeatherLocationMode value)
    {
        OnPropertyChanged(nameof(WeatherLocationModeIsCustomCity));
        OnPropertyChanged(nameof(WeatherLocationModeIsCustomCoordinates));
        OnPropertyChanged(nameof(WeatherLocationModeIsIPBased));
        OnPropertyChanged(nameof(WeatherIpConsentMissing));
        OnPropertyChanged(nameof(WeatherLocationModeUsesCity));
    }

    partial void OnWeatherAllowIPLocationChanged(bool value)
    {
        OnPropertyChanged(nameof(WeatherIpConsentMissing));
    }
}
