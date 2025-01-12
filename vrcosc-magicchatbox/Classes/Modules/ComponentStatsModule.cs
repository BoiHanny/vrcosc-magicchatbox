﻿using LibreHardwareMonitor.Hardware;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using System.Windows;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.Models;

namespace vrcosc_magicchatbox.Classes.Modules
{
    public class ComponentStatsModule
    {
        private static string FileName = null;

        private static readonly StatsComponentType[] StatDisplayOrder =
        {
            StatsComponentType.CPU,
            StatsComponentType.GPU,
            StatsComponentType.VRAM,
            StatsComponentType.RAM,
        };


        public static Computer CurrentSystem;
        private readonly List<ComponentStatsItem> _componentStats = new List<ComponentStatsItem>();
        private string _ramDDRVersion = "Unknown";
        public bool started = false;

        private void FetchAndStoreDDRVersion()
        {
            _ramDDRVersion = GetDDRVersion();
            var ramItem = _componentStats.FirstOrDefault(stat => stat.ComponentType == StatsComponentType.RAM);
            if (ramItem != null)
            {
                ramItem.DDRVersion = _ramDDRVersion;
            }
        }

        private static string FetchCPUStat() =>
            FetchStat(HardwareType.Cpu, SensorType.Load, "CPU Total", statsComponentType: StatsComponentType.CPU);

        private static string FetchFPSStat()
        {
            // Replace with actual FPS fetching logic.
            return "88";
        }

        private static string FetchGPUStat() =>
            FetchStat(HardwareType.GpuNvidia, SensorType.Load, ViewModel.Instance.ComponentStatsGPU3DHook ? "D3D 3D" : "GPU Core", hardwarePredicate: h => h == GetDedicatedGPU(), statsComponentType: StatsComponentType.GPU);



        private static string FetchHotspotTemperatureStat(IHardware hardware, ComponentStatsItem item)
        {
            foreach (var sensor in hardware.Sensors)
            {
                if (sensor.SensorType == SensorType.Temperature &&
                    (sensor.Name.Contains("Hot spot", StringComparison.InvariantCultureIgnoreCase) ||
                     sensor.Name.Contains("Hotspot", StringComparison.InvariantCultureIgnoreCase)))
                {
                    double temperatureCelsius = sensor.Value ?? 0.0;

                    if (temperatureCelsius == 0)
                    {
                        item.cantShowHotSpotTemperature = true;
                        return "N/A";
                    }

                    string unit = ViewModel.Instance.TemperatureUnit;
                    double temperature = unit == "F" ? temperatureCelsius * 9 / 5 + 32 : temperatureCelsius;

                    if (item.RemoveNumberTrailing)
                    {
                        temperature = Math.Round(temperature);
                    }

                    string unitSymbol = unit == "F" ? "°F" : "°C";
                    string tempText = ViewModel.Instance.UseEmojisForTempAndPower ? "🔥" : "GPU HotSpot";
                    if (item.ShowSmallName && !ViewModel.Instance.UseEmojisForTempAndPower)
                    {
                        tempText = DataController.TransformToSuperscript(tempText);
                    }

                    string formattedTemperature = item.RemoveNumberTrailing ? $"{(int)temperature}" : $"{temperature:F1}";
                    item.cantShowHotSpotTemperature = false;
                    return $"{tempText} {formattedTemperature}{DataController.TransformToSuperscript(unitSymbol)}";
                }
            }
            item.cantShowHotSpotTemperature = true;
            return "N/A";
        }

