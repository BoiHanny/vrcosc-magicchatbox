using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Threading;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using static vrcosc_magicchatbox.Classes.Modules.ComponentStatsModule;

namespace vrcosc_magicchatbox.ViewModels.Sections;

/// <summary>How one component is set to be written, without the reading itself.</summary>
public readonly record struct StatPreviewShape(
    string ShortName,
    string HardwareName,
    bool RaiseName,
    bool UseHardwareName,
    bool RoundNumbers,
    bool ShowMax);

/// <summary>Everything the readout's shape depends on, gathered so the writer needs no sensors.</summary>
public sealed record ComponentStatsPreviewOptions
{
    public string Separator { get; init; } = DefaultSeparator;
    public bool UseEmojis { get; init; }
    public bool Fahrenheit { get; init; }

    public bool ShowGpuTemperature { get; init; }
    public bool ShowGpuHotspot { get; init; }
    public bool ShowGpuWattage { get; init; }
    public bool ShowGpuMemoryTemperature { get; init; }
    public bool ShowGpuFanSpeed { get; init; }
    public bool ShowGpuCoreClock { get; init; }
    public bool ShowGpuMemoryClock { get; init; }
    public bool ShowGpuMemoryLoad { get; init; }
    public bool ShowDdrVersion { get; init; }

    public StatPreviewShape Cpu { get; init; }
    public StatPreviewShape Gpu { get; init; }
    public StatPreviewShape Vram { get; init; }
    public StatPreviewShape Ram { get; init; }
}

/// <summary>
/// The component readout written from fixed, plausible sensor values.
/// </summary>
/// <remarks>
/// This is the largest settings surface in the app and around thirty of its switches only ever show
/// their effect inside a chatbox line. It reuses the module's own writer, so the preview cannot
/// drift from the output: only the numbers are invented here, never the shape.
///
/// It also makes one dependency visible that no label can state briefly. The emoji switch replaces
/// the temperature and power words with icons, and an icon is never raised - so with emojis on, the
/// GPU's "small raised labels" switch stops changing anything about those readings.
/// </remarks>
public static class ComponentStatsPreview
{
    // A machine under a moderate VR load. Chosen so every switch changes something visible: the
    // loads have fractions to round off, and the memory figures have a capacity to show or hide.
    private const string SampleCpuLoad = "23.4";
    private const string SampleGpuLoad = "61.7";
    private const string SampleVramUsed = "5.7";
    private const string SampleVramTotal = "16.0";
    private const string SampleRamUsed = "18.3";
    private const string SampleRamTotal = "32.0";

    private const double SampleGpuCelsius = 64.0;
    private const double SampleHotspotCelsius = 78.0;
    private const double SampleMemoryCelsius = 72.0;
    private const string SampleWatts = "213.0";
    private const string SampleFanPercent = "45";
    private const string SampleCoreClock = "2100";
    private const string SampleMemoryClock = "9500";
    private const string SampleMemoryLoad = "34.2";

    /// <summary>The raised DDR generation the module appends behind the RAM figure.</summary>
    private const string SampleDdrSuffix = "⁽ᴰᴰᴿ⁵⁾";

    private const string PercentUnit = "﹪";
    private const string GigabyteUnit = "ᵍᵇ";

    public static string Render(ComponentStatsPreviewOptions options)
    {
        if (options is null)
            return string.Empty;

        var core = new List<StatExtra>(3);
        var other = new List<StatExtra>(5);

        if (options.ShowGpuTemperature)
            core.Add(Temperature(options, "♨️", "temp", SampleGpuCelsius));
        if (options.ShowGpuHotspot)
            core.Add(Temperature(options, "🔥", "GPU HotSpot", SampleHotspotCelsius));
        if (options.ShowGpuWattage)
            core.Add(Extra(options, "⚡", "power", Round(options.Gpu, SampleWatts), "W"));

        if (options.ShowGpuMemoryTemperature)
            other.Add(Temperature(options, "🧊", "mem temp", SampleMemoryCelsius));
        if (options.ShowGpuFanSpeed)
            other.Add(Extra(options, "🌀", "fan", SampleFanPercent, PercentUnit));
        if (options.ShowGpuCoreClock)
            other.Add(Extra(options, "🔄", "core clk", SampleCoreClock, "MHz"));
        if (options.ShowGpuMemoryClock)
            other.Add(Extra(options, "💾", "mem clk", SampleMemoryClock, "MHz"));
        if (options.ShowGpuMemoryLoad)
            other.Add(Extra(options, "📊", "mem load", Round(options.Gpu, SampleMemoryLoad), PercentUnit));

        var readings = new List<StatReading>(4)
        {
            Reading(options.Cpu, SampleCpuLoad, null, PercentUnit),
            Reading(options.Gpu, SampleGpuLoad, null, PercentUnit, core, other),
            Reading(options.Vram, SampleVramUsed, SampleVramTotal, GigabyteUnit),
            Reading(options.Ram, SampleRamUsed, SampleRamTotal, GigabyteUnit,
                suffix: options.ShowDdrVersion ? SampleDdrSuffix : string.Empty),
        };

        // The module's own writer, not a copy of it: the preview cannot disagree with the output.
        return ComponentStatsModule.Render(readings, options.Separator, StatsDetail.Full);
    }

