using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;
using static vrcosc_magicchatbox.ViewModels.ViewModel;
using LibreHardwareMonitor.Hardware;
using static vrcosc_magicchatbox.ViewModels.InternalEnums;

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
            if(Instance.IntgrComponentStats &&
                Instance.IntgrComponentStats_VR &&
                Instance.IsVRRunning ||
                Instance.IntgrComponentStats &&
                Instance.IntgrComponentStats_DESKTOP &&
                !Instance.IsVRRunning)
            {
                Instance.ComponentStatsRunning = true;
                if (CurrentSystem == null)
                {
                    StartMonitoringComponents();
                }
                Instance.SyncComponentStatsList();

                bool UpdateStatsCompleted = UpdateStats();

                if(UpdateStatsCompleted)
                {
                    Instance.ComponentStatCombined = Instance._statsManager.GenerateStatsDescription();
                }
            } else
            {
                Instance.ComponentStatsRunning = false;
                if (CurrentSystem != null)
                {
                    StopMonitoringComponents();

                }
            }
        }

        public static void StopMonitoringComponents()
        {
            try
            {
                Instance.SyncComponentStatsList();
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
            try
            {
                // Fetches the ComponentStatsItem for the given type.
                ComponentStatsItem GetStatsItem(StatsComponentType type)
                { return ViewModel.Instance.ComponentStatsList.FirstOrDefault(stat => stat.ComponentType == type); }

                // CPU stats
                var cpuStatItem = GetStatsItem(StatsComponentType.CPU);
                if(cpuStatItem != null && cpuStatItem.IsEnabled)
                {
                    string cpuValue = FetchCPUStat();
                    if(!cpuValue.Contains("N/A"))
                    ViewModel.Instance.UpdateComponentStat(StatsComponentType.CPU, cpuValue);
                }

                // GPU stats
                var gpuStatItem = GetStatsItem(StatsComponentType.GPU);
                if(gpuStatItem != null && gpuStatItem.IsEnabled)
                {
                    string gpuValue = FetchGPUStat();
                    if(!gpuValue.Contains("N/A"))
                    ViewModel.Instance.UpdateComponentStat(StatsComponentType.GPU, gpuValue);
                }

                // RAM stats
                var ramStatItem = GetStatsItem(StatsComponentType.RAM);
                if(ramStatItem != null && ramStatItem.IsEnabled)
                {
                    var (usedMemory, totalMemory) = FetchRAMStats();
                    if(!usedMemory.Contains("N/A"))
                    ViewModel.Instance.UpdateComponentStat(StatsComponentType.RAM, usedMemory);
                     if(!totalMemory.Contains("N/A"))
                    ViewModel.Instance.SetComponentStatMaxValue(StatsComponentType.RAM, totalMemory);
                }

                // VRAM stats
                var vramStatItem = GetStatsItem(StatsComponentType.VRAM);
                if(vramStatItem != null && vramStatItem.IsEnabled)
                {
                    string vramValue = FetchVRAMStat();
                    string vramValueMax = FetchVRAMMaxStat();
                    if(!vramValue.Contains("N/A"))
                    ViewModel.Instance.UpdateComponentStat(StatsComponentType.VRAM, vramValue);
                    if(!vramValueMax.Contains("N/A"))
                    ViewModel.Instance.SetComponentStatMaxValue(StatsComponentType.VRAM, vramValueMax);
                }

                // FPS stats
                var fpsStatItem = GetStatsItem(StatsComponentType.FPS);
                if(fpsStatItem != null && fpsStatItem.IsEnabled)
                {
                    string fpsValue = FetchFPSStat();
                    ViewModel.Instance.UpdateComponentStat(StatsComponentType.FPS, fpsValue);
                }
            } catch(Exception)
            {
                return false;
            }
            return true;
        }

        public static bool IsVRRunning()
        {
            try
            {
                Process[] pname = Process.GetProcessesByName("vrmonitor");
                if(pname.Length == 0)
                    return false;
                else
                    return true;
            } catch(Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
                return false;
            }
        }

        private static Hardware GetDedicatedGPU()
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

        private static string FetchStat(
            HardwareType hardwareType,
            SensorType sensorType,
            string sensorName,
            Func<double, double> transform = null,
            Func<IHardware, bool> hardwarePredicate = null)
        {
            try
            {
                IHardware hardware = hardwarePredicate == null
                    ? CurrentSystem.Hardware.FirstOrDefault(h => h.HardwareType == hardwareType)
                    : CurrentSystem.Hardware.FirstOrDefault(hardwarePredicate);

                if (hardware == null) return "N/A";

                hardware.Update();
                var sensor = hardware.Sensors
                    .FirstOrDefault(s => s.SensorType == sensorType && s.Name == sensorName);

                if (sensor?.Value == null) return "N/A";

                var value = transform == null ? sensor.Value.Value : transform(sensor.Value.Value);
                return $"{value:F1}";
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
                return "N/A";
            }
        }




        private static string FetchCPUStat() =>
            FetchStat(HardwareType.Cpu, SensorType.Load, "CPU Total");

        private static string FetchGPUStat() =>
            FetchStat(HardwareType.GpuNvidia, SensorType.Load, "GPU Core", hardwarePredicate: h => h == GetDedicatedGPU());

        private static string FetchVRAMStat() =>
            FetchStat(HardwareType.GpuNvidia, SensorType.SmallData, "GPU Memory Used", val => val / 1024, h => h == GetDedicatedGPU());

        private static string FetchVRAMMaxStat() =>
            FetchStat(HardwareType.GpuNvidia, SensorType.SmallData, "GPU Memory Total", val => val / 1024, h => h == GetDedicatedGPU());

        private static (string UsedMemory, string MaxMemory) FetchRAMStats()
        {
            string usedMemory = FetchStat(HardwareType.Memory, SensorType.Data, "Memory Used");
            string availableMemory = FetchStat(HardwareType.Memory, SensorType.Data, "Memory Available");

            if (double.TryParse(usedMemory, out double usedMemoryVal) && double.TryParse(availableMemory, out double availableMemoryVal))
            {
                double totalMemory = usedMemoryVal + availableMemoryVal;
                return ($"{usedMemoryVal:F1}", $"{totalMemory:F1}");
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
