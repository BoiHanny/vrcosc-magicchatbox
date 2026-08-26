using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Utilities;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Osc.Text;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.Core.Units;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Classes.Modules;

public class ComponentStatsModule : IModule
{
    private string? FileName = null;
    private readonly object _statsInitLock = new();
    private bool _statsLoaded;
    private bool _ddrVersionFetchStarted;
    private bool _gpuListLoaded;
    private static readonly TimeSpan MinHardwareNameCacheTtl = TimeSpan.FromSeconds(1);
    private DateTime _gpuNameCachedAtUtc = DateTime.MinValue;
    private DateTime _cpuNameCachedAtUtc = DateTime.MinValue;
    private string? _cachedGpuName;
    private string? _cachedCpuName;

    private static readonly StatsComponentType[] StatDisplayOrder =
    {
        StatsComponentType.CPU,
        StatsComponentType.GPU,
        StatsComponentType.VRAM,
        StatsComponentType.RAM,
    };

    private static readonly StatsComponentType[] SupportedComponentTypes =
    {
        StatsComponentType.CPU,
        StatsComponentType.GPU,
        StatsComponentType.VRAM,
        StatsComponentType.RAM,
    };

    public ObservableCollection<string> GPUList { get; } = new();

    private readonly IHardwareMonitorService _hwService;
    private readonly List<ComponentStatsItem> _componentStats = new List<ComponentStatsItem>();
    private readonly Dictionary<StatsComponentType, ComponentStatsItem> _statsByType = new();

    private volatile IReadOnlyList<StatReading>? _lastReadings;

    private string _ramDDRVersion = "Unknown";
    public bool started = false;

    private readonly ISettingsProvider<ComponentStatsSettings> _settingsProvider;
    public ComponentStatsSettings Settings => _settingsProvider.Value;
    public void SaveSettings()
    {
        _settingsProvider.Save();
        SaveComponentStats();
    }