        private string FetchPowerStat(IHardware hardware, ComponentStatsItem item)
        {
            foreach (var sensor in hardware.Sensors)
            {
                if (sensor.SensorType == SensorType.Power &&
                    (sensor.Name.Contains("Package", StringComparison.InvariantCultureIgnoreCase) ||
                     sensor.Name.Contains("Core", StringComparison.InvariantCultureIgnoreCase)))
                {
                    double power = sensor.Value ?? 0.0;

                    if (power == 0)
                    {
                        item.cantShowWattage = true;
                        return "N/A";
                    }

                    if (item.RemoveNumberTrailing)
                    {
                        power = Math.Round(power);
                    }

                    string powerUnit = "W";
                    string powerText = ViewModel.Instance.UseEmojisForTempAndPower ? "⚡" : "power";
                    if (item.ShowSmallName && !ViewModel.Instance.UseEmojisForTempAndPower)
                    {
                        powerText = DataController.TransformToSuperscript(powerText);
                    }

                    string formattedPower = item.RemoveNumberTrailing ? $"{(int)power}" : $"{power:F1}";
                    item.cantShowWattage = false;
                    return $"{powerText} {formattedPower}{DataController.TransformToSuperscript(powerUnit)}";
                }
            }
            item.cantShowWattage = true;
            return "N/A";
        }

        private static (string UsedMemory, string MaxMemory) FetchRAMStats()
        {
            string usedMemory = FetchStat(HardwareType.Memory, SensorType.Data, "Memory Used", statsComponentType: StatsComponentType.RAM);
            string availableMemory = FetchStat(HardwareType.Memory, SensorType.Data, "Memory Available", statsComponentType: StatsComponentType.RAM);

            var current = ViewModel.Instance.ComponentStatsList.FirstOrDefault(stat => stat.ComponentType == StatsComponentType.RAM);

            if (double.TryParse(usedMemory, out double usedMemoryVal) && double.TryParse(availableMemory, out double availableMemoryVal))
            {
                double totalMemory = usedMemoryVal + availableMemoryVal;

                if (current?.RemoveNumberTrailing == true)
                {
                    return ($"{(int)usedMemoryVal}", $"{(int)totalMemory}");
                }
                else
                {
                    return ($"{usedMemoryVal:F1}", $"{totalMemory:F1}");
                }
            }

            return ("N/A", "N/A");
        }

