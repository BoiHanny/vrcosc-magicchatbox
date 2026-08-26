using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Threading;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Core.Units;
using static vrcosc_magicchatbox.Classes.Modules.ComponentStatsModule;

namespace vrcosc_magicchatbox.ViewModels.Sections;

public readonly record struct StatPreviewShape(
    string ShortName,
    string HardwareName,
    bool RaiseName,
    bool UseHardwareName,
    bool RoundNumbers,
    bool ShowMax);

public sealed record ComponentStatsPreviewOptions
{
    public string Separator { get; init; } = DefaultSeparator;
    public bool UseEmojis { get; init; }
    public TemperatureScale Scale { get; init; } = TemperatureScale.Celsius;
    public TemperatureCompanion Companion { get; init; } = TemperatureCompanion.None;

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

public static class ComponentStatsPreview
{
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

    private static StatExtra Extra(ComponentStatsPreviewOptions options, string emoji, string label, string value, string unit)
        => options.UseEmojis
            ? new StatExtra(emoji, RaiseLabel: false, value, unit)
            : new StatExtra(label, options.Gpu.RaiseName, value, unit);

    private static StatExtra Temperature(ComponentStatsPreviewOptions options, string emoji, string label, double celsius)
    {
        string unit = Temperatures.Symbol(options.Scale, degreeSign: true) + Companion(options, celsius);

        return Extra(options, emoji, label, Reading(options, celsius, options.Scale), unit);
    }

    private static string Companion(ComponentStatsPreviewOptions options, double celsius)
        => Temperatures.TryCompanion(options.Companion, options.Scale, out TemperatureScale companion)
            ? $" ({Reading(options, celsius, companion)}{Temperatures.Symbol(companion, degreeSign: true)})"
            : string.Empty;

    private static string Reading(ComponentStatsPreviewOptions options, double celsius, TemperatureScale scale)
    {
        double value = Temperatures.FromCelsius(celsius, scale);
        return options.Gpu.RoundNumbers ? $"{(int)Math.Round(value)}" : $"{value:F1}";
    }

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
    private static readonly TimeSpan TemperatureTick = TimeSpan.FromSeconds(1);

    private readonly DispatcherTimer? _temperatureTimer;

    private readonly IAppState _appState;

    public AppSettings AppSettings { get; }
    public ComponentStatsModule StatsManager { get; }
    public ComponentStatsViewModel ComponentStats { get; }

    [ObservableProperty] private string _previewLine = string.Empty;

    public ComponentStatsSectionViewModel(
        ISettingsProvider<AppSettings> appSettingsProvider,
        Lazy<ComponentStatsModule> statsManager,
        Lazy<ComponentStatsViewModel> componentStats,
        IAppState appState)
    {
        AppSettings = appSettingsProvider.Value;
        StatsManager = statsManager.Value;
        ComponentStats = componentStats.Value;
        _appState = appState;

        StatsManager.Settings.PropertyChanged += OnAnythingChanged;
        ComponentStats.PropertyChanged += OnAnythingChanged;
        AppSettings.PropertyChanged += OnAppSettingsChanged;

        if (_appState is INotifyPropertyChanged observable)
            observable.PropertyChanged += OnAppStateChanged;

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null)
        {
            _temperatureTimer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher) { Interval = TemperatureTick };
            _temperatureTimer.Tick += (_, _) => RefreshPreview();
        }