    public string Name => "ComponentStats";
    public bool IsEnabled { get; set; } = true;
    public bool IsRunning => started;
    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task StartAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken ct = default) { _hwService.Close(); return Task.CompletedTask; }
    public void Dispose() => _hwService.Close();

    private ISettingsProvider<ComponentStatsSettings> _staticSettingsProvider;
    private ComponentStatsSettings StaticSettings => _staticSettingsProvider.Value;

    private TimeSettings _timeSettings;
    private TimeSettings TS => _timeSettings;

    private AppSettings _appSettings;
    private AppSettings AS => _appSettings;

    private IAppState _appState;

    private IEnvironmentService _env;

    private IntegrationDisplayState _integrationDisplay;

    // ServiceRegistration resolves ComponentStatsViewModel (which calls SetStatsViewModel) before StartModule ever runs.
    private ComponentStatsViewModel _statsVm = null!;
    private ComponentStatsViewModel StatsVm => _statsVm;

    public void SetStatsViewModel(ComponentStatsViewModel vm)
    {
        _statsVm = vm;
        if (_statsLoaded)
            _statsVm.SyncComponentStatsList();
    }

    private IntegrationSettings _integrationSettings;

    private readonly IUiDispatcher _dispatcher;
    private readonly Lazy<IStatePersistenceCoordinator> _persistence;
    private readonly IPrivacyConsentService _consentService;
    private readonly IToastService? _toast;
    private readonly IProcessPresenceService? _processPresence;

    public ComponentStatsModule(
        ISettingsProvider<ComponentStatsSettings> settingsProvider,
        ISettingsProvider<TimeSettings> timeSettingsProvider,
        ISettingsProvider<AppSettings> appSettingsProvider,
        IAppState appState,
        IEnvironmentService env,
        IntegrationDisplayState integrationDisplay,
        ISettingsProvider<IntegrationSettings> integrationSettingsProvider,
        IUiDispatcher dispatcher,
        Lazy<IStatePersistenceCoordinator> persistence,
        IHardwareMonitorService hwService,
        IPrivacyConsentService consentService,
        IToastService? toast = null,
        IProcessPresenceService? processPresence = null)
    {
        _settingsProvider = settingsProvider;
        _staticSettingsProvider = settingsProvider;
        _timeSettings = timeSettingsProvider.Value;
        _appSettings = appSettingsProvider.Value;
        _appState = appState;
        _env = env;
        _integrationDisplay = integrationDisplay;
        _integrationSettings = integrationSettingsProvider.Value;
        _dispatcher = dispatcher;
        _persistence = persistence;
        _hwService = hwService;
        _consentService = consentService;
        _toast = toast;
        _processPresence = processPresence;

        Settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(ComponentStatsSettings.EnableVendorGpuSensors))
                return;

            if (_hwService.IsOpen)
                _hwService.Close();
        };

        _consentService.ConsentChanged += (_, e) =>
        {
            if (e.Hook == PrivacyHook.HardwareMonitor && e.NewState == ConsentState.Denied)
            {
                if (_hwService.IsOpen)
                    _hwService.Close();
                _integrationDisplay.ComponentStatCombined = string.Empty;
                _lastReadings = null;
                _toast?.Show("🔒 Hardware Monitor", "Hardware monitoring paused — privacy consent revoked.", ToastType.Privacy, key: "hw-privacy-denied");
            }
        };
    }

    private void FetchAndStoreDDRVersion()
    {
        string? ddrVersion = GetDDRVersion();
        if (string.IsNullOrWhiteSpace(ddrVersion))
            return;

        _ramDDRVersion = ddrVersion;
        _dispatcher.BeginInvoke(() =>
        {
            var ramItem = GetStat(StatsComponentType.RAM);
            if (ramItem != null)
            {
                ramItem.DDRVersion = _ramDDRVersion;
            }

            _statsVm?.RefreshAllProperties();
        });
    }

    private void EnsureComponentStatsLoaded()
    {
        if (_statsLoaded)
            return;

        lock (_statsInitLock)
        {
            if (_statsLoaded)
                return;

            LoadComponentStats();
            _statsLoaded = true;
        }
    }

    private bool ShouldFetchDdrVersion()
    {
        var ramItem = GetStat(StatsComponentType.RAM);
        return ramItem != null && ramItem.IsEnabled && ramItem.ShowDDRVersion;
    }

    private void QueueDdrVersionFetchIfNeeded()
    {
        if (_ddrVersionFetchStarted || !ShouldFetchDdrVersion())
            return;

        lock (_statsInitLock)
        {
            if (_ddrVersionFetchStarted || !ShouldFetchDdrVersion())
                return;

            _ddrVersionFetchStarted = true;
        }

        _ = Task.Run(() =>
        {
            try
            {
                FetchAndStoreDDRVersion();
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        });
    }

    private void EnsureGpuListLoaded()
    {
        if (_gpuListLoaded)
            return;

        RefreshGpuList();
    }

    private string FetchCPUStat()
    {
        var current = GetStat(StatsComponentType.CPU);
        if (current == null) return "N/A";
        try
        {
            float? load = _hwService.GetCpuLoad();
            string? name = GetCachedCpuName();
            UpdateHardwareName(current, name);
            if (load == null) return "N/A";
            return current.RemoveNumberTrailing == true ? $"{(int)load}" : $"{load:F1}";
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return "N/A";
        }
    }

    private StatExtra Extra(ComponentStatsItem item, string emoji, string label, string value, string unit)
        => Settings.UseEmojisForTempAndPower
            ? new StatExtra(emoji, RaiseLabel: false, value, unit)
            : new StatExtra(label, item.ShowSmallName, value, unit);

    private StatExtra? FetchGpuCoreClockStat(ComponentStatsItem item)
    {
        float? mhz = _hwService.GetGpuCoreClock(GetDedicatedGPUNameForHardwareCall());
        if (mhz == null) return null;
        string value = item.RemoveNumberTrailing ? $"{(int)mhz.Value}" : $"{mhz.Value:F0}";
        return Extra(item, "🔄", "core clk", value, "MHz");
    }

    private StatExtra? FetchGpuFanSpeedStat(ComponentStatsItem item)
    {
        float? fanPercent = _hwService.GetGpuFanSpeed(GetDedicatedGPUNameForHardwareCall());
        if (fanPercent == null) return null;
        string value = item.RemoveNumberTrailing ? $"{(int)fanPercent.Value}" : $"{fanPercent.Value:F0}";
        return Extra(item, "🌀", "fan", value, PercentUnit);
    }

    private StatExtra? FetchGpuMemoryClockStat(ComponentStatsItem item)
    {
        float? mhz = _hwService.GetGpuMemoryClock(GetDedicatedGPUNameForHardwareCall());
        if (mhz == null) return null;
        string value = item.RemoveNumberTrailing ? $"{(int)mhz.Value}" : $"{mhz.Value:F0}";
        return Extra(item, "💾", "mem clk", value, "MHz");
    }

    private StatExtra? FetchGpuMemoryLoadStat(ComponentStatsItem item)
    {
        float? load = _hwService.GetGpuMemoryLoad(GetDedicatedGPUNameForHardwareCall());
        if (load == null) return null;
        string value = item.RemoveNumberTrailing ? $"{(int)load.Value}" : $"{load.Value:F1}";
        return Extra(item, "📊", "mem load", value, PercentUnit);
    }

    private StatExtra? FetchGpuMemoryTemperatureStat(ComponentStatsItem item)
    {
        float? rawCelsius = _hwService.GetGpuMemoryTemperature(GetDedicatedGPUNameForHardwareCall());
        if (rawCelsius == null || rawCelsius == 0) return null;
        return Extra(item, "🧊", "mem temp", FormatTemperature(item, rawCelsius.Value, out string unitSymbol), unitSymbol);
    }

    private string FormatTemperature(ComponentStatsItem item, float rawCelsius, out string unitSymbol)
    {
        TemperatureScale scale = Settings.CurrentTemperatureScale;
        double temperature = Temperatures.FromCelsius(rawCelsius, scale);
        if (item.RemoveNumberTrailing)
            temperature = Math.Round(temperature);

        unitSymbol = Temperatures.Symbol(scale, degreeSign: true) + CompanionSuffix(item, rawCelsius, scale);
        return item.RemoveNumberTrailing ? $"{(int)temperature}" : $"{temperature:F1}";
    }

    private string CompanionSuffix(ComponentStatsItem item, float rawCelsius, TemperatureScale shown)
    {
        if (!Temperatures.TryCompanion(Settings.TemperatureCompanionScale, shown, out TemperatureScale companion))
            return string.Empty;

        double temperature = Temperatures.FromCelsius(rawCelsius, companion);
        string value = item.RemoveNumberTrailing ? $"{(int)Math.Round(temperature)}" : $"{temperature:F1}";
        return $" ({value}{Temperatures.Symbol(companion, degreeSign: true)})";
    }

    private string FetchGPUStat()
    {
        var current = GetStat(StatsComponentType.GPU);
        if (current == null) return "N/A";
        try
        {
            string gpuName = GetDedicatedGPUNameForHardwareCall();
            string sensorName = StaticSettings.GPU3DHook ? "D3D 3D" : "GPU Core";
            float? load = _hwService.GetGpuLoad(gpuName, sensorName);
            string? resolvedName = _hwService.GetGpuName(gpuName);
            UpdateHardwareName(current, resolvedName);
            if (load == null) return "N/A";
            return current.RemoveNumberTrailing == true ? $"{(int)load}" : $"{load:F1}";
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return "N/A";
        }
    }

    private StatExtra? FetchHotspotTemperatureStat(ComponentStatsItem item)
    {
        float? rawCelsius = _hwService.GetGpuHotspotTemperature(GetDedicatedGPUNameForHardwareCall());

        if (rawCelsius == null || rawCelsius == 0)
        {
            item.cantShowHotSpotTemperature = true;
            return null;
        }

        item.cantShowHotSpotTemperature = false;
        return Extra(item, "🔥", "GPU HotSpot", FormatTemperature(item, rawCelsius.Value, out string unitSymbol), unitSymbol);
    }

    private StatExtra? FetchPowerStat(ComponentStatsItem item)
    {
        float? rawWatts = _hwService.GetGpuPower(GetDedicatedGPUNameForHardwareCall());

        if (rawWatts == null || rawWatts == 0)
        {
            item.cantShowWattage = true;
            return null;
        }

        double power = item.RemoveNumberTrailing ? Math.Round(rawWatts.Value) : rawWatts.Value;
        string formattedPower = item.RemoveNumberTrailing ? $"{(int)power}" : $"{power:F1}";

        item.cantShowWattage = false;
        return Extra(item, "⚡", "power", formattedPower, "W");
    }

    private (string UsedMemory, string MaxMemory) FetchRAMStats()
    {
        var current = GetStat(StatsComponentType.RAM);

        var wmiMem = _hwService.GetWindowsMemoryInfo();
        if (wmiMem.HasValue)
        {
            if (current?.RemoveNumberTrailing == true)
                return ($"{(int)wmiMem.Value.usedGiB}", $"{(int)wmiMem.Value.totalGiB}");
            else
                return ($"{wmiMem.Value.usedGiB:F1}", $"{wmiMem.Value.totalGiB:F1}");
        }

        float? used = _hwService.GetRamUsed();
        float? available = _hwService.GetRamAvailable();
        if (used.HasValue && available.HasValue)
        {
            double total = used.Value + available.Value;
            if (current?.RemoveNumberTrailing == true)
                return ($"{(int)used.Value}", $"{(int)total}");
            else
                return ($"{used.Value:F1}", $"{total:F1}");
        }

        return ("N/A", "N/A");
    }

    private void UpdateHardwareName(ComponentStatsItem current, string? hardwareName)
    {
        if (current == null || string.IsNullOrEmpty(hardwareName)) return;
        if (current.HardwareFriendlyName != hardwareName)
        {
            current.HardwareFriendlyName = hardwareName;
            if (!current.ReplaceWithHardwareName)
                current.HardwareFriendlyNameSmall = TextUtilities.TransformToSuperscript(hardwareName);
        }
        if (current.ReplaceWithHardwareName || string.IsNullOrEmpty(current.HardwareFriendlyNameSmall))
        {
            current.CustomHardwarenameValueSmall = TextUtilities.TransformToSuperscript(current.CustomHardwarenameValue ?? string.Empty);
        }
    }

    private StatExtra? FetchTemperatureStat(ComponentStatsItem item)
    {
        float? rawCelsius = _hwService.GetGpuTemperature(GetDedicatedGPUNameForHardwareCall());

        if (rawCelsius == null || rawCelsius == 0)
        {
            item.cantShowTemperature = true;
            return null;
        }

        item.cantShowTemperature = false;
        return Extra(item, "♨️", "temp", FormatTemperature(item, rawCelsius.Value, out string unitSymbol), unitSymbol);
    }

    private string FetchVRAMMaxStat()
    {
        var current = GetStat(StatsComponentType.VRAM);
        if (current == null) return "N/A";
        try
        {
            string gpuName = GetDedicatedGPUNameForHardwareCall();
            string sensorName = StaticSettings.GPU3DVRAMHook ? "D3D Dedicated Memory Total" : "GPU Memory Total";
            float? rawMb = _hwService.GetGpuVramTotal(gpuName, sensorName);
            string? resolvedName = _hwService.GetGpuName(gpuName);
            UpdateHardwareName(current, resolvedName);
            if (rawMb == null) return "N/A";
            double gb = rawMb.Value / 1024.0;
            return current.RemoveNumberTrailing == true ? $"{(int)gb}" : $"{gb:F1}";
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return "N/A";
        }
    }

    private string FetchVRAMStat()
    {
        var current = GetStat(StatsComponentType.VRAM);
        if (current == null) return "N/A";
        try
        {
            string gpuName = GetDedicatedGPUNameForHardwareCall();
            string sensorName = StaticSettings.GPU3DVRAMHook ? "D3D Dedicated Memory Used" : "GPU Memory Used";
            float? rawMb = _hwService.GetGpuVramUsed(gpuName, sensorName);
            string? resolvedName = _hwService.GetGpuName(gpuName);
            UpdateHardwareName(current, resolvedName);
            if (rawMb == null) return "N/A";
            double gb = rawMb.Value / 1024.0;
            return current.RemoveNumberTrailing == true ? $"{(int)gb}" : $"{gb:F1}";
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return "N/A";
        }
    }

    private TimeSpan HardwareNameCacheTtl
    {
        get
        {
            var ttl = _hwService.StatsTickInterval;
            return ttl > MinHardwareNameCacheTtl ? ttl : MinHardwareNameCacheTtl;
        }
    }

    private string? GetCachedCpuName()
    {
        var now = DateTime.UtcNow;
        if (now - _cpuNameCachedAtUtc < HardwareNameCacheTtl)
            return _cachedCpuName;

        _cachedCpuName = _hwService.GetCpuName();
        _cpuNameCachedAtUtc = now;
        return _cachedCpuName;
    }

    private string? GetDedicatedGPUName()
    {
        var now = DateTime.UtcNow;
        if (now - _gpuNameCachedAtUtc < HardwareNameCacheTtl)
            return _cachedGpuName;

        _cachedGpuName = ResolveDedicatedGPUName();
        _gpuNameCachedAtUtc = now;
        return _cachedGpuName;
    }

    private string GetDedicatedGPUNameForHardwareCall() =>
        // IHardwareMonitorService's per-GPU accessors declare a non-nullable name, but resolve
        // an absent one to the primary adapter internally, same as GetDedicatedGPUName's own contract.
        GetDedicatedGPUName()!;

    private string? ResolveDedicatedGPUName()
    {
        try
        {
            EnsureGpuListLoaded();

            if (string.IsNullOrEmpty(StaticSettings.SelectedGPU) || StaticSettings.AutoSelectGPU)
            {
                string? resolved = _hwService.GetGpuName(string.Empty);
                if (!string.IsNullOrEmpty(resolved) &&
                    !string.Equals(StaticSettings.SelectedGPU, resolved, StringComparison.Ordinal))
                {
                    StaticSettings.SelectedGPU = resolved;
                }
                return resolved;
            }
            else
            {
                string? resolved = _hwService.GetGpuName(StaticSettings.SelectedGPU);
                if (!string.IsNullOrEmpty(resolved))
                    return resolved;

                string? fallback = _hwService.GetGpuName(string.Empty);
                Logging.WriteInfo(
                    $"Selected GPU '{StaticSettings.SelectedGPU}' no longer matches any adapter; " +
                    $"falling back to '{fallback ?? "none"}'.");

                if (!string.IsNullOrEmpty(fallback))
                    StaticSettings.SelectedGPU = fallback;

                return fallback;
            }
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return null;
        }
    }

    private void InitializeDefaultStats()
    {
        try
        {
            foreach (StatsComponentType type in SupportedComponentTypes)
            {
                var unit = "";
                switch (type)
                {
                    case StatsComponentType.CPU:
                        unit = "﹪";
                        break;
                    case StatsComponentType.GPU:
                        unit = "﹪";
                        break;
                    case StatsComponentType.RAM:
                        unit = "ᵍᵇ";
                        break;
                    case StatsComponentType.VRAM:
                        unit = "ᵍᵇ";
                        break;
                }

                var component = new ComponentStatsItem(
                    type.ToString(),
                    type.GetSmallName(),
                    type,
                    "",
                    "",
                    !(type == StatsComponentType.GPU || type == StatsComponentType.CPU),
                    unit
                );

                if (type == StatsComponentType.CPU)
                {
                    component.ShowWattage = false;
                    component.ShowTemperature = false;
                }

                if (type == StatsComponentType.GPU)
                {
                    component.ShowWattage = true;
                    component.ShowTemperature = false;
                }

                if (type == StatsComponentType.VRAM || type == StatsComponentType.RAM)
                {
                    component.RemoveNumberTrailing = false;
                    component.IsEnabled = false;
                }
                _componentStats.Add(component);
            }
            RebuildStatsByType();
            _dispatcher.BeginInvoke(() =>
            {
                started = true;
            });
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex);
        }
    }

    private void PerformStopActions()
    {
        _integrationDisplay.ComponentStatsRunning = false;

        if (_hwService.IsOpen)
        {
            _integrationDisplay.ComponentStatsPhase = ComponentStatsPhase.Stopping;
            StopMonitoringComponents();
        }

        _integrationDisplay.ComponentStatsPhase = ComponentStatsPhase.Off;
        _integrationDisplay.ComponentStatsLastUpdate = null;
    }

    private void PerformUpdateActions()
    {
        EnsureComponentStatsLoaded();

        bool hardwareAccessApproved = _consentService.IsApproved(PrivacyHook.HardwareMonitor);

        if (!hardwareAccessApproved)
        {
            if (_hwService.IsOpen)
                _hwService.Close();

            _integrationDisplay.ComponentStatsRunning = false;
            _integrationDisplay.ComponentStatsPhase = ComponentStatsPhase.Off;
            _integrationDisplay.ComponentStatsLastUpdate = null;
            _integrationDisplay.ComponentStatCombined = string.Empty;
            _lastReadings = null;
            return;
        }

        if (!_hwService.IsOpen)
        {
            _integrationDisplay.ComponentStatsPhase = ComponentStatsPhase.Starting;
            StartMonitoringComponents();

            if (!_hwService.IsOpen)
            {
                _integrationDisplay.ComponentStatsRunning = false;
                return;
            }
        }

        _integrationDisplay.ComponentStatsRunning = true;
        _integrationDisplay.ComponentStatsPhase = ComponentStatsPhase.Running;

        EnsureGpuListLoaded();
        _hwService.UpdateAll();
        StatsVm.SyncComponentStatsList();
        QueueDdrVersionFetchIfNeeded();

        if (UpdateStats())
            _integrationDisplay.ComponentStatCombined = StatsVm.Module.GenerateStatsDescription();
    }

    private bool ShouldUpdateComponentStats()
    {
        var intgr = _integrationSettings;
        return intgr.IntgrComponentStats && intgr.IntgrComponentStats_VR && _appState.IsVRRunning ||
               intgr.IntgrComponentStats && intgr.IntgrComponentStats_DESKTOP && !_appState.IsVRRunning;
    }

    public void ActivateStateState(StatsComponentType type, bool state)
    {
        var item = GetStat(type);
        if (item != null)
        {
            item.IsEnabled = state;
        }
    }

    private ComponentStatsItem? GetStat(StatsComponentType type)
        => _statsByType.TryGetValue(type, out var item) ? item : null;

    private void RebuildStatsByType()
    {
        _statsByType.Clear();
        foreach (var stat in _componentStats)
            _statsByType[stat.ComponentType] = stat;
    }

    #region Writing the readout

    public const string DefaultSeparator = " ¦ ";

    private const string PercentUnit = "﹪";

    public const int MaxSeparatorLength = 8;

    public readonly record struct StatExtra(string Label, bool RaiseLabel, string Value, string Unit);

    public sealed record StatReading
    {
        public required string Name { get; init; }

        public required string ShortName { get; init; }

        public bool RaiseName { get; init; }

        public required string Value { get; init; }

        public string? Max { get; init; }

        public string Unit { get; init; } = string.Empty;

        public string Suffix { get; init; } = string.Empty;

        public IReadOnlyList<StatExtra> CoreExtras { get; init; } = [];

        public IReadOnlyList<StatExtra> OtherExtras { get; init; } = [];
    }

    public readonly record struct StatsDetail(
        bool LongNames,
        bool Capacity,
        bool AllExtras,
        bool CoreExtras,
        bool Fraction)
    {
        public static StatsDetail Full => new(true, true, true, true, true);
        public static StatsDetail ShortNames => new(false, true, true, true, true);
        public static StatsDetail NoCapacity => new(false, false, true, true, true);
        public static StatsDetail CoreOnly => new(false, false, false, true, true);
        public static StatsDetail LoadsOnly => new(false, false, false, false, true);
        public static StatsDetail Bare => new(false, false, false, false, false);
    }

    public static string ClampSeparator(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
            return DefaultSeparator;

        if (configured.Length <= MaxSeparatorLength)
            return configured;

        int cut = MaxSeparatorLength;
        if (char.IsHighSurrogate(configured[cut - 1]))
            cut--;

        return configured[..cut];
    }

    public static string Render(IReadOnlyList<StatReading> readings, string separator, StatsDetail detail)
    {
        if (readings is null || readings.Count == 0)
            return string.Empty;

        var written = new List<string>(readings.Count);
        foreach (StatReading reading in readings)
        {
            string text = RenderReading(reading, detail);
            if (text.Length > 0)
                written.Add(text);
        }

        return string.Join(ClampSeparator(separator), written);
    }

    public static string FitToBudget(IReadOnlyList<StatReading> readings, string separator, int budget)
        => SegmentWriter.Fit(
            budget,
            () => Render(readings, separator, StatsDetail.Full),
            () => Render(readings, separator, StatsDetail.ShortNames),
            () => Render(readings, separator, StatsDetail.NoCapacity),
            () => Render(readings, separator, StatsDetail.CoreOnly),
            () => Render(readings, separator, StatsDetail.LoadsOnly),
            () => Render(readings, separator, StatsDetail.Bare));

    private static string RenderReading(StatReading reading, StatsDetail detail)
    {
        var parts = new List<OscText>(16);

        string name = detail.LongNames ? reading.Name : reading.ShortName;
        if (!string.IsNullOrWhiteSpace(name))
        {
            parts.Add(reading.RaiseName ? OscText.Label(name) : OscText.Raw(name.Trim() + ":"));
        }

        string value = Number(reading.Value, detail);
        if (detail.Capacity && !string.IsNullOrEmpty(reading.Max))
            value += "/" + Number(reading.Max, detail);

        parts.Add(OscText.Value(value));
        parts.Add(OscText.Unit(reading.Unit));

        if (detail.Capacity)
            parts.Add(OscText.Raw(reading.Suffix));

        if (detail.CoreExtras)
            AddExtras(parts, reading.CoreExtras, detail);
        if (detail.AllExtras)
            AddExtras(parts, reading.OtherExtras, detail);

        return new SegmentWriter().Field(parts.ToArray()).Text;
    }

    private static void AddExtras(List<OscText> parts, IReadOnlyList<StatExtra> extras, StatsDetail detail)
    {
        foreach (StatExtra extra in extras)
        {
            parts.Add(extra.RaiseLabel ? OscText.Label(extra.Label) : OscText.Raw(extra.Label));
            parts.Add(OscText.Value(Number(extra.Value, detail)));
            parts.Add(OscText.Unit(extra.Unit));
        }
    }

    private static string Number(string? value, StatsDetail detail)
    {
        string text = value ?? string.Empty;
        if (detail.Fraction)
            return text;

        int point = text.IndexOfAny(['.', ',']);
        return point > 0 ? text[..point] : text;
    }

    private string GetEffectiveSeparator() => ClampSeparator(Settings.StatsSeparator);

    public string GenerateStatsDescription()
    {
        var readings = CollectReadings();
        _lastReadings = readings;

        _integrationDisplay.ComponentStatsLastUpdate = DateTime.Now;

        return Render(readings, GetEffectiveSeparator(), StatsDetail.Full);
    }

    public string WriteWithin(int budget)
    {
        var readings = _lastReadings;
        return readings is null ? string.Empty : FitToBudget(readings, GetEffectiveSeparator(), budget);
    }

    private IReadOnlyList<StatReading> CollectReadings()
    {
        QueueDdrVersionFetchIfNeeded();

        var readings = new List<StatReading>(StatDisplayOrder.Length);

        bool hasCpu = GetCachedCpuName() != null;
        bool hasGpu = GetDedicatedGPUName() != null;

        foreach (var type in StatDisplayOrder)
        {
            var stat = GetStat(type);
            if (stat == null || !stat.IsEnabled || !stat.Available)
                continue;

            var core = new List<StatExtra>(3);
            var other = new List<StatExtra>(5);

            if (stat.ComponentType == StatsComponentType.CPU && hasCpu)
            {
                if (stat.ShowWattage) stat.ShowWattage = false;
                if (stat.ShowTemperature) stat.ShowTemperature = false;
                if (stat.cantShowWattage) stat.cantShowWattage = false;
                if (stat.cantShowTemperature) stat.cantShowTemperature = false;
            }
            else if (stat.ComponentType == StatsComponentType.GPU && hasGpu)
            {
                Collect(core, stat.ShowTemperature, () => FetchTemperatureStat(stat));
                Collect(core, stat.ShowHotSpotTemperature, () => FetchHotspotTemperatureStat(stat));
                Collect(core, stat.ShowWattage, () => FetchPowerStat(stat));

                Collect(other, Settings.ShowGpuMemoryTemperature, () => FetchGpuMemoryTemperatureStat(stat));
                Collect(other, Settings.ShowGpuFanSpeed, () => FetchGpuFanSpeedStat(stat));
                Collect(other, Settings.ShowGpuCoreClock, () => FetchGpuCoreClockStat(stat));
                Collect(other, Settings.ShowGpuMemoryClock, () => FetchGpuMemoryClockStat(stat));
                Collect(other, Settings.ShowGpuMemoryLoad, () => FetchGpuMemoryLoadStat(stat));
            }

            readings.Add(ToReading(stat, core, other));
        }

        return readings;
    }

    private static void Collect(List<StatExtra> into, bool show, Func<StatExtra?> fetch)
    {
        if (!show)
            return;

        if (fetch() is { } extra)
            into.Add(extra);
    }

    private static StatReading ToReading(
        ComponentStatsItem stat,
        IReadOnlyList<StatExtra> core,
        IReadOnlyList<StatExtra> other)
    {
        bool custom = stat.ReplaceWithHardwareName && !string.IsNullOrWhiteSpace(stat.CustomHardwarenameValue);
        string? name = stat.ShowPrefixHardwareTitle
            ? custom ? stat.CustomHardwarenameValue : stat.HardwareFriendlyName
            : stat.SystemMainName;

        bool showDdr = stat.ComponentType == StatsComponentType.RAM
                       && stat.ShowDDRVersion
                       && !string.IsNullOrWhiteSpace(stat.DDRVersion);

        return new StatReading
        {
            Name = string.IsNullOrWhiteSpace(name) ? stat.SystemMainName : name,
            ShortName = stat.SystemMainName,
            RaiseName = stat.ShowSmallName,
            Value = stat.ComponentValue ?? string.Empty,
            Max = stat.ShowMaxValue ? stat.ComponentValueMax : null,
            Unit = stat.ShowUnit ? stat.Unit ?? string.Empty : string.Empty,
            Suffix = showDdr ? $"⁽{stat.DDRVersion}⁾" : string.Empty,
            CoreExtras = core,
            OtherExtras = other,
        };
    }

    #endregion

    public IReadOnlyList<ComponentStatsItem> GetAllStats()
    {
        EnsureComponentStatsLoaded();
        return _componentStats.AsReadOnly();
    }

    public string? GetCustomHardwareName(StatsComponentType type)
    {
        var item = GetStat(type);
        return item?.CustomHardwarenameValue;
    }

    public string? GetDDRVersion()
    {
        EnsureComponentStatsLoaded();
        string? plain = _hwService.GetDdrVersion();
        return ToSuperscript(plain);
    }

    private static string? ToSuperscript(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (char c in text)
        {
            sb.Append(c switch
            {
                'D' => 'ᴰ',
                'R' => 'ᴿ',
                '1' => '¹',
                '2' => '²',
                '3' => '³',
                '4' => '⁴',
                '5' => '⁵',
                _ => c,
            });
        }
        return sb.ToString();
    }

    public string? GetHardwareName(StatsComponentType type)
    {
        var item = GetStat(type);
        return item?.HardwareFriendlyName;
    }

    public bool GetHardwareTitleState(StatsComponentType type)
    {
        var item = GetStat(type);
        return item?.ShowPrefixHardwareTitle ?? false;
    }

    public bool GetRemoveNumberTrailing(StatsComponentType type)
    {
        var item = GetStat(type);
        return item?.RemoveNumberTrailing ?? false;
    }

    public bool GetShowGPUHotspotTemperature()
    {
        var item = GetStat(StatsComponentType.GPU);
        return item?.ShowHotSpotTemperature ?? false;
    }

    public bool GetShowGPUTemperature()
    {
        var item = GetStat(StatsComponentType.GPU);
        return item?.ShowTemperature ?? false;
    }

    public bool GetShowGPUWattage()
    {
        var item = GetStat(StatsComponentType.GPU);
        return item?.ShowWattage ?? false;
    }

    public bool GetShowMaxValue(StatsComponentType type)
    {
        var item = GetStat(type);
        return item?.ShowMaxValue ?? false;
    }

    public bool GetShowRamDDRVersion()
    {
        EnsureComponentStatsLoaded();
        var item = GetStat(StatsComponentType.RAM);
        return item?.ShowDDRVersion ?? false;
    }

    public bool GetShowReplaceWithHardwareName(StatsComponentType type)
    {
        var item = GetStat(type);
        return item?.ReplaceWithHardwareName ?? false;
    }

    public bool GetShowSmallName(StatsComponentType type)
    {
        var item = GetStat(type);
        return item?.ShowSmallName ?? false;
    }

    public string? GetStatMaxValue(StatsComponentType type)
    {
        var item = GetStat(type);
        return item?.ComponentValueMax;
    }

    public string? GetStatValue(StatsComponentType type)
    {
        var item = GetStat(type);
        return item?.ComponentValue;
    }

    public string GetWhitchComponentsAreNotAvailableString()
    {
        List<string> notAvailableComponents = new List<string>();
        foreach (var item in _componentStats)
        {
            if (!item.Available && item.IsEnabled)
            {
                notAvailableComponents.Add(item.ComponentType.ToString());
            }
        }

        if (notAvailableComponents.Count == 0)
        {
            return "";
        }

        string names = string.Join(", ", notAvailableComponents);

        if (!_hwService.VendorGpuSensorsEnabled &&
            notAvailableComponents.Contains(StatsComponentType.GPU.ToString()))
        {
            return $"{names} stats need GPU sensors, which are switched off in settings.";
        }

        return $"No readings yet for {names}. If this persists, make sure your GPU driver is "
             + "installed and up to date — the log line starting \"Hardware monitor:\" says what was detected.";
    }

    public bool IsStatAvailable(StatsComponentType type)
    {
        var item = GetStat(type);
        return item?.Available ?? false;
    }

    public bool IsStatEnabled(StatsComponentType type)
    {
        var item = GetStat(type);
        return item?.IsEnabled ?? false;
    }

    public bool IsStatMaxValueShown(StatsComponentType type)
    {
        var item = GetStat(type);
        return item?.ShowMaxValue ?? false;
    }

    public bool IsThereAComponentThatIsNotAvailable()
    {
        foreach (var item in _componentStats)
        {
            if (!item.Available && item.IsEnabled)
            {
                return true;
            }
        }
        return false;
    }

    public bool IsThereAComponentThatIsNotGettingTempOrWattage()
    {
        foreach (var item in _componentStats)
        {
            if (item.Available && item.IsEnabled && item.ComponentType == StatsComponentType.GPU && (item.cantShowWattage && item.ShowWattage || item.cantShowTemperature && item.ShowTemperature) == true)
            {
                return true;
            }
        }
        return false;
    }

    public bool IsVRRunning()
    {
        try
        {
            bool isSteamVRRunning = IsVrProcessPresent("vrmonitor");
            bool isOculusRunning = AS.CountOculusSystemAsVR && IsVrProcessPresent("OVRServer_x64");

            bool isVRRunning = isSteamVRRunning || isOculusRunning;

            if (isVRRunning != _appState.IsVRRunning)
            {
                _dispatcher.BeginInvoke(() => _appState.IsVRRunning = isVRRunning);
            }

            return isVRRunning;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return false;
        }
    }

    private bool IsVrProcessPresent(string processName)
    {
        if (_processPresence != null)
            return _processPresence.IsRunning(processName);

        var processes = Process.GetProcessesByName(processName);
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes)
                process.Dispose();
        }
    }

    public void LoadComponentStats()
    {
        try
        {
            FileName = Path.Combine(_env.DataPath, "ComponentStatsV1.json");
            if (!File.Exists(FileName))
            {
                InitializeDefaultStats();
                SaveComponentStats();
                return;
            }

            var jsonData = File.ReadAllText(FileName);

            if (string.IsNullOrWhiteSpace(jsonData) || jsonData.All(c => c == '\0'))
            {
                Logging.WriteException(new Exception("The component stats file is empty or corrupted."), MSGBox: false);
                InitializeDefaultStats();
                return;
            }

            var loadedStats = JsonConvert.DeserializeObject<List<ComponentStatsItem>>(jsonData);
            if (loadedStats != null)
            {
                var filtered = loadedStats
                    .Where(s => s.ComponentType != StatsComponentType.FPS)
                    .GroupBy(s => s.ComponentType)
                    .ToDictionary(g => g.Key, g => g.First());

                _componentStats.Clear();
                bool needsResave = false;

                foreach (var type in SupportedComponentTypes)
                {
                    if (filtered.TryGetValue(type, out var existing))
                    {
                        if (existing.ComponentType == StatsComponentType.CPU)
                        {
                            if (existing.ShowWattage) { existing.ShowWattage = false; needsResave = true; }
                            if (existing.ShowTemperature) { existing.ShowTemperature = false; needsResave = true; }
                            if (existing.cantShowWattage) { existing.cantShowWattage = false; needsResave = true; }
                            if (existing.cantShowTemperature) { existing.cantShowTemperature = false; needsResave = true; }
                        }
                        _componentStats.Add(existing);
                    }
                    else
                    {
                        var unit = type == StatsComponentType.CPU || type == StatsComponentType.GPU ? "﹪" : "ᵍᵇ";
                        var item = new ComponentStatsItem(
                            type.ToString(), type.GetSmallName(), type, "", "", false, unit)
                        {
                            IsEnabled = false,
                            RemoveNumberTrailing = type == StatsComponentType.RAM || type == StatsComponentType.VRAM ? false : true,
                        };
                        _componentStats.Add(item);
                        needsResave = true;
                    }
                }

                RebuildStatsByType();

                if (needsResave || loadedStats.Any(s => s.ComponentType == StatsComponentType.FPS))
                    SaveComponentStats();
            }
            else
            {
                Logging.WriteException(new Exception("Failed to deserialize component stats."), MSGBox: false);
                _toast?.Show("⚙️ Hardware Monitor", "Component stats file is corrupt — reset to defaults.", ToastType.Warning, key: "hw-stats-corrupt");
                InitializeDefaultStats();
            }
        }
        catch (JsonException ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            _toast?.Show("⚙️ Hardware Monitor", "Component stats file is corrupt — reset to defaults.", ToastType.Warning, key: "hw-stats-corrupt");
            if (_componentStats.Count == 0)
                InitializeDefaultStats();
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            _toast?.Show("⚙️ Hardware Monitor", "Failed to load component stats configuration.", ToastType.Error, key: "hw-stats-load-failed");
            if (_componentStats.Count == 0)
                InitializeDefaultStats();
        }
    }

    public void SaveComponentStats()
    {
        try
        {
            if (_componentStats == null || _componentStats.Count == 0) return;
            var jsonData = JsonConvert.SerializeObject(_componentStats);
            if (!AtomicFileWriter.WriteAllText(FileName ?? string.Empty, jsonData))
                Logging.WriteInfo("Failed to save component stats.");
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex);
        }
    }

    public void SetCustomHardwareName(StatsComponentType type, string? name)
    {
        var item = GetStat(type);
        if (item != null)
        {
            item.CustomHardwarenameValue = name;
        }
    }

    public void SetHardwareTitle(StatsComponentType type, bool state)
    {
        var item = GetStat(type);
        if (item != null)
        {
            item.ShowPrefixHardwareTitle = state;
        }
    }

    public void SetRemoveNumberTrailing(StatsComponentType type, bool state)
    {
        var item = GetStat(type);
        if (item != null)
        {
            item.RemoveNumberTrailing = state;
        }
    }

    public void SetReplaceWithHardwareName(StatsComponentType type, bool state)
    {
        var item = GetStat(type);
        if (item != null)
        {
            item.ReplaceWithHardwareName = state;
        }
    }

    public void SetShowGPUHotspotTemperature(bool state)
    {
        var item = GetStat(StatsComponentType.GPU);
        if (item != null)
        {
            item.ShowHotSpotTemperature = state;
        }
    }

    public void SetShowGPUTemperature(bool state)
    {
        var item = GetStat(StatsComponentType.GPU);
        if (item != null)
        {
            item.ShowTemperature = state;
        }
    }

    public void SetShowGPUWattage(bool state)
    {
        var item = GetStat(StatsComponentType.GPU);
        if (item != null)
        {
            item.ShowWattage = state;
        }
    }

    public void SetShowMaxValue(StatsComponentType type, bool state)
    {
        var item = GetStat(type);
        if (item != null)
        {
            item.ShowMaxValue = state;
        }
    }

    public void SetShowRamDDRVersion(bool state)
    {
        EnsureComponentStatsLoaded();
        var item = GetStat(StatsComponentType.RAM);
        if (item != null)
        {
            item.ShowDDRVersion = state;
            if (state)
                QueueDdrVersionFetchIfNeeded();
        }
    }

    public void SetShowSmallName(StatsComponentType type, bool state)
    {
        var item = GetStat(type);
        if (item != null)
        {
            item.ShowSmallName = state;
        }
    }

    public void SetStatAvailable(StatsComponentType type, bool available)
    {
        var item = GetStat(type);
        if (item != null)
        {
            item.Available = available;
        }
    }

    public void SetStatMaxValue(StatsComponentType type, string maxValue)
    {
        var item = GetStat(type);
        if (item != null)
        {
            item.ComponentValueMax = maxValue;
        }
    }

    public void SetStatMaxValueShown(StatsComponentType type, bool state)
    {
        var item = GetStat(type);
        if (item != null)
        {
            item.ShowMaxValue = state;
        }
    }

    public void StartModule()
    {
        EnsureComponentStatsLoaded();
        _dispatcher.BeginInvoke(() => _statsVm?.SyncComponentStatsList());
        QueueDdrVersionFetchIfNeeded();
    }

    private bool _loggedHardwareStatus;

    public void StartMonitoringComponents()
    {
        try
        {
            _hwService.VendorGpuSensorsEnabled = StaticSettings.EnableVendorGpuSensors;
            _hwService.StatsTickInterval = TimeSpan.FromSeconds(AS.ScanningInterval);
            _hwService.Open();
            RefreshGpuList();

            if (!_loggedHardwareStatus)
            {
                _loggedHardwareStatus = true;
                Logging.WriteInfo($"Hardware monitor: {_hwService.GetHardwareMonitorStatusMessage()}");
            }
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
    }

    private void RefreshGpuList()
    {
        _gpuListLoaded = true;
        var gpus = _hwService.GetAvailableGpus();
        _dispatcher.BeginInvoke(() =>
        {
            if (!GPUList.SequenceEqual(gpus))
            {
                var selectedGpu = StaticSettings.SelectedGPU;
                GPUList.Clear();
                foreach (var gpu in gpus)
                    GPUList.Add(gpu);
                StaticSettings.SelectedGPU = selectedGpu;
            }
        });
    }

    public void StopMonitoringComponents()
    {
        try
        {
            StatsVm.SyncComponentStatsList();
            StatsVm.Module.SaveComponentStats();
            _hwService.Close();
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
    }

    public void StopAndClear()
    {
        PerformStopActions();
        _integrationDisplay.ComponentStatCombined = string.Empty;
        _lastReadings = null;
    }

    public void TickAndUpdate()
    {
        if (ShouldUpdateComponentStats())
        {
            PerformUpdateActions();
        }
        else
        {
            PerformStopActions();
        }
    }

    public void ToggleStatEnabledStatus(StatsComponentType type)
    {
        var item = GetStat(type);
        if (item != null)
        {
            item.IsEnabled = !item.IsEnabled;
        }
    }

    public bool UpdateStats()
    {
        void UpdateComponentStats(StatsComponentType type, Func<string> fetchStat, Func<string>? fetchMaxStat = null)
        {
            var statItem = GetStat(type);
            if (statItem == null || !statItem.IsEnabled) return;

            string value = fetchStat();
            string? maxValue = fetchMaxStat?.Invoke();

            if (!value.Contains("N/A"))
            {
                StatsVm.UpdateComponentStat(type, value);
                SetAvailability(type, true);
            }
            else
            {
                SetAvailability(type, false);
            }

            if (maxValue != null && !maxValue.Contains("N/A"))
            {
                StatsVm.SetComponentStatMaxValue(type, maxValue);
                SetAvailability(type, true);
            }
            else if (maxValue != null && statItem.ShowMaxValue)
            {
                SetAvailability(type, false);
            }
        }

        void SetAvailability(StatsComponentType type, bool value)
        {
            switch (type)
            {
                case StatsComponentType.CPU:
                    StatsVm.isCPUAvailable = value;
                    break;
                case StatsComponentType.GPU:
                    StatsVm.IsGPUAvailable = value;
                    break;
                case StatsComponentType.RAM:
                    StatsVm.isRAMAvailable = value;
                    break;
                case StatsComponentType.VRAM:
                    StatsVm.isVRAMAvailable = value;
                    break;
            }
        }
        try
        {
            UpdateComponentStats(StatsComponentType.CPU, FetchCPUStat);
            UpdateComponentStats(StatsComponentType.GPU, FetchGPUStat);
            var ramResult = FetchRAMStats();
            UpdateComponentStats(StatsComponentType.RAM, () => ramResult.UsedMemory, () => ramResult.MaxMemory);
            UpdateComponentStats(StatsComponentType.VRAM, FetchVRAMStat, FetchVRAMMaxStat);
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return false;
        }
        return true;
    }

    public void UpdateStatValue(StatsComponentType type, string newValue)
    {
        var item = GetStat(type);
        if (item != null)
        {
            item.ComponentValue = newValue;
            item.LastUpdated = DateTime.Now;
        }
    }
}
