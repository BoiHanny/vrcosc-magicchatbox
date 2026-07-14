using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Settings for the weather display module, including location, units, layout, and condition overrides.
/// </summary>
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
    [ObservableProperty] private string _weatherTemplate = string.Empty;
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

    partial void OnWeatherTemplateChanged(string value)
    {
        OnPropertyChanged(nameof(WeatherTemplateIsEmpty));
        OnPropertyChanged(nameof(WeatherTemplateHasValue));
    }

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