        private static string FetchStat(
            HardwareType hardwareType,
            SensorType sensorType,
            string sensorName,
            Func<double, double> transform = null,
            Func<IHardware, bool> hardwarePredicate = null,
            StatsComponentType statsComponentType = StatsComponentType.Unknown)
        {
            if (hardwareType == default || sensorType == default || string.IsNullOrWhiteSpace(sensorName))
            {
                return "N/A";
            }

            if (statsComponentType == StatsComponentType.Unknown)
            {
                return "N/A";
            }

            ComponentStatsItem current = null;
            if (statsComponentType != StatsComponentType.Unknown)
            {
                current = ViewModel.Instance.ComponentStatsList.FirstOrDefault(stat => stat.ComponentType == statsComponentType);
            }

            try
            {
                IHardware hardware = hardwarePredicate == null
                    ? CurrentSystem.Hardware.FirstOrDefault(h => h.HardwareType == hardwareType)
                    : CurrentSystem.Hardware.FirstOrDefault(hardwarePredicate);

                if (hardware == null) return "N/A";

                hardware.Update();
                var sensor = hardware.Sensors
                    .FirstOrDefault(s => s.SensorType == sensorType && s.Name == sensorName);

                if (current.HardwareFriendlyName != hardware.Name)
                {
                    current.HardwareFriendlyName = hardware.Name;
                    if (!current.ReplaceWithHardwareName)
                        current.HardwareFriendlyNameSmall = DataController.TransformToSuperscript(hardware.Name);
                }
                if (current.ReplaceWithHardwareName || string.IsNullOrEmpty(current.HardwareFriendlyNameSmall))
                {
                    current.CustomHardwarenameValueSmall = DataController.TransformToSuperscript(current.CustomHardwarenameValue);
                }

                if (sensor?.Value == null) return "N/A";

                var value = transform == null ? sensor.Value.Value : transform(sensor.Value.Value);

                if (current?.RemoveNumberTrailing == true)
                {
                    return ((int)value).ToString();
                }
                else
                {
                    return $"{value:F1}";
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
                return "N/A";
            }
        }

        private string FetchTemperatureStat(IHardware hardware, ComponentStatsItem item)
        {
            foreach (var sensor in hardware.Sensors)
            {
                if (sensor.SensorType == SensorType.Temperature &&
                    (sensor.Name.Contains("Package", StringComparison.InvariantCultureIgnoreCase) ||
                     sensor.Name.Contains("Core", StringComparison.InvariantCultureIgnoreCase)))
                {
                    double temperatureCelsius = sensor.Value ?? 0.0;

                    if (temperatureCelsius == 0)
                    {
                        item.cantShowTemperature = true;
                        return "N/A";
                    }

                    string unit = ViewModel.Instance.TemperatureUnit;
                    double temperature = unit == "F" ? temperatureCelsius * 9 / 5 + 32 : temperatureCelsius;

                    if (item.RemoveNumberTrailing)
                    {
                        temperature = Math.Round(temperature);
                    }

                    string unitSymbol = unit == "F" ? "°F" : "°C";
                    string tempText = ViewModel.Instance.UseEmojisForTempAndPower ? "♨️" : "temp";
                    if (item.ShowSmallName && !ViewModel.Instance.UseEmojisForTempAndPower)
                    {
                        tempText = DataController.TransformToSuperscript(tempText);
                    }

                    string formattedTemperature = item.RemoveNumberTrailing ? $"{(int)temperature}" : $"{temperature:F1}";
                    item.cantShowTemperature = false;
                    return $"{tempText} {formattedTemperature}{DataController.TransformToSuperscript(unitSymbol)}";
                }
            }
            item.cantShowTemperature = true;
            return "N/A";
        }

        private static string FetchVRAMMaxStat() =>
            FetchStat(HardwareType.GpuNvidia, SensorType.SmallData, ViewModel.Instance.ComponentStatsGPU3DVRAMHook ? "D3D Dedicated Memory Total" : "GPU Memory Total", val => val / 1024, h => h == GetDedicatedGPU(), statsComponentType: StatsComponentType.VRAM);

        private static string FetchVRAMStat() =>
            FetchStat(HardwareType.GpuNvidia, SensorType.SmallData, ViewModel.Instance.ComponentStatsGPU3DVRAMHook ? "D3D Dedicated Memory Used" : "GPU Memory Used", val => val / 1024, h => h == GetDedicatedGPU(), statsComponentType: StatsComponentType.VRAM);

        private static DateTimeOffset GetDateTimeWithZone(
            bool autoSetDaylight,
            bool timeShowTimeZone,
            DateTimeOffset localDateTime,
            TimeZoneInfo timeZoneInfo,
            out TimeSpan timeZoneOffset)
        {
            DateTimeOffset dateTimeWithZone;

            if (autoSetDaylight)
            {
                if (timeShowTimeZone)
                {
                    timeZoneOffset = timeZoneInfo.GetUtcOffset(localDateTime);
                    dateTimeWithZone = TimeZoneInfo.ConvertTime(localDateTime, timeZoneInfo);
                }
                else
                {
                    timeZoneOffset = TimeZoneInfo.Local.GetUtcOffset(localDateTime);
                    dateTimeWithZone = localDateTime;
                }
            }
            else
            {
                timeZoneOffset = timeZoneInfo.BaseUtcOffset;
                if (ViewModel.Instance.UseDaylightSavingTime)
                {
                    TimeSpan adjustment = timeZoneInfo.GetAdjustmentRules().FirstOrDefault()?.DaylightDelta ??
                        TimeSpan.Zero;
                    timeZoneOffset = timeZoneOffset.Add(adjustment);
                }
                dateTimeWithZone = localDateTime.ToOffset(timeZoneOffset);
            }
            return dateTimeWithZone;
        }

        private string GetDDRVersionFromSMBIOSMemoryType(ushort smbiosMemoryType)
        {
            // Map the SMBIOSMemoryType to DDR version numbers 0-5
            switch (smbiosMemoryType)
            {
                case 0:
                case 1:
                case 2:
                case 3:
                case 4:
                case 5:
                    return "ᴰᴰᴿ";
                case 20:
                    return "ᴰᴰᴿ¹";
                case 21:
                    return "ᴰᴰᴿ²";
                case 24:
                    return "ᴰᴰᴿ³";
                case 26:
                    return "ᴰᴰᴿ⁴";
                case 34:
                    return "ᴰᴰᴿ⁵";
                // Include other cases as needed
                default:
                    return null;
            }
        }

        private static Hardware GetDedicatedGPU()
        {
            try
            {
                // Ensure the GPU list in the ViewModel is populated.
                if (ViewModel.Instance.GPUList == null || !ViewModel.Instance.GPUList.Any())
                {
                    ViewModel.Instance.GPUList = CurrentSystem.Hardware
                        .Where(h => h.HardwareType == HardwareType.GpuNvidia || h.HardwareType == HardwareType.GpuAmd || h.HardwareType == HardwareType.GpuIntel)
                        .Select(h => h.Name)
                        .ToList();
                }

                Hardware selectedHardware = null;

                // Use AutoSelectGPU mechanism if SelectedGPU is not set or AutoSelectGPU is true.
                if (string.IsNullOrEmpty(ViewModel.Instance.SelectedGPU) || ViewModel.Instance.AutoSelectGPU)
                {
                    // Perform auto-selection of GPU based on predefined criteria.
                    foreach (var type in new[] { HardwareType.GpuNvidia, HardwareType.GpuAmd })
                    {
                        selectedHardware = CurrentSystem.Hardware
                            .FirstOrDefault(h => ViewModel.Instance.GPUList.Contains(h.Name) && h.HardwareType == type && !h.Name.ToLower().Contains("integrated")) as Hardware;
                        if (selectedHardware != null)
                        {
                            ViewModel.Instance.SelectedGPU = selectedHardware.Name; // Update SelectedGPU with the auto-selected GPU name.
                            break; // Break on finding the first dedicated GPU
                        }
                    }

                    // Fallback to integrated GPU if no dedicated GPU is found.
                    if (selectedHardware == null)
                    {
                        selectedHardware = CurrentSystem.Hardware
                            .FirstOrDefault(h => ViewModel.Instance.GPUList.Contains(h.Name) && h.HardwareType == HardwareType.GpuIntel) as Hardware;
                        if (selectedHardware != null)
                        {
                            ViewModel.Instance.SelectedGPU = selectedHardware.Name; // Update SelectedGPU with the auto-selected GPU name.
                        }
                    }
                }
                else
                {
                    // Attempt to use the manually selected GPU if AutoSelectGPU is false and a GPU is selected.
                    selectedHardware = CurrentSystem.Hardware
                        .FirstOrDefault(h => h.Name.Equals(ViewModel.Instance.SelectedGPU, StringComparison.OrdinalIgnoreCase)) as Hardware;
                }

                return selectedHardware; // Return the selected or auto-selected GPU.
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
                return null; // Return null in case of any exceptions.
            }
        }

        private void InitializeDefaultStats()
        {
            try
            {
                foreach (StatsComponentType type in Enum.GetValues(typeof(StatsComponentType)))
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
                        case StatsComponentType.FPS:
                            unit = "ᶠᵖˢ";
                            break;
                    }

                    var component = new ComponentStatsItem(
                        type.ToString(),
                        type.GetSmallName(),
                        type,
                        "",
                        "",
                        !(type == StatsComponentType.FPS ||
                          type == StatsComponentType.GPU ||
                          type == StatsComponentType.CPU),
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

                    if (type == StatsComponentType.FPS)
                    {
                        component.ShowUnit = false;
                    }
                    if (type == StatsComponentType.VRAM || type == StatsComponentType.RAM)
                    {
                        component.RemoveNumberTrailing = false;
                        component.IsEnabled = false;
                    }
                    _componentStats.Add(component);
                }
                Application.Current.Dispatcher.Invoke(() =>
                {
                    started = true;
                });
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex);
            }
        }

