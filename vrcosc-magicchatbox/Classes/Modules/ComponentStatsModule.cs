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
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Polls hardware sensors (CPU, GPU, RAM, VRAM) and generates the stats description string for the VRChat chatbox.
/// </summary>
public class ComponentStatsModule : IModule
{
    private string FileName = null;

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

    // GPU list cache — ObservableCollection so the ComboBox binding updates when items arrive
    public ObservableCollection<string> GPUList { get; } = new();

    private readonly IHardwareMonitorService _hwService;
    private readonly List<ComponentStatsItem> _componentStats = new List<ComponentStatsItem>();
    private string _ramDDRVersion = "Unknown";
    public bool started = false;

    private readonly ISettingsProvider<ComponentStatsSettings> _settingsProvider;
    public ComponentStatsSettings Settings => _settingsProvider.Value;
    public void SaveSettings() => _settingsProvider.Save();

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

    private ComponentStatsViewModel _statsVm;
    private ComponentStatsViewModel StatsVm => _statsVm;

    /// <summary>
    /// Late-bound setter for ComponentStatsViewModel (avoids circular dependency).
    /// </summary>
    public void SetStatsViewModel(ComponentStatsViewModel vm) => _statsVm = vm;

    private IntegrationSettings _integrationSettings;

    private readonly IUiDispatcher _dispatcher;
    private readonly Lazy<IStatePersistenceCoordinator> _persistence;
    private readonly IPrivacyConsentService _consentService;

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
        IPrivacyConsentService consentService)
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

        _consentService.ConsentChanged += (_, e) =>
        {
            if (e.Hook == PrivacyHook.HardwareMonitor && e.NewState == ConsentState.Denied)
            {
                if (_hwService.IsOpen)
                    _hwService.Close();
                _integrationDisplay.ComponentStatCombined = string.Empty;
            }
        };
    }

    private void FetchAndStoreDDRVersion()
    {
        _ramDDRVersion = GetDDRVersion();
        var ramItem = _componentStats.FirstOrDefault(stat => stat.ComponentType == StatsComponentType.RAM);
        if (ramItem != null)
        {
            ramItem.DDRVersion = _ramDDRVersion;
        }
    }

    private string FetchCPUStat()
    {
        var current = StatsVm.ComponentStatsList.FirstOrDefault(s => s.ComponentType == StatsComponentType.CPU);
        if (current == null) return "N/A";
        try
        {
            float? load = _hwService.GetCpuLoad();
            string name = _hwService.GetCpuName();
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

    private string FetchCPUMaxCoreLoadStat(ComponentStatsItem item)
    {
        float? maxCore = _hwService.GetCpuMaxCoreLoad();
        if (maxCore == null) return null;
        string label = Settings.UseEmojisForTempAndPower ? "🔺" : "max core";
        if (item.ShowSmallName && !Settings.UseEmojisForTempAndPower)
            label = TextUtilities.TransformToSuperscript(label);
        string value = item.RemoveNumberTrailing ? $"{(int)maxCore.Value}" : $"{maxCore.Value:F1}";
        return $"{label} {value}﹪";
    }

    private string FetchGpuCoreClockStat(ComponentStatsItem item)
    {
        string gpuName = GetDedicatedGPUName();
        float? mhz = _hwService.GetGpuCoreClock(gpuName);
        if (mhz == null) return null;
        string label = Settings.UseEmojisForTempAndPower ? "🔄" : "core clk";
        if (item.ShowSmallName && !Settings.UseEmojisForTempAndPower)
            label = TextUtilities.TransformToSuperscript(label);
        string value = item.RemoveNumberTrailing ? $"{(int)mhz.Value}" : $"{mhz.Value:F0}";
        return $"{label} {value}MHz";
    }

    private string FetchGpuFanSpeedStat(ComponentStatsItem item)
    {
        string gpuName = GetDedicatedGPUName();
        float? rpm = _hwService.GetGpuFanSpeed(gpuName);
        if (rpm == null) return null;
        string label = Settings.UseEmojisForTempAndPower ? "🌀" : "fan";
        if (item.ShowSmallName && !Settings.UseEmojisForTempAndPower)
            label = TextUtilities.TransformToSuperscript(label);
        string value = item.RemoveNumberTrailing ? $"{(int)rpm.Value}" : $"{rpm.Value:F0}";
        return $"{label} {value}RPM";
    }

    private string FetchGpuMemoryClockStat(ComponentStatsItem item)
    {
        string gpuName = GetDedicatedGPUName();
        float? mhz = _hwService.GetGpuMemoryClock(gpuName);
        if (mhz == null) return null;
        string label = Settings.UseEmojisForTempAndPower ? "💾" : "mem clk";
        if (item.ShowSmallName && !Settings.UseEmojisForTempAndPower)
            label = TextUtilities.TransformToSuperscript(label);
        string value = item.RemoveNumberTrailing ? $"{(int)mhz.Value}" : $"{mhz.Value:F0}";
        return $"{label} {value}MHz";
    }

    private string FetchGpuMemoryLoadStat(ComponentStatsItem item)
    {
        string gpuName = GetDedicatedGPUName();
        float? load = _hwService.GetGpuMemoryLoad(gpuName);
        if (load == null) return null;
        string label = Settings.UseEmojisForTempAndPower ? "📊" : "mem load";
        if (item.ShowSmallName && !Settings.UseEmojisForTempAndPower)
            label = TextUtilities.TransformToSuperscript(label);
        string value = item.RemoveNumberTrailing ? $"{(int)load.Value}" : $"{load.Value:F1}";
        return $"{label} {value}﹪";
    }

    private string FetchGpuMemoryTemperatureStat(ComponentStatsItem item)
    {
        string gpuName = GetDedicatedGPUName();
        float? rawCelsius = _hwService.GetGpuMemoryTemperature(gpuName);
        if (rawCelsius == null || rawCelsius == 0) return null;
        string unit = Settings.TemperatureUnit;
        double temperature = unit == "F" ? rawCelsius.Value * 9.0 / 5.0 + 32 : rawCelsius.Value;
        if (item.RemoveNumberTrailing)
            temperature = Math.Round(temperature);
        string unitSymbol = unit == "F" ? "°F" : "°C";
        string tempText = Settings.UseEmojisForTempAndPower ? "🧊" : "mem temp";
        if (item.ShowSmallName && !Settings.UseEmojisForTempAndPower)
            tempText = TextUtilities.TransformToSuperscript(tempText);
        string formattedTemperature = item.RemoveNumberTrailing ? $"{(int)temperature}" : $"{temperature:F1}";
        return $"{tempText} {formattedTemperature}{TextUtilities.TransformToSuperscript(unitSymbol)}";
    }

    private string FetchGPUStat()
    {
        var current = StatsVm.ComponentStatsList.FirstOrDefault(s => s.ComponentType == StatsComponentType.GPU);
        if (current == null) return "N/A";
        try
        {
            string gpuName = GetDedicatedGPUName();
            string sensorName = StaticSettings.GPU3DHook ? "D3D 3D" : "GPU Core";
            float? load = _hwService.GetGpuLoad(gpuName, sensorName);
            string resolvedName = _hwService.GetGpuName(gpuName);
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



    private string FetchHotspotTemperatureStat(ComponentStatsItem item)
    {
        string gpuName = GetDedicatedGPUName();
        float? rawCelsius = _hwService.GetGpuHotspotTemperature(gpuName);

        if (rawCelsius == null || rawCelsius == 0)
        {
            item.cantShowHotSpotTemperature = true;
            return "N/A";
        }

        string unit = StaticSettings.TemperatureUnit;
        double temperature = unit == "F" ? rawCelsius.Value * 9.0 / 5.0 + 32 : rawCelsius.Value;

        if (item.RemoveNumberTrailing)
            temperature = Math.Round(temperature);

        string unitSymbol = unit == "F" ? "°F" : "°C";
        string tempText = StaticSettings.UseEmojisForTempAndPower ? "🔥" : "GPU HotSpot";
        if (item.ShowSmallName && !StaticSettings.UseEmojisForTempAndPower)
            tempText = TextUtilities.TransformToSuperscript(tempText);

        string formattedTemperature = item.RemoveNumberTrailing ? $"{(int)temperature}" : $"{temperature:F1}";
        item.cantShowHotSpotTemperature = false;
        return $"{tempText} {formattedTemperature}{TextUtilities.TransformToSuperscript(unitSymbol)}";
    }

    private string FetchPowerStat(bool isCpu, ComponentStatsItem item)
    {
        string gpuName = isCpu ? null : GetDedicatedGPUName();
        float? rawWatts = isCpu ? _hwService.GetCpuPower() : _hwService.GetGpuPower(gpuName);

        if (rawWatts == null || rawWatts == 0)
        {
            item.cantShowWattage = true;
            return "N/A";
        }

        double power = rawWatts.Value;
        if (item.RemoveNumberTrailing)
            power = Math.Round(power);

        string powerUnit = "W";
        string powerText = Settings.UseEmojisForTempAndPower ? "⚡" : "power";
        if (item.ShowSmallName && !Settings.UseEmojisForTempAndPower)
            powerText = TextUtilities.TransformToSuperscript(powerText);

        string formattedPower = item.RemoveNumberTrailing ? $"{(int)power}" : $"{power:F1}";
        item.cantShowWattage = false;
        return $"{powerText} {formattedPower}{TextUtilities.TransformToSuperscript(powerUnit)}";
    }

    private (string UsedMemory, string MaxMemory) FetchRAMStats()
    {
        var current = StatsVm.ComponentStatsList.FirstOrDefault(stat => stat.ComponentType == StatsComponentType.RAM);

        // Try WMI first (more accurate on some systems)
        var wmiMem = _hwService.GetWindowsMemoryInfo();
        if (wmiMem.HasValue)
        {
            string name = _hwService.GetCpuName(); // RAM doesn't have its own hardware name
            if (current?.RemoveNumberTrailing == true)
                return ($"{(int)wmiMem.Value.usedGiB}", $"{(int)wmiMem.Value.totalGiB}");
            else
                return ($"{wmiMem.Value.usedGiB:F1}", $"{wmiMem.Value.totalGiB:F1}");
        }

        // Fallback to LibreHardwareMonitor
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

    /// <summary>
    /// Updates display-name side-effects on a ComponentStatsItem when hardware name is resolved.
    /// </summary>
    private void UpdateHardwareName(ComponentStatsItem current, string hardwareName)
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
            current.CustomHardwarenameValueSmall = TextUtilities.TransformToSuperscript(current.CustomHardwarenameValue);
        }
    }

    private string FetchTemperatureStat(bool isCpu, ComponentStatsItem item)
    {
        string gpuName = isCpu ? null : GetDedicatedGPUName();
        float? rawCelsius = isCpu ? _hwService.GetCpuTemperature() : _hwService.GetGpuTemperature(gpuName);

        if (rawCelsius == null || rawCelsius == 0)
        {
            item.cantShowTemperature = true;
            return "N/A";
        }

        string unit = Settings.TemperatureUnit;
        double temperature = unit == "F" ? rawCelsius.Value * 9.0 / 5.0 + 32 : rawCelsius.Value;

        if (item.RemoveNumberTrailing)
            temperature = Math.Round(temperature);

        string unitSymbol = unit == "F" ? "°F" : "°C";
        string tempText = Settings.UseEmojisForTempAndPower ? "♨️" : "temp";
        if (item.ShowSmallName && !Settings.UseEmojisForTempAndPower)
            tempText = TextUtilities.TransformToSuperscript(tempText);

        string formattedTemperature = item.RemoveNumberTrailing ? $"{(int)temperature}" : $"{temperature:F1}";
        item.cantShowTemperature = false;
        return $"{tempText} {formattedTemperature}{TextUtilities.TransformToSuperscript(unitSymbol)}";
    }

    private string FetchVRAMMaxStat()
    {
        var current = StatsVm.ComponentStatsList.FirstOrDefault(s => s.ComponentType == StatsComponentType.VRAM);
        if (current == null) return "N/A";
        try
        {
            string gpuName = GetDedicatedGPUName();
            string sensorName = StaticSettings.GPU3DVRAMHook ? "D3D Dedicated Memory Total" : "GPU Memory Total";
            float? rawMb = _hwService.GetGpuVramTotal(gpuName, sensorName);
            string resolvedName = _hwService.GetGpuName(gpuName);
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
        var current = StatsVm.ComponentStatsList.FirstOrDefault(s => s.ComponentType == StatsComponentType.VRAM);
        if (current == null) return "N/A";
        try
        {
            string gpuName = GetDedicatedGPUName();
            string sensorName = StaticSettings.GPU3DVRAMHook ? "D3D Dedicated Memory Used" : "GPU Memory Used";
            float? rawMb = _hwService.GetGpuVramUsed(gpuName, sensorName);
            string resolvedName = _hwService.GetGpuName(gpuName);
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


    private string GetDedicatedGPUName()
    {
        try
        {
            if (!GPUList.Any())
                RefreshGpuList();

            if (string.IsNullOrEmpty(StaticSettings.SelectedGPU) || StaticSettings.AutoSelectGPU)
            {
                string resolved = _hwService.GetGpuName(null);
                if (!string.IsNullOrEmpty(resolved))
                    StaticSettings.SelectedGPU = resolved;
                return resolved;
            }
            else
            {
                return _hwService.GetGpuName(StaticSettings.SelectedGPU);
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
                    component.ShowTemperature = true;
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
            _dispatcher.Invoke(() =>
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
            StopMonitoringComponents();
        }
    }

    private void PerformUpdateActions()
    {
        _integrationDisplay.ComponentStatsRunning = true;

        bool driverApproved = _consentService.IsApproved(PrivacyHook.HardwareMonitor);

        if (driverApproved)
        {
            if (!_hwService.IsOpen)
                StartMonitoringComponents();

            _hwService.UpdateAll();
            StatsVm.SyncComponentStatsList();

            if (UpdateStats())
                _integrationDisplay.ComponentStatCombined = StatsVm.Module.GenerateStatsDescription();
        }
        else
        {
            // Basic mode — RAM via performance counter (no kernel driver)
            float? cpuLoad = _hwService.GetCpuLoadBasic();
            var ramInfo = _hwService.GetWindowsMemoryInfo();
            var parts = new System.Collections.Generic.List<string>();

            if (cpuLoad.HasValue)
                parts.Add($"CPU {(int)cpuLoad.Value}%");

            if (ramInfo.HasValue)
                parts.Add($"RAM {ramInfo.Value.usedGiB:F1}/{ramInfo.Value.totalGiB:F1}GB");

            if (parts.Count > 0)
                _integrationDisplay.ComponentStatCombined = string.Join(" · ", parts);
        }
    }

    private bool ShouldUpdateComponentStats()
    {
        var intgr = _integrationSettings;
        return intgr.IntgrComponentStats && intgr.IntgrComponentStats_VR && _appState.IsVRRunning ||
               intgr.IntgrComponentStats && intgr.IntgrComponentStats_DESKTOP && !_appState.IsVRRunning;
    }

    public void ActivateStateState(StatsComponentType type, bool state)
    {
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
        if (item != null)
        {
            item.IsEnabled = state;
        }
    }

    private string GetEffectiveSeparator()
    {
        return string.IsNullOrWhiteSpace(Settings.StatsSeparator) ? " ¦ " : Settings.StatsSeparator;
    }

    /// <summary>
    /// Builds the combined stats string from all enabled component items for display in the chatbox.
    /// </summary>
    public string GenerateStatsDescription()
    {
        List<string> descriptions = new List<string>();

        foreach (var type in StatDisplayOrder)
        {
            var stat = _componentStats.FirstOrDefault(s => s.ComponentType == type && s.IsEnabled && s.Available);
            if (stat != null)
            {
                string componentDescription = stat.GetDescription();
                List<string> additionalInfoParts = new List<string>();

                bool hasCpu = _hwService.GetCpuName() != null;
                bool hasGpu = GetDedicatedGPUName() != null;

                if (stat.ComponentType == StatsComponentType.CPU && hasCpu)
                {
                    string cpuTemp = stat.ShowTemperature ? FetchTemperatureStat(true, stat) : "";
                    string cpuPower = stat.ShowWattage ? FetchPowerStat(true, stat) : "";

                    if (!stat.cantShowTemperature && !string.IsNullOrWhiteSpace(cpuTemp))
                        additionalInfoParts.Add(cpuTemp);
                    if (!stat.cantShowWattage && !string.IsNullOrWhiteSpace(cpuPower))
                        additionalInfoParts.Add(cpuPower);

                    if (Settings.ShowCpuMaxCoreLoad)
                    {
                        var maxCore = FetchCPUMaxCoreLoadStat(stat);
                        if (!string.IsNullOrWhiteSpace(maxCore)) additionalInfoParts.Add(maxCore);
                    }
                }
                else if (stat.ComponentType == StatsComponentType.GPU && hasGpu)
                {
                    string gpuTemp = stat.ShowTemperature ? FetchTemperatureStat(false, stat) : "";
                    string gpuHotSpotTemp = stat.ShowHotSpotTemperature ? FetchHotspotTemperatureStat(stat) : "";
                    string gpuPower = stat.ShowWattage ? FetchPowerStat(false, stat) : "";

                    if (!stat.cantShowTemperature && !string.IsNullOrWhiteSpace(gpuTemp))
                        additionalInfoParts.Add(gpuTemp);
                    if (!stat.cantShowHotSpotTemperature && !string.IsNullOrWhiteSpace(gpuHotSpotTemp))
                        additionalInfoParts.Add(gpuHotSpotTemp);
                    if (!stat.cantShowWattage && !string.IsNullOrWhiteSpace(gpuPower))
                        additionalInfoParts.Add(gpuPower);

                    if (Settings.ShowGpuMemoryTemperature)
                    {
                        var mt = FetchGpuMemoryTemperatureStat(stat);
                        if (!string.IsNullOrWhiteSpace(mt)) additionalInfoParts.Add(mt);
                    }
                    if (Settings.ShowGpuFanSpeed)
                    {
                        var fan = FetchGpuFanSpeedStat(stat);
                        if (!string.IsNullOrWhiteSpace(fan)) additionalInfoParts.Add(fan);
                    }
                    if (Settings.ShowGpuCoreClock)
                    {
                        var clk = FetchGpuCoreClockStat(stat);
                        if (!string.IsNullOrWhiteSpace(clk)) additionalInfoParts.Add(clk);
                    }
                    if (Settings.ShowGpuMemoryClock)
                    {
                        var mclk = FetchGpuMemoryClockStat(stat);
                        if (!string.IsNullOrWhiteSpace(mclk)) additionalInfoParts.Add(mclk);
                    }
                    if (Settings.ShowGpuMemoryLoad)
                    {
                        var ml = FetchGpuMemoryLoadStat(stat);
                        if (!string.IsNullOrWhiteSpace(ml)) additionalInfoParts.Add(ml);
                    }
                }

                if (stat.ComponentType == StatsComponentType.RAM && stat.ShowDDRVersion && !string.IsNullOrWhiteSpace(stat.DDRVersion))
                {
                    componentDescription += $" ⁽{stat.DDRVersion}⁾";
                }

                string additionalInfo = string.Join(" ", additionalInfoParts).Trim();

                string fullComponentInfo = string.IsNullOrWhiteSpace(additionalInfo)
                    ? componentDescription
                    : $"{componentDescription} {additionalInfo}";

                if (!string.IsNullOrEmpty(fullComponentInfo))
                {
                    descriptions.Add(fullComponentInfo);
                }
            }
        }

        _integrationDisplay.ComponentStatsLastUpdate = DateTime.Now;

        return string.Join(GetEffectiveSeparator(), descriptions);
    }

    public IReadOnlyList<ComponentStatsItem> GetAllStats()
    {
        return _componentStats.AsReadOnly();
    }

    public string GetCustomHardwareName(StatsComponentType type)
    {
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
        return item?.CustomHardwarenameValue;
    }

    public string GetDDRVersion()
    {
        string plain = _hwService.GetDdrVersion();
        return ToSuperscript(plain);
    }

    private static string ToSuperscript(string text)
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


    public string GetHardwareName(StatsComponentType type)
    {
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
        return item?.HardwareFriendlyName;
    }

    public bool GetHardwareTitleState(StatsComponentType type)
    {
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
        return item?.ShowPrefixHardwareTitle ?? false;
    }

    public bool GetRemoveNumberTrailing(StatsComponentType type)
    {
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
        return item?.RemoveNumberTrailing ?? false;
    }

    public bool GetShowCPUTemperature()
    {
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == StatsComponentType.CPU);
        return item?.ShowTemperature ?? false;
    }

    public bool GetShowCPUWattage()
    {
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == StatsComponentType.CPU);
        return item?.ShowWattage ?? false;
    }

    public bool GetShowGPUHotspotTemperature()
    {
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == StatsComponentType.GPU);
        return item?.ShowHotSpotTemperature ?? false;
    }

    public bool GetShowGPUTemperature()
    {
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == StatsComponentType.GPU);
        return item?.ShowTemperature ?? false;
    }

    public bool GetShowGPUWattage()
    {
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == StatsComponentType.GPU);
        return item?.ShowWattage ?? false;
    }

    public bool GetShowMaxValue(StatsComponentType type)
    {
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
        return item?.ShowMaxValue ?? false;
    }

    public bool GetShowRamDDRVersion()
    {
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == StatsComponentType.RAM);
        return item?.ShowDDRVersion ?? false;
    }

    public bool GetShowReplaceWithHardwareName(StatsComponentType type)
    {
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
        return item?.ReplaceWithHardwareName ?? false;
    }

    public bool GetShowSmallName(StatsComponentType type)
    {
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
        return item?.ShowSmallName ?? false;
    }

    public string GetStatMaxValue(StatsComponentType type)
    {
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
        return item?.ComponentValueMax;
    }

    public string GetStatValue(StatsComponentType type)
    {
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
        return item?.ComponentValue;
    }


    /// <summary>
    /// Returns a human-readable string listing components that have no data available.
    /// </summary>
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
            return ""; // or return some default message if you prefer
        }

        string result = "😞 " + string.Join(", ", notAvailableComponents) + " stats may not be available on your system...";
        return result;
    }

    public bool IsStatAvailable(StatsComponentType type)
    {
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
        return item?.Available ?? false;
    }

    public bool IsStatEnabled(StatsComponentType type)
    {
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
        return item?.IsEnabled ?? false;
    }

    public bool IsStatMaxValueShown(StatsComponentType type)
    {
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
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
            if (item.Available && item.IsEnabled && item.ComponentType == StatsComponentType.CPU && (item.cantShowWattage && item.ShowWattage || item.cantShowTemperature && item.ShowTemperature) == true)
            {
                return true;
            }
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
            bool isSteamVRRunning = Process.GetProcessesByName("vrmonitor").Length > 0;
            bool isOculusRunning = false;
            if (AS.CountOculusSystemAsVR)
            {
                isOculusRunning = Process.GetProcessesByName("OVRServer_x64").Length > 0;
            }

            bool isVRRunning = isSteamVRRunning || isOculusRunning;

            if (isVRRunning != _appState.IsVRRunning)
            {
                _appState.IsVRRunning = isVRRunning;
            }

            return isVRRunning;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return false;
        }
    }

    /// <summary>
    /// Loads persisted component stats configuration from disk, or seeds defaults if none exists.
    /// </summary>
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
                // Strip legacy FPS entries and normalize to exactly one item per supported type
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
                        _componentStats.Add(existing);
                    }
                    else
                    {
                        // Add a sensible default for this newly supported type
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

                if (needsResave || loadedStats.Any(s => s.ComponentType == StatsComponentType.FPS))
                    SaveComponentStats();

                started = true;
            }
            else
            {
                Logging.WriteException(new Exception("Failed to deserialize component stats."), MSGBox: true);
                InitializeDefaultStats();
            }
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: true);
        }
    }

    public void SaveComponentStats()
    {
        try
        {
            if (_componentStats == null || _componentStats.Count == 0) return;
            var jsonData = JsonConvert.SerializeObject(_componentStats);
            File.WriteAllText(FileName, jsonData);
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex);
        }
    }

    public void SetCustomHardwareName(StatsComponentType type, string name)
    {
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
        if (item != null)
        {
            item.CustomHardwarenameValue = name;
        }
    }

    public void SetHardwareTitle(StatsComponentType type, bool state)
    {
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
        if (item != null)
        {
            item.ShowPrefixHardwareTitle = state;
        }
    }

    public void SetRemoveNumberTrailing(StatsComponentType type, bool state)
    {
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
        if (item != null)
        {
            item.RemoveNumberTrailing = state;
        }
    }

    public void SetReplaceWithHardwareName(StatsComponentType type, bool state)
    {
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
        if (item != null)
        {
            item.ReplaceWithHardwareName = state;
        }
    }

    public void SetShowCPUTemperature(bool state)
    {
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == StatsComponentType.CPU);
        if (item != null)
        {
            item.ShowTemperature = state;
        }
    }

    public void SetShowCPUWattage(bool state)
    {
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == StatsComponentType.CPU);
        if (item != null)
        {
            item.ShowWattage = state;
        }
    }

    public void SetShowGPUHotspotTemperature(bool state)
    {
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == StatsComponentType.GPU);
        if (item != null)
        {
            item.ShowHotSpotTemperature = state;
        }
    }

    public void SetShowGPUTemperature(bool state)
    {
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == StatsComponentType.GPU);
        if (item != null)
        {
            item.ShowTemperature = state;
        }
    }

    public void SetShowGPUWattage(bool state)
    {
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == StatsComponentType.GPU);
        if (item != null)
        {
            item.ShowWattage = state;
        }
    }

    public void SetShowMaxValue(StatsComponentType type, bool state)
    {
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
        if (item != null)
        {
            item.ShowMaxValue = state;
        }
    }

    public void SetShowRamDDRVersion(bool state)
    {
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == StatsComponentType.RAM);
        if (item != null)
        {
            item.ShowDDRVersion = state;
        }
    }

    public void SetShowSmallName(StatsComponentType type, bool state)
    {
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
        if (item != null)
        {
            item.ShowSmallName = state;
        }
    }

    public void SetStatAvailable(StatsComponentType type, bool available)
    {
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
        if (item != null)
        {
            item.Available = available;
        }
    }

    public void SetStatMaxValue(StatsComponentType type, string maxValue)
    {
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
        if (item != null)
        {
            item.ComponentValueMax = maxValue;
        }
    }

    public void SetStatMaxValueShown(StatsComponentType type, bool state)
    {
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
        if (item != null)
        {
            item.ShowMaxValue = state;
        }
    }

    public void StartModule()
    {
        if (_integrationSettings.IntgrComponentStats && _integrationSettings.IntgrComponentStats_VR &&
                _appState.IsVRRunning || _integrationSettings.IntgrComponentStats &&
                _integrationSettings.IntgrComponentStats_DESKTOP &&
                !_appState.IsVRRunning)
        {
            LoadComponentStats();
            FetchAndStoreDDRVersion();
        }

    }

    public void StartMonitoringComponents()
    {
        try
        {
            _hwService.Open();
            RefreshGpuList();
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
    }

    private void RefreshGpuList()
    {
        var gpus = _hwService.GetAvailableGpus();
        _dispatcher.Invoke(() =>
        {
            GPUList.Clear();
            foreach (var gpu in gpus)
                GPUList.Add(gpu);
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

    /// <summary>
    /// Checks if a stats update is due and, if so, polls hardware and regenerates the display string.
    /// </summary>
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
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
        if (item != null)
        {
            item.IsEnabled = !item.IsEnabled;
        }
    }






    /// <summary>
    /// Refreshes all component stat values from the hardware service and returns <see langword="true"/> if any changed.
    /// </summary>
    public bool UpdateStats()
    {
        void UpdateComponentStats(StatsComponentType type, Func<string> fetchStat, Func<string> fetchMaxStat = null)
        {
            var statItem = StatsVm.ComponentStatsList.FirstOrDefault(stat => stat.ComponentType == type);
            if (statItem == null || !statItem.IsEnabled) return;

            string value = fetchStat();
            string maxValue = fetchMaxStat?.Invoke();

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
        var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
        if (item != null)
        {
            item.ComponentValue = newValue;
            item.LastUpdated = DateTime.Now;
        }
    }
}
