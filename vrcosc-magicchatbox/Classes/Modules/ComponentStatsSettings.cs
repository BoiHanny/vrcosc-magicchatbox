using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Persisted settings for the component stats (CPU/GPU/RAM/VRAM) display module.
/// </summary>
public partial class ComponentStatsSettings : VersionedSettings
{
    [ObservableProperty] private string _selectedGPU = string.Empty;
    [ObservableProperty] private bool _autoSelectGPU = true;
    [ObservableProperty] private bool _useEmojisForTempAndPower = false;
    [ObservableProperty] private bool _isTemperatureSwitchEnabled = true;
    [ObservableProperty] private bool _isFahrenheit = false;
    [ObservableProperty] private bool _gPU3DHook = false;
    [ObservableProperty] private bool _gPU3DVRAMHook = false;

    [ObservableProperty] private bool _showGpuFanSpeed = false;
    [ObservableProperty] private bool _showGpuCoreClock = false;
    [ObservableProperty] private bool _showGpuMemoryClock = false;
    [ObservableProperty] private bool _showGpuMemoryTemperature = false;
    [ObservableProperty] private bool _showGpuMemoryLoad = false;
    [ObservableProperty] private bool _showCpuMaxCoreLoad = false;
    [ObservableProperty] private bool _showCpuClock = false;
    [ObservableProperty] private string _statsSeparator = " ¦ ";

    public int TemperatureDisplaySwitchInterval { get; set; } = 5;

    [JsonIgnore]
    public string TemperatureUnit
    {
        get
        {
            if (IsTemperatureSwitchEnabled)
            {
                int interval = Math.Max(1, TemperatureDisplaySwitchInterval);
                return (DateTime.Now.Second / interval) % 2 == 0 ? "F" : "C";
            }
            return IsFahrenheit ? "F" : "C";
        }
    }
}