        private static void PerformStopActions()
        {
            var vm = ViewModel.Instance;
            vm.ComponentStatsRunning = false;

            if (CurrentSystem != null)
            {
                StopMonitoringComponents();
            }
        }

        private static void PerformUpdateActions()
        {
            var vm = ViewModel.Instance;
            vm.ComponentStatsRunning = true;

            if (CurrentSystem == null)
            {
                StartMonitoringComponents();
            }

            vm.SyncComponentStatsList();

            if (UpdateStats())
            {
                vm.ComponentStatCombined = vm._statsManager.GenerateStatsDescription();
            }
        }

        private async void RestartApplication()
        {
            // Obtain the full path of the current application
            string dllPath = System.Reflection.Assembly.GetExecutingAssembly().Location;

            // Replace .dll with .exe to get the path to the executable
            string exePath = dllPath.Replace(".dll", ".exe");

            // Create a new process to start the application again
            ProcessStartInfo psi = new ProcessStartInfo(exePath)
            {
                UseShellExecute = false
            };

            // Start the new process
            Process.Start(psi);

            // Wait for a short delay
            await Task.Delay(500);

            // Shut down the current application
            Application.Current.Shutdown();
        }

        private static bool ShouldUpdateComponentStats()
        {
            var vm = ViewModel.Instance;
            return vm.IntgrComponentStats && vm.IntgrComponentStats_VR && vm.IsVRRunning ||
                   vm.IntgrComponentStats && vm.IntgrComponentStats_DESKTOP && !vm.IsVRRunning;
        }

