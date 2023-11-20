using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;
using LibreHardwareMonitor.Hardware;
using vrcosc_magicchatbox.DataAndSecurity;

namespace vrcosc_magicchatbox.Classes
{
    public class SystemStats
    {
        public static Computer CurrentSystem;

        public static void StartMonitoringComponents()
        {
            try
            {
                CurrentSystem = new Computer() { IsCpuEnabled = true, IsGpuEnabled = true, IsMemoryEnabled = true, };

                CurrentSystem.Open();
            } catch(Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
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

        private static bool ShouldUpdateComponentStats()
        {
            var vm = ViewModel.Instance;
            return (vm.IntgrComponentStats && vm.IntgrComponentStats_VR && vm.IsVRRunning) ||
                   (vm.IntgrComponentStats && vm.IntgrComponentStats_DESKTOP && !vm.IsVRRunning);
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

        private static void PerformStopActions()
        {
            var vm = ViewModel.Instance;
            vm.ComponentStatsRunning = false;

            if (CurrentSystem != null)
            {
                StopMonitoringComponents();
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
            } catch(Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
            }
        }

        private static DateTimeOffset GetDateTimeWithZone(
            bool autoSetDaylight,
            bool timeShowTimeZone,
            DateTimeOffset localDateTime,
            TimeZoneInfo timeZoneInfo,
            out TimeSpan timeZoneOffset)
        {
            DateTimeOffset dateTimeWithZone;

            if(autoSetDaylight)
            {
                if(timeShowTimeZone)
                {
                    timeZoneOffset = timeZoneInfo.GetUtcOffset(localDateTime);
                    dateTimeWithZone = TimeZoneInfo.ConvertTime(localDateTime, timeZoneInfo);
                } else
                {
                    timeZoneOffset = TimeZoneInfo.Local.GetUtcOffset(localDateTime);
                    dateTimeWithZone = localDateTime;
                }
            } else
            {
                timeZoneOffset = timeZoneInfo.BaseUtcOffset;
                if(ViewModel.Instance.UseDaylightSavingTime)
                {
                    TimeSpan adjustment = timeZoneInfo.GetAdjustmentRules().FirstOrDefault()?.DaylightDelta ??
                        TimeSpan.Zero;
                    timeZoneOffset = timeZoneOffset.Add(adjustment);
                }
                dateTimeWithZone = localDateTime.ToOffset(timeZoneOffset);
            }
            return dateTimeWithZone;
        }

        private static string GetFormattedTime(
            DateTimeOffset dateTimeWithZone,
            bool time24H,
            bool timeShowTimeZone,
            string timeZoneDisplay)
        {
            CultureInfo userCulture = CultureInfo.CurrentCulture;
            string timeFormat = time24H ? "HH:mm" : "hh:mm tt";

            if(timeShowTimeZone)
            {
                return dateTimeWithZone.ToString($"{timeFormat}{timeZoneDisplay}", userCulture);
            } else
            {
                return dateTimeWithZone.ToString(timeFormat, CultureInfo.InvariantCulture).ToUpper();
            }
        }

        public static string GetTime()
        {
            try
            {
                DateTimeOffset localDateTime = DateTimeOffset.Now;
                TimeZoneInfo timeZoneInfo;
                string timezoneLabel = null;

                switch(ViewModel.Instance.SelectedTimeZone)
                {
                    case Timezone.UTC:
                        timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("UTC");
                        timezoneLabel = "UTC";
                        break;
                    case Timezone.EST:
                        timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                        timezoneLabel = "EST";
                        break;
                    case Timezone.CST:
                        timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
                        timezoneLabel = "CST";
                        break;
                    case Timezone.PST:
                        timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
                        timezoneLabel = "PST";
                        break;
                    case Timezone.CET:
                        timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
                        timezoneLabel = "CET";
                        break;
                    case Timezone.AEST:
                        timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("E. Australia Standard Time");
                        timezoneLabel = "AEST";
                        break;
                    default:
                        timeZoneInfo = TimeZoneInfo.Local;
                        timezoneLabel = "??";
                        break;
                }

                TimeSpan timeZoneOffset;
                var dateTimeWithZone = GetDateTimeWithZone(
                    ViewModel.Instance.AutoSetDaylight,
                    ViewModel.Instance.TimeShowTimeZone,
                    localDateTime,
                    timeZoneInfo,
                    out timeZoneOffset);

                string timeZoneDisplay = $" ({timezoneLabel}{(timeZoneOffset < TimeSpan.Zero ? "-" : "+")}{timeZoneOffset.Hours.ToString("00")})";
                return GetFormattedTime(
                    dateTimeWithZone,
                    ViewModel.Instance.Time24H,
                    ViewModel.Instance.TimeShowTimeZone,
                    timeZoneDisplay);
            } catch(Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
                return "00:00 XX";
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
                    else if (maxValue != null)
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
                            ViewModel.Instance.isGPUAvailable = value;
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
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
                return false;
            }
            return true;
        }



        public static bool IsVRRunning()
        {
            try
            {
                bool isSteamVRRunning = Process.GetProcessesByName("vrmonitor").Length > 0;

                bool isOculusRunning = Process.GetProcessesByName("OVRServer_x64").Length > 0;

                return isSteamVRRunning || isOculusRunning;
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
                return false;
            }
        }

        private static Hardware GetDedicatedGPU()
        {
            try
            {
                foreach (var type in new[] { HardwareType.GpuNvidia, HardwareType.GpuAmd })
                {
                    var hardware = CurrentSystem.Hardware
                        .FirstOrDefault(h => h.HardwareType == type && !h.Name.ToLower().Contains("integrated"))
                        as Hardware;

                    if (hardware != null) return hardware;
                }
                return CurrentSystem.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.GpuIntel)
                    as Hardware;
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
                return null;
            }

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

                if(current.HardwareFriendlyName != hardware.Name)
                {
                    current.HardwareFriendlyName = hardware.Name;
                    if(!current.ReplaceWithHardwareName)
                        current.HardwareFriendlyNameSmall = DataController.TransformToSuperscript(hardware.Name);
                }
                if(current.ReplaceWithHardwareName || string.IsNullOrEmpty(current.HardwareFriendlyNameSmall))
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
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
                return "N/A";
            }
        }

        private static string FetchCPUStat() =>
            FetchStat(HardwareType.Cpu, SensorType.Load, "CPU Total", statsComponentType:StatsComponentType.CPU);

        private static string FetchGPUStat() =>
            FetchStat(HardwareType.GpuNvidia, SensorType.Load, ViewModel.Instance.ComponentStatsGPU3DHook? "D3D 3D" : "GPU Core", hardwarePredicate: h => h == GetDedicatedGPU(), statsComponentType: StatsComponentType.GPU);

        private static string FetchVRAMStat() =>
            FetchStat(HardwareType.GpuNvidia, SensorType.SmallData, "GPU Memory Used", val => val / 1024, h => h == GetDedicatedGPU(), statsComponentType: StatsComponentType.VRAM);

        private static string FetchVRAMMaxStat() =>
            FetchStat(HardwareType.GpuNvidia, SensorType.SmallData, "GPU Memory Total", val => val / 1024, h => h == GetDedicatedGPU(), statsComponentType:StatsComponentType.VRAM);

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

        private static string FetchFPSStat()
        {
            // Replace with actual FPS fetching logic.
            return "88";
        }
    }
}
