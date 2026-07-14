using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.Models;

namespace vrcosc_magicchatbox.ViewModels.State;

/// <summary>
/// Manages the runtime UI collection for weather condition overrides.
/// Reads/writes the serialized string from WeatherSettings.WeatherConditionOverrides.
/// </summary>
public partial class WeatherOverrideState : ObservableObject
{
    private ObservableCollection<WeatherConditionOverrideItem> _items;
    private bool _isUpdating;
    private WeatherSettings _settings;
    private readonly IWeatherService _weatherService;

    public WeatherOverrideState(IWeatherService weatherService)
    {
        _weatherService = weatherService;
    }

    /// <summary>
    /// Must be called after DI is ready to bind to WeatherSettings.
    /// </summary>
    public void Initialize(WeatherSettings settings)
    {
        _settings = settings;
        _settings.PropertyChanged += OnSettingsPropertyChanged;
    }

    private void OnSettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WeatherSettings.WeatherConditionOverrides) && !_isUpdating && _items != null)
        {
            ApplyOverridesToItems(_settings.WeatherConditionOverrides);
        }
    }

    public ObservableCollection<WeatherConditionOverrideItem> Items
    {
        get
        {
            EnsureItems();
            return _items;
        }
    }

    private void EnsureItems()
    {
        if (_items != null) return;

        _items = BuildItems();
        foreach (var item in _items)
        {
            item.PropertyChanged += OnItemPropertyChanged;
        }

        if (_settings != null)
        {
            ApplyOverridesToItems(_settings.WeatherConditionOverrides);
        }
    }

    private ObservableCollection<WeatherConditionOverrideItem> BuildItems()
    {
        var items = new ObservableCollection<WeatherConditionOverrideItem>();
        var defaultMap = _weatherService.GetDefaultConditionMap();
        var defaultIconMap = _weatherService.GetDefaultConditionIconMap();
        foreach (var entry in defaultMap.OrderBy(pair => pair.Key))
        {
            defaultIconMap.TryGetValue(entry.Key, out string icon);
            items.Add(new WeatherConditionOverrideItem(entry.Key, icon, entry.Value));
        }
        return items;
    }

    private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        // React to both CustomText AND CustomIcon changes (fixes icon persistence bug)
        if (e.PropertyName != nameof(WeatherConditionOverrideItem.CustomText) &&
            e.PropertyName != nameof(WeatherConditionOverrideItem.CustomIcon))
            return;

        PushItemsToSettings();
    }

    private void PushItemsToSettings()
    {
        if (_isUpdating || _settings == null) return;

        _isUpdating = true;
        _settings.WeatherConditionOverrides = BuildOverridesString(_items);
        _isUpdating = false;
    }

    private void ApplyOverridesToItems(string overrides)
    {
        if (_items == null) return;

        var map = ParseOverrides(overrides);
        _isUpdating = true;
        foreach (var item in _items)
        {
            if (map.TryGetValue(item.Code, out var val))
            {
                item.CustomIcon = val.Icon;
                item.CustomText = val.Text;
            }
            else
            {
                item.CustomIcon = string.Empty;
                item.CustomText = string.Empty;
            }
        }
        _isUpdating = false;
    }

    private static string BuildOverridesString(IEnumerable<WeatherConditionOverrideItem> items)
    {
        if (items == null) return string.Empty;

        var builder = new StringBuilder();
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.CustomText) && string.IsNullOrWhiteSpace(item.CustomIcon))
                continue;

            if (builder.Length > 0) builder.AppendLine();
            builder.Append(item.Code.ToString(CultureInfo.InvariantCulture));
            builder.Append('=');
            builder.Append(item.CustomIcon?.Trim() ?? string.Empty);
            builder.Append('|');
            builder.Append(item.CustomText?.Trim() ?? string.Empty);
        }
        return builder.ToString();
    }

    private static Dictionary<int, (string Icon, string Text)> ParseOverrides(string overrides)
    {
        var map = new Dictionary<int, (string Icon, string Text)>();
        if (string.IsNullOrWhiteSpace(overrides)) return map;

        var entries = overrides.Split(new[] { '\n', ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string entry in entries)
        {
            string trimmed = entry.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            int separatorIndex = trimmed.IndexOf('=');
            if (separatorIndex < 0) separatorIndex = trimmed.IndexOf(':');
            if (separatorIndex < 0) continue;

            string codeText = trimmed.Substring(0, separatorIndex).Trim();
            if (!int.TryParse(codeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int code))
                continue;

            string value = trimmed.Substring(separatorIndex + 1).Trim();
            string icon = string.Empty;
            string text = string.Empty;
            int iconSep = value.IndexOf('|');
            if (iconSep >= 0)
            {
                icon = value.Substring(0, iconSep).Trim();
                text = value.Substring(iconSep + 1).Trim();
            }
            else
            {
                text = value;
            }
            map[code] = (icon, text);
        }
        return map;
    }
}