        public void ActivateStateState(StatsComponentType type, bool state)
        {
            var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
            if (item != null)
            {
                item.IsEnabled = state;
            }
        }

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

                    var cpuHardware = CurrentSystem.Hardware.FirstOrDefault(hw => hw.HardwareType == HardwareType.Cpu);
                    var gpuHardware = GetDedicatedGPU();

                    if (stat.ComponentType == StatsComponentType.CPU && cpuHardware != null)
                    {
                        string cpuTemp = stat.ShowTemperature ? FetchTemperatureStat(cpuHardware, stat) : "";
                        string cpuPower = stat.ShowWattage ? FetchPowerStat(cpuHardware, stat) : "";

                        if (!stat.cantShowTemperature && !string.IsNullOrWhiteSpace(cpuTemp))
                        {
                            additionalInfoParts.Add(cpuTemp);
                        }
                        if (!stat.cantShowWattage && !string.IsNullOrWhiteSpace(cpuPower))
                        {
                            additionalInfoParts.Add(cpuPower);
                        }
                    }
                    else if (stat.ComponentType == StatsComponentType.GPU && gpuHardware != null)
                    {
                        string gpuTemp = stat.ShowTemperature ? FetchTemperatureStat(gpuHardware, stat) : "";
                        string gpuHotSpotTemp = stat.ShowHotSpotTemperature ? FetchHotspotTemperatureStat(gpuHardware, stat) : "";
                        string gpuPower = stat.ShowWattage ? FetchPowerStat(gpuHardware, stat) : "";

                        if (!stat.cantShowTemperature && !string.IsNullOrWhiteSpace(gpuTemp))
                        {
                            additionalInfoParts.Add(gpuTemp);
                        }
                        if (!stat.cantShowHotSpotTemperature && !string.IsNullOrWhiteSpace(gpuHotSpotTemp))
                        {
                            additionalInfoParts.Add(gpuHotSpotTemp);
                        }
                        if (!stat.cantShowWattage && !string.IsNullOrWhiteSpace(gpuPower))
                        {
                            additionalInfoParts.Add(gpuPower);
                        }
                    }

                    if (stat.ComponentType == StatsComponentType.RAM && stat.ShowDDRVersion && !string.IsNullOrWhiteSpace(stat.DDRVersion))
                    {
                        componentDescription += $" ⁽{stat.DDRVersion}⁾";
                    }

                    // Combine the additionalInfo parts with a single space
                    string additionalInfo = string.Join(" ", additionalInfoParts).Trim();