    private static StatReading Reading(
        StatPreviewShape shape,
        string value,
        string? max,
        string unit,
        IReadOnlyList<StatExtra>? core = null,
        IReadOnlyList<StatExtra>? other = null,
        string suffix = "")
    {
        bool useHardware = shape.UseHardwareName && !string.IsNullOrWhiteSpace(shape.HardwareName);

        return new StatReading
        {
            Name = useHardware ? shape.HardwareName : shape.ShortName,
            ShortName = shape.ShortName,
            RaiseName = shape.RaiseName,
            Value = Round(shape, value),
            Max = shape.ShowMax && max != null ? Round(shape, max) : null,
            Unit = unit,
            Suffix = suffix,
            CoreExtras = core ?? [],
            OtherExtras = other ?? [],
        };
    }

    /// <summary>
    /// Mirrors the module's choice: an emoji stands in for the word and is never raised, so the
    /// component's raised-label switch stops reaching these readings the moment emojis are on.
    /// </summary>
    private static StatExtra Extra(ComponentStatsPreviewOptions options, string emoji, string label, string value, string unit)
        => options.UseEmojis
            ? new StatExtra(emoji, RaiseLabel: false, value, unit)
            : new StatExtra(label, options.Gpu.RaiseName, value, unit);

    private static StatExtra Temperature(ComponentStatsPreviewOptions options, string emoji, string label, double celsius)
    {
        double reading = options.Fahrenheit ? celsius * 9.0 / 5.0 + 32 : celsius;
        string unit = options.Fahrenheit ? "°F" : "°C";
        string value = options.Gpu.RoundNumbers ? $"{(int)Math.Round(reading)}" : $"{reading:F1}";

        return Extra(options, emoji, label, value, unit);
    }

    /// <summary>Drops the fraction the same way the sensors do when rounding is on.</summary>
    private static string Round(StatPreviewShape shape, string value)
    {
        if (!shape.RoundNumbers)
            return value;

        int point = value.IndexOfAny(['.', ',']);
        return point > 0 ? value[..point] : value;
    }
}

public partial class ComponentStatsSectionViewModel : ObservableObject
{
    /// <summary>
    /// Redraw rate while the temperature unit is set to alternate. The unit flips on a clock, so a
    /// still preview would show one half of a setting whose whole point is that it changes.
    /// </summary>
    private static readonly TimeSpan TemperatureTick = TimeSpan.FromSeconds(1);

    private readonly DispatcherTimer? _temperatureTimer;

    public AppSettings AppSettings { get; }
    public ComponentStatsModule StatsManager { get; }
    public ComponentStatsViewModel ComponentStats { get; }

    [ObservableProperty] private string _previewLine = string.Empty;