        RefreshPreview();
        UpdateTemperatureTimer();
    }

    private bool _previewRefreshPending;

    private void OnAnythingChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateTemperatureTimer();

        if (!IsPreviewRelevant(e.PropertyName))
            return;

        SchedulePreviewRefresh();
    }

    private static bool IsPreviewRelevant(string? propertyName) => propertyName switch
    {
        null => true,
        nameof(ComponentStatsSettings.StatsSeparator) => true,
        nameof(ComponentStatsSettings.UseEmojisForTempAndPower) => true,
        nameof(ComponentStatsSettings.TemperatureCompanionScale) => true,
        nameof(ComponentStatsSettings.EnabledTemperatureScales) => true,
        nameof(ComponentStatsSettings.ShowGpuMemoryTemperature) => true,
        nameof(ComponentStatsSettings.ShowGpuFanSpeed) => true,
        nameof(ComponentStatsSettings.ShowGpuCoreClock) => true,
        nameof(ComponentStatsSettings.ShowGpuMemoryClock) => true,
        nameof(ComponentStatsSettings.ShowGpuMemoryLoad) => true,
        nameof(ComponentStatsViewModel.ComponentStatGPUTempVisible) => true,
        nameof(ComponentStatsViewModel.ComponentStatGPUHotSpotVisible) => true,
        nameof(ComponentStatsViewModel.ComponentStatGPUWattageVisible) => true,
        nameof(ComponentStatsViewModel.RAM_ShowDDRVersion) => true,
        nameof(ComponentStatsViewModel.CPU_SmallName) => true,
        nameof(ComponentStatsViewModel.CPU_EnableHardwareTitle) => true,
        nameof(ComponentStatsViewModel.CPU_PrefixHardwareTitle) => true,
        nameof(ComponentStatsViewModel.CPUCustomHardwareName) => true,
        nameof(ComponentStatsViewModel.CPU_NumberTrailingZeros) => true,
        nameof(ComponentStatsViewModel.GPU_SmallName) => true,
        nameof(ComponentStatsViewModel.GPU_EnableHardwareTitle) => true,
        nameof(ComponentStatsViewModel.GPU_PrefixHardwareTitle) => true,
        nameof(ComponentStatsViewModel.GPUCustomHardwareName) => true,
        nameof(ComponentStatsViewModel.GPU_NumberTrailingZeros) => true,
        nameof(ComponentStatsViewModel.VRAM_SmallName) => true,
        nameof(ComponentStatsViewModel.VRAM_EnableHardwareTitle) => true,
        nameof(ComponentStatsViewModel.VRAM_PrefixHardwareTitle) => true,
        nameof(ComponentStatsViewModel.VRAMCustomHardwareName) => true,
        nameof(ComponentStatsViewModel.VRAM_NumberTrailingZeros) => true,
        nameof(ComponentStatsViewModel.VRAM_ShowMaxValue) => true,
        nameof(ComponentStatsViewModel.RAM_SmallName) => true,
        nameof(ComponentStatsViewModel.RAM_EnableHardwareTitle) => true,
        nameof(ComponentStatsViewModel.RAM_PrefixHardwareTitle) => true,
        nameof(ComponentStatsViewModel.RAMCustomHardwareName) => true,
        nameof(ComponentStatsViewModel.RAM_NumberTrailingZeros) => true,
        nameof(ComponentStatsViewModel.RAM_ShowMaxValue) => true,
        _ => false,
    };

    private void SchedulePreviewRefresh()
    {
        if (_previewRefreshPending)
            return;

        _previewRefreshPending = true;

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            _previewRefreshPending = false;
            RefreshPreview();
            return;
        }

        dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            _previewRefreshPending = false;
            RefreshPreview();
        }));
    }

    private void OnAppSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSettings.Settings_ComponentStats))
            UpdateTemperatureTimer();
    }

    private void OnAppStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IAppState.IsWindowOnScreen)
            or nameof(IAppState.IsTrayMenuOpen)
            or nameof(IAppState.IsUiObservable))
        {
            UpdateTemperatureTimer();
        }
    }

    private void UpdateTemperatureTimer()
    {
        if (_temperatureTimer == null)
            return;

        // The tick only rewrites a preview string on the Options page. This view model is a singleton, so
        // without the visibility gate it keeps rendering that string while the app sits in the tray.
        bool wanted = _appState.IsUiObservable
            && AppSettings.Settings_ComponentStats
            && StatsManager.Settings.TemperatureRotates;
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
            Scale = settings.CurrentTemperatureScale,
            Companion = settings.TemperatureCompanionScale,
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
        string hardware = useCustomName && !string.IsNullOrWhiteSpace(customName)
            ? customName!
            : StatsManager.GetHardwareName(type) ?? shortName;

        return new StatPreviewShape(shortName, hardware, raiseName, useHardwareName, roundNumbers, showMax);
    }
}
