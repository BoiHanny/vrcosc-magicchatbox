using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.Models;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Settings for the tracker battery display module, including layout, thresholds, and sort options.
/// </summary>
public partial class TrackerBatterySettings : VersionedSettings
{
    public static IEnumerable<TrackerBatterySortMode> AvailableSortModes { get; } = Enum.GetValues(typeof(TrackerBatterySortMode)).Cast<TrackerBatterySortMode>().ToList();

    [ObservableProperty] private string _template = "{icon} {name} {batt}%";
    [ObservableProperty] private string _prefix = string.Empty;
    [ObservableProperty] private string _suffix = string.Empty;
    [ObservableProperty] private string _separator = " | ";
    [ObservableProperty] private bool _globalEmergency = false;
    [ObservableProperty] private bool _showControllers = true;
    [ObservableProperty] private bool _showHeadset = true;
    [ObservableProperty] private bool _showTrackers = false;
    [ObservableProperty] private bool _showDisconnected = false;
    [ObservableProperty] private string _offlineBatteryText = "N/A";
    [ObservableProperty] private string _onlineText = "Online";
    [ObservableProperty] private string _offlineText = "Offline";
    [ObservableProperty] private string _lowTag = "LOW";
    [ObservableProperty] private bool _compactWhitespace = true;
    [ObservableProperty] private bool _useSmallText = false;
    [ObservableProperty] private TrackerBatterySortMode _sortMode = TrackerBatterySortMode.None;
    [ObservableProperty] private bool _rotateOverflow = false;

    private int _lowThreshold = 20;
    public int LowThreshold
    {
        get => _lowThreshold;
        set
        {
            value = Math.Clamp(value, 1, 100);
            SetProperty(ref _lowThreshold, value);
        }
    }

    private int _maxEntries = 2;
    public int MaxEntries
    {
        get => _maxEntries;
        set
        {
            if (value < 0) value = 0;
            SetProperty(ref _maxEntries, value);
        }
    }

    private int _rotationIntervalSeconds = 5;
    public int RotationIntervalSeconds
    {
        get => _rotationIntervalSeconds;
        set
        {
            if (value < 1) value = 1;
            SetProperty(ref _rotationIntervalSeconds, value);
        }
    }

    private int _maxEntryLength = 0;
    public int MaxEntryLength
    {
        get => _maxEntryLength;
        set
        {
            if (value < 0) value = 0;
            SetProperty(ref _maxEntryLength, value);
        }
    }

    /// <summary>
    /// Persisted tracker device list — restored across restarts.
    /// At runtime this is copied into TrackerDisplayState.TrackerDevices.
    /// </summary>
    [ObservableProperty] private ObservableCollection<TrackerDevice> _savedDevices = new();
}