    public ComponentStatsSectionViewModel(
        ISettingsProvider<AppSettings> appSettingsProvider,
        Lazy<ComponentStatsModule> statsManager,
        Lazy<ComponentStatsViewModel> componentStats)
    {
        AppSettings = appSettingsProvider.Value;
        StatsManager = statsManager.Value;
        ComponentStats = componentStats.Value;

        StatsManager.Settings.PropertyChanged += OnAnythingChanged;
        ComponentStats.PropertyChanged += OnAnythingChanged;
        AppSettings.PropertyChanged += OnAppSettingsChanged;

        // Bound to the application's dispatcher rather than whichever thread built the view model:
        // a timer on a thread with no message pump never ticks.
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null)
        {
            _temperatureTimer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher) { Interval = TemperatureTick };
            _temperatureTimer.Tick += (_, _) => RefreshPreview();
        }

        RefreshPreview();
        UpdateTemperatureTimer();
    }

    private void OnAnythingChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshPreview();
        UpdateTemperatureTimer();
    }

    private void OnAppSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSettings.Settings_ComponentStats))
            UpdateTemperatureTimer();
    }

    /// <summary>Only ticks while the panel is open and the unit is actually alternating.</summary>
    private void UpdateTemperatureTimer()
    {
        if (_temperatureTimer == null)
            return;

        bool wanted = AppSettings.Settings_ComponentStats && StatsManager.Settings.IsTemperatureSwitchEnabled;
        if (wanted == _temperatureTimer.IsEnabled)
            return;

        if (wanted) _temperatureTimer.Start();
        else _temperatureTimer.Stop();
    }

    private void RefreshPreview() => PreviewLine = ComponentStatsPreview.Render(BuildPreviewOptions());

    private ComponentStatsPreviewOptions BuildPreviewOptions()
    {
        var settings = StatsManager.Settings;

        return new ComponentStatsPreviewOptions
        {
            Separator = settings.StatsSeparator,
            UseEmojis = settings.UseEmojisForTempAndPower,
            Fahrenheit = settings.TemperatureUnit == "F",
            ShowGpuTemperature = ComponentStats.ComponentStatGPUTempVisible,
            ShowGpuHotspot = ComponentStats.ComponentStatGPUHotSpotVisible,
            ShowGpuWattage = ComponentStats.ComponentStatGPUWattageVisible,
            ShowGpuMemoryTemperature = settings.ShowGpuMemoryTemperature,
            ShowGpuFanSpeed = settings.ShowGpuFanSpeed,
            ShowGpuCoreClock = settings.ShowGpuCoreClock,
            ShowGpuMemoryClock = settings.ShowGpuMemoryClock,
            ShowGpuMemoryLoad = settings.ShowGpuMemoryLoad,
            ShowDdrVersion = ComponentStats.RAM_ShowDDRVersion,
            Cpu = Shape(StatsComponentType.CPU, "CPU", ComponentStats.CPU_SmallName, ComponentStats.CPU_EnableHardwareTitle, ComponentStats.CPU_PrefixHardwareTitle, ComponentStats.CPUCustomHardwareName, ComponentStats.CPU_NumberTrailingZeros, showMax: false),
            Gpu = Shape(StatsComponentType.GPU, "GPU", ComponentStats.GPU_SmallName, ComponentStats.GPU_EnableHardwareTitle, ComponentStats.GPU_PrefixHardwareTitle, ComponentStats.GPUCustomHardwareName, ComponentStats.GPU_NumberTrailingZeros, showMax: false),
            Vram = Shape(StatsComponentType.VRAM, "VRAM", ComponentStats.VRAM_SmallName, ComponentStats.VRAM_EnableHardwareTitle, ComponentStats.VRAM_PrefixHardwareTitle, ComponentStats.VRAMCustomHardwareName, ComponentStats.VRAM_NumberTrailingZeros, ComponentStats.VRAM_ShowMaxValue),
            Ram = Shape(StatsComponentType.RAM, "RAM", ComponentStats.RAM_SmallName, ComponentStats.RAM_EnableHardwareTitle, ComponentStats.RAM_PrefixHardwareTitle, ComponentStats.RAMCustomHardwareName, ComponentStats.RAM_NumberTrailingZeros, ComponentStats.RAM_ShowMaxValue),
        };
    }

    private StatPreviewShape Shape(
        StatsComponentType type,
        string shortName,
        bool raiseName,
        bool useHardwareName,
        bool useCustomName,
        string? customName,
        bool roundNumbers,
        bool showMax)
    {
        // The custom name only wins once it has something in it - the same test the writer makes.
        string hardware = useCustomName && !string.IsNullOrWhiteSpace(customName)
            ? customName!
            : StatsManager.GetHardwareName(type) ?? shortName;

        return new StatPreviewShape(shortName, hardware, raiseName, useHardwareName, roundNumbers, showMax);
    }
}
