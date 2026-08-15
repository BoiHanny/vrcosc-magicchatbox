using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Units;

namespace vrcosc_magicchatbox.Classes.Modules;

[CurrentSchema(ComponentStatsSettings.TemperatureScaleSchema)]
public partial class ComponentStatsSettings : VersionedSettings, ILegacySettingsMigration
{
    public const int TemperatureScaleSchema = 2;

    public static IEnumerable<TemperatureCompanion> AvailableTemperatureCompanions { get; } =
        Enum.GetValues(typeof(TemperatureCompanion)).Cast<TemperatureCompanion>().ToList();

    [ObservableProperty] private string _selectedGPU = string.Empty;
    [ObservableProperty] private bool _autoSelectGPU = true;
    [ObservableProperty] private bool _useEmojisForTempAndPower = false;
    [ObservableProperty] private bool _gPU3DHook = false;
    [ObservableProperty] private bool _gPU3DVRAMHook = false;

    [ObservableProperty] private bool _enableVendorGpuSensors = true;

    [ObservableProperty] private bool _showGpuFanSpeed = false;
    [ObservableProperty] private bool _showGpuCoreClock = false;
    [ObservableProperty] private bool _showGpuMemoryClock = false;
    [ObservableProperty] private bool _showGpuMemoryTemperature = false;
    [ObservableProperty] private bool _showGpuMemoryLoad = false;
    [ObservableProperty] private string _statsSeparator = " ¦ ";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EnabledTemperatureScales))]
    [NotifyPropertyChangedFor(nameof(TemperatureRotates))]
    private bool _temperatureCelsius = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EnabledTemperatureScales))]
    [NotifyPropertyChangedFor(nameof(TemperatureRotates))]
    private bool _temperatureFahrenheit = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EnabledTemperatureScales))]
    [NotifyPropertyChangedFor(nameof(TemperatureRotates))]
    private bool _temperatureKelvin = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EnabledTemperatureScales))]
    [NotifyPropertyChangedFor(nameof(TemperatureRotates))]
    private bool _temperatureRankine = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EnabledTemperatureScales))]
    [NotifyPropertyChangedFor(nameof(TemperatureRotates))]
    private bool _temperatureReaumur = false;

    [ObservableProperty] private TemperatureCompanion _temperatureCompanionScale = TemperatureCompanion.None;

    public int TemperatureDisplaySwitchInterval { get; set; } = 5;

    public bool IsFahrenheit { get; set; } = false;

    public bool IsTemperatureSwitchEnabled { get; set; } = true;

    [JsonIgnore]
    public IReadOnlyList<TemperatureScale> EnabledTemperatureScales
    {
        get
        {
            var picked = new List<TemperatureScale>(Temperatures.All.Length);
            foreach (var scale in Temperatures.All)
            {
                if (IsEnabled(scale))
                    picked.Add(scale);
            }

            return picked.Count == 0 ? new[] { TemperatureScale.Celsius } : picked;
        }
    }

    [JsonIgnore]
    public bool TemperatureRotates => EnabledTemperatureScales.Count > 1;

    [JsonIgnore]
    public TemperatureScale CurrentTemperatureScale => TemperatureScaleAt(DateTime.Now.Second);

    public TemperatureScale TemperatureScaleAt(int second)
    {
        var scales = EnabledTemperatureScales;
        if (scales.Count == 1)
            return scales[0];

        int interval = Math.Max(1, TemperatureDisplaySwitchInterval);
        return scales[Math.Abs(second / interval) % scales.Count];
    }

    private bool IsEnabled(TemperatureScale scale)
        => scale switch
        {
            TemperatureScale.Fahrenheit => TemperatureFahrenheit,
            TemperatureScale.Kelvin => TemperatureKelvin,
            TemperatureScale.Rankine => TemperatureRankine,
            TemperatureScale.Reaumur => TemperatureReaumur,
            _ => TemperatureCelsius,
        };

    public bool AdoptLegacySettings()
    {
        if (SchemaVersion >= TemperatureScaleSchema)
            return false;

        TemperatureCelsius = IsTemperatureSwitchEnabled || !IsFahrenheit;
        TemperatureFahrenheit = IsTemperatureSwitchEnabled || IsFahrenheit;
        TemperatureKelvin = false;
        TemperatureRankine = false;
        TemperatureReaumur = false;
        SchemaVersion = TemperatureScaleSchema;
        return true;
    }

    [OnDeserialized]
    internal void AdoptLegacyTemperatureChoice(StreamingContext context) => AdoptLegacySettings();
}