                    // Combine the component description with additional info if any
                    string fullComponentInfo = string.IsNullOrWhiteSpace(additionalInfo)
                        ? componentDescription
                        : $"{componentDescription} {additionalInfo}";

                    // Add the full component info to the list of descriptions
                    if (!string.IsNullOrEmpty(fullComponentInfo))
                    {
                        descriptions.Add(fullComponentInfo);
                    }
                }
            }

            ViewModel.Instance.ComponentStatsLastUpdate = DateTime.Now;

            // Join the descriptions with the separator, ensuring no leading separator when there's only one item
            return string.Join(" ¦ ", descriptions);
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
            try
            {
                HashSet<string> ddrVersions = new HashSet<string>();

                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT SMBIOSMemoryType FROM Win32_PhysicalMemory");
                foreach (ManagementObject queryObj in searcher.Get())
                {
                    ushort smbiosMemoryType = Convert.ToUInt16(queryObj["SMBIOSMemoryType"]);
                    string ddrVersion = GetDDRVersionFromSMBIOSMemoryType(smbiosMemoryType);
                    if (!string.IsNullOrEmpty(ddrVersion))
                    {
                        ddrVersions.Add(ddrVersion);
                    }
                }

                if (ddrVersions.Count > 0)
                {
                    // Return the unique DDR versions, joined by a slash if multiple
                    return string.Join("'", ddrVersions);
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }

            // Return null if DDR version cannot be determined
            return null;
        }


        public static string GetFormattedTime(
            DateTimeOffset dateTimeWithZone,
            bool time24H,
            bool timeShowTimeZone,
            string timeZoneDisplay)
        {
            CultureInfo userCulture = ViewModel.Instance.UseSystemCulture ? CultureInfo.CurrentCulture : CultureInfo.InvariantCulture;
            string timeFormat = time24H ? "HH:mm" : "hh:mm tt";

            string formattedTime = dateTimeWithZone.ToString(timeFormat, userCulture);

            if (timeShowTimeZone)
            {
                return formattedTime + timeZoneDisplay;
            }
            else
            {
                return formattedTime;
            }
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


        public static string GetTime()
        {
            try
            {
                DateTimeOffset localDateTime = DateTimeOffset.Now;
                TimeZoneInfo timeZoneInfo;
                string standardAbbreviation = null;
                string daylightAbbreviation = null;

                switch (ViewModel.Instance.SelectedTimeZone)
                {
                    case Timezone.UTC:
                        timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("UTC");
                        standardAbbreviation = "UTC";
                        daylightAbbreviation = "UTC";
                        break;
                    case Timezone.GMT:
                        timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
                        standardAbbreviation = "GMT";
                        daylightAbbreviation = "BST";
                        break;
                    case Timezone.EST:
                        timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                        standardAbbreviation = "EST";
                        daylightAbbreviation = "EDT";
                        break;
                    case Timezone.CST:
                        timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
                        standardAbbreviation = "CST";
                        daylightAbbreviation = "CDT";
                        break;
                    case Timezone.MST:
                        timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("Mountain Standard Time");
                        standardAbbreviation = "MST";
                        daylightAbbreviation = "MDT";
                        break;
                    case Timezone.PST:
                        timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
                        standardAbbreviation = "PST";
                        daylightAbbreviation = "PDT";
                        break;
                    case Timezone.AKST:
                        timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("Alaskan Standard Time");
                        standardAbbreviation = "AKST";
                        daylightAbbreviation = "AKDT";
                        break;
                    case Timezone.HST:
                        timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("Hawaiian Standard Time");
                        standardAbbreviation = "HST";
                        daylightAbbreviation = "HST";
                        break;
                    case Timezone.CET:
                        timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
                        standardAbbreviation = "CET";
                        daylightAbbreviation = "CEST";
                        break;
                    case Timezone.EET:
                        timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("E. Europe Standard Time");
                        standardAbbreviation = "EET";
                        daylightAbbreviation = "EEST";
                        break;
                    case Timezone.IST:
                        timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
                        standardAbbreviation = "IST";
                        daylightAbbreviation = "IST";
                        break;
                    case Timezone.CSTChina:
                        timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
                        standardAbbreviation = "CST";
                        daylightAbbreviation = "CST";
                        break;
                    case Timezone.JST:
                        timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
                        standardAbbreviation = "JST";
                        daylightAbbreviation = "JST";
                        break;
                    case Timezone.KST:
                        timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");
                        standardAbbreviation = "KST";
                        daylightAbbreviation = "KST";
                        break;
                    case Timezone.MSK:
                        timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");
                        standardAbbreviation = "MSK";
                        daylightAbbreviation = "MSK";
                        break;
                    case Timezone.AEST:
                        timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time");
                        standardAbbreviation = "AEST";
                        daylightAbbreviation = "AEDT";
                        break;
                    case Timezone.NZST:
                        timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("New Zealand Standard Time");
                        standardAbbreviation = "NZST";
                        daylightAbbreviation = "NZDT";
                        break;
                    case Timezone.BRT:
                        timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
                        standardAbbreviation = "BRT";
                        daylightAbbreviation = "BRST";
                        break;
                    case Timezone.SAST:
                        timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");
                        standardAbbreviation = "SAST";
                        daylightAbbreviation = "SAST";
                        break;
                    default:
                        timeZoneInfo = TimeZoneInfo.Local;
                        standardAbbreviation = timeZoneInfo.StandardName;
                        daylightAbbreviation = timeZoneInfo.DaylightName;
                        break;
                }

                // Convert localDateTime to the selected time zone
                DateTimeOffset targetDateTime = TimeZoneInfo.ConvertTime(localDateTime, timeZoneInfo);

                // Determine if DST is in effect based on user settings
                bool isDaylightSavingTime;

                if (ViewModel.Instance.AutoSetDaylight)
                {
                    // Use system's DST determination
                    isDaylightSavingTime = timeZoneInfo.IsDaylightSavingTime(targetDateTime);
                }
                else
                {
                    // Use user's manual DST setting
                    isDaylightSavingTime = ViewModel.Instance.UseDaylightSavingTime;
                }

                // Select the appropriate time zone abbreviation
                string timezoneLabel = isDaylightSavingTime ? daylightAbbreviation : standardAbbreviation;

                TimeSpan timeZoneOffset;
                var dateTimeWithZone = GetDateTimeWithZone(
                    ViewModel.Instance.AutoSetDaylight,
                    ViewModel.Instance.TimeShowTimeZone,
                    localDateTime,
                    timeZoneInfo,
                    out timeZoneOffset);

                // Calculate offset sign and absolute values
                string offsetSign = timeZoneOffset < TimeSpan.Zero ? "-" : "+";
                int totalOffsetHours = (int)timeZoneOffset.TotalHours;
                int offsetMinutes = Math.Abs(timeZoneOffset.Minutes);

                // Format the offset string
                string offsetString = offsetMinutes == 0
                    ? $"{offsetSign}{Math.Abs(totalOffsetHours)}"
                    : $"{offsetSign}{Math.Abs(totalOffsetHours)}:{offsetMinutes.ToString("00")}";

                string timeZoneDisplay = $" ({timezoneLabel}{offsetString})";

                return GetFormattedTime(
                    dateTimeWithZone,
                    ViewModel.Instance.Time24H,
                    ViewModel.Instance.TimeShowTimeZone,
                    timeZoneDisplay);
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
                return "00:00 XX";
            }
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

        public static bool IsVRRunning()
        {
            try
            {
                bool isSteamVRRunning = Process.GetProcessesByName("vrmonitor").Length > 0;
                bool isOculusRunning = false;
                if (ViewModel.Instance.CountOculusSystemAsVR)
                {
                    isOculusRunning = Process.GetProcessesByName("OVRServer_x64").Length > 0;
                }

                bool isVRRunning = isSteamVRRunning || isOculusRunning;

                // Only set the property if the value has changed
                if (isVRRunning != ViewModel.Instance.IsVRRunning)
                {
                    ViewModel.Instance.IsVRRunning = isVRRunning;
                }

                return isVRRunning;
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
                return false;
            }
        }

        public void LoadComponentStats()
        {
            try
            {
                FileName = Path.Combine(ViewModel.Instance.DataPath, "ComponentStatsV1.json");
                if (!File.Exists(FileName))
                {
                    InitializeDefaultStats();
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MainWindow.FireExitSave();
                        RestartApplication();
                    });
                    return;
                }

                var jsonData = File.ReadAllText(FileName);

                // Check if the JSON string is empty, contains only null characters, or is whitespace
                if (string.IsNullOrWhiteSpace(jsonData) || jsonData.All(c => c == '\0'))
                {
                    Logging.WriteException(new Exception("The component stats file is empty or corrupted."), MSGBox: false);
                    InitializeDefaultStats();
                    return;
                }

                var loadedStats = JsonConvert.DeserializeObject<List<ComponentStatsItem>>(jsonData);
                if (loadedStats != null)
                {
                    _componentStats.Clear();
                    _componentStats.AddRange(loadedStats);
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
            if (ViewModel.Instance.IntgrComponentStats && ViewModel.Instance.IntgrComponentStats_VR &&
                    ViewModel.Instance.IsVRRunning || ViewModel.Instance.IntgrComponentStats &&
                    ViewModel.Instance.IntgrComponentStats_DESKTOP &&
                    !ViewModel.Instance.IsVRRunning)
            {
                LoadComponentStats();
                FetchAndStoreDDRVersion();
            }

        }

        public static void StartMonitoringComponents()
        {
            try
            {
                CurrentSystem = new Computer() { IsCpuEnabled = true, IsGpuEnabled = true, IsMemoryEnabled = true, };

                CurrentSystem.Open();
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        public static void StopMonitoringComponents()
        {
            try
            {
                ViewModel.Instance.SyncComponentStatsList();
                ViewModel.Instance._statsManager.SaveComponentStats();
                CurrentSystem.Close();
                CurrentSystem = null;
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        public static void TickAndUpdate()
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






        public static bool UpdateStats()
        {
            void UpdateComponentStats(StatsComponentType type, Func<string> fetchStat, Func<string> fetchMaxStat = null)
            {
                var statItem = ViewModel.Instance.ComponentStatsList.FirstOrDefault(stat => stat.ComponentType == type);
                if (statItem == null || !statItem.IsEnabled) return;

                string value = fetchStat();
                string maxValue = fetchMaxStat?.Invoke();

                if (!value.Contains("N/A"))
                {
                    ViewModel.Instance.UpdateComponentStat(type, value);
                    SetAvailability(type, true);
                }
                else
                {
                    SetAvailability(type, false);
                }

                if (maxValue != null && !maxValue.Contains("N/A"))
                {
                    ViewModel.Instance.SetComponentStatMaxValue(type, maxValue);
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
                        ViewModel.Instance.isCPUAvailable = value;
                        break;
                    case StatsComponentType.GPU:
                        ViewModel.Instance.IsGPUAvailable = value;
                        break;
                    case StatsComponentType.RAM:
                        ViewModel.Instance.isRAMAvailable = value;
                        break;
                    case StatsComponentType.VRAM:
                        ViewModel.Instance.isVRAMAvailable = value;
                        break;
                }
            }
            try
            {
                UpdateComponentStats(StatsComponentType.CPU, FetchCPUStat);
                UpdateComponentStats(StatsComponentType.GPU, FetchGPUStat);
                UpdateComponentStats(StatsComponentType.RAM, () => FetchRAMStats().UsedMemory, () => FetchRAMStats().MaxMemory);
                UpdateComponentStats(StatsComponentType.VRAM, FetchVRAMStat, FetchVRAMMaxStat);
                UpdateComponentStats(StatsComponentType.FPS, FetchFPSStat);
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
}
