using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;
using static vrcosc_magicchatbox.ViewModels.ViewModel;
using LibreHardwareMonitor.Hardware;

namespace vrcosc_magicchatbox.Classes
{
    public class SystemStats
    {
        public static Computer CurrentSystem;

        public static void StartMonitoringComponents()
        {
            CurrentSystem = new Computer()
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
            };

            CurrentSystem.Open();
        }

        public static void StopMonitoringComponents()
        {
            CurrentSystem.Close();
            CurrentSystem = null;
        }

        private static DateTimeOffset GetDateTimeWithZone(bool autoSetDaylight, bool timeShowTimeZone, DateTimeOffset localDateTime, TimeZoneInfo timeZoneInfo, out TimeSpan timeZoneOffset)
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
                    TimeSpan adjustment = timeZoneInfo.GetAdjustmentRules().FirstOrDefault()?.DaylightDelta ?? TimeSpan.Zero;
                    timeZoneOffset = timeZoneOffset.Add(adjustment);
                }
                dateTimeWithZone = localDateTime.ToOffset(timeZoneOffset);
            }
            return dateTimeWithZone;
        }

        private static string GetFormattedTime(DateTimeOffset dateTimeWithZone, bool time24H, bool timeShowTimeZone, string timeZoneDisplay)
        {
            CultureInfo userCulture = CultureInfo.CurrentCulture;
            string timeFormat = time24H ? "HH:mm" : "hh:mm tt";

            if (timeShowTimeZone)
            {
                return dateTimeWithZone.ToString($"{timeFormat}{timeZoneDisplay}", userCulture);
            }
            else
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

                switch (ViewModel.Instance.SelectedTimeZone)
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
                var dateTimeWithZone = GetDateTimeWithZone(ViewModel.Instance.AutoSetDaylight,
                                                            ViewModel.Instance.TimeShowTimeZone,
                                                            localDateTime,
                                                            timeZoneInfo,
                                                            out timeZoneOffset);

                string timeZoneDisplay = $" ({timezoneLabel}{(timeZoneOffset < TimeSpan.Zero ? "-" : "+")}{timeZoneOffset.Hours.ToString("00")})";
                return GetFormattedTime(dateTimeWithZone, ViewModel.Instance.Time24H, ViewModel.Instance.TimeShowTimeZone, timeZoneDisplay);
            }
            catch (Exception ex)
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
                {
                    return ViewModel.Instance.ComponentStatsList.FirstOrDefault(stat => stat.ComponentType == type);
                }

                // CPU stats
                var cpuStatItem = GetStatsItem(StatsComponentType.CPU);
                if (cpuStatItem != null && cpuStatItem.IsEnabled)
                {
                    string cpuValue = FetchCPUStat();
                    ViewModel.Instance.UpdateComponentStat(StatsComponentType.CPU, cpuValue);
                    //ViewModel.Instance.SetComponentStatMaxValue(StatsComponentType.CPU, "100 %");
                }

                // GPU stats
                var gpuStatItem = GetStatsItem(StatsComponentType.GPU);
                if (gpuStatItem != null && gpuStatItem.IsEnabled)
                {
                    string gpuValue = FetchGPUStat();
                    ViewModel.Instance.UpdateComponentStat(StatsComponentType.GPU, gpuValue);
                    //ViewModel.Instance.SetComponentStatMaxValue(StatsComponentType.GPU, "100 %");
                }

                // RAM stats
                var ramStatItem = GetStatsItem(StatsComponentType.RAM);
                if (ramStatItem != null && ramStatItem.IsEnabled)
                {
                    var (usedMemory, totalMemory) = FetchRAMStats();
                    ViewModel.Instance.UpdateComponentStat(StatsComponentType.RAM, usedMemory);
                    ViewModel.Instance.SetComponentStatMaxValue(StatsComponentType.RAM, totalMemory);
                }

                // VRAM stats
                var vramStatItem = GetStatsItem(StatsComponentType.VRAM);
                if (vramStatItem != null && vramStatItem.IsEnabled)
                {
                    string vramValue = FetchVRAMStat();
                    string vramValueMax = FetchVRAMMaxStat();
                    ViewModel.Instance.UpdateComponentStat(StatsComponentType.VRAM, vramValue);
                    ViewModel.Instance.SetComponentStatMaxValue(StatsComponentType.VRAM, vramValueMax);
                }

                // FPS stats
                var fpsStatItem = GetStatsItem(StatsComponentType.FPS);
                if (fpsStatItem != null && fpsStatItem.IsEnabled)
                {
                    string fpsValue = FetchFPSStat();
                    ViewModel.Instance.UpdateComponentStat(StatsComponentType.FPS, fpsValue);
                }
            }
            catch (Exception)
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
                if (pname.Length == 0)
                    return false;
                else
                    return true;
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
                return false;
            }
        }


        private static string FetchCPUStat()
        {
            var cpuHardware = CurrentSystem.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
            if (cpuHardware != null)
            {
                cpuHardware.Update();
                var loadSensor = cpuHardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load && s.Name == "CPU Total");
                if (loadSensor != null)
                {
                    return $"{loadSensor.Value:F1}";
                }
            }
            return "N/A";
        }


        private static string FetchGPUStat()
        {
            LibreHardwareMonitor.Hardware.Hardware GetDedicatedGPU(HardwareType type)
            {
                return CurrentSystem.Hardware
                       .FirstOrDefault(h => h.HardwareType == type && !h.Name.ToLower().Contains("integrated"))
                       as LibreHardwareMonitor.Hardware.Hardware;
            }

            var gpuHardware = GetDedicatedGPU(HardwareType.GpuNvidia)
                           ?? GetDedicatedGPU(HardwareType.GpuAmd)
                           ?? CurrentSystem.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.GpuIntel) as LibreHardwareMonitor.Hardware.Hardware;

            if (gpuHardware != null)
            {
                gpuHardware.Update();
                var loadSensor = gpuHardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load && s.Name == "GPU Core");
                if (loadSensor != null)
                {
                    return $"{loadSensor.Value:F1}";
                }
            }
            return "N/A";
        }







        private static string FetchVRAMStat()
        {
            LibreHardwareMonitor.Hardware.Hardware GetDedicatedGPU(HardwareType type)
            {
                return CurrentSystem.Hardware
                       .FirstOrDefault(h => h.HardwareType == type && !h.Name.ToLower().Contains("integrated"))
                       as LibreHardwareMonitor.Hardware.Hardware;
            }

            var gpuHardware = GetDedicatedGPU(HardwareType.GpuNvidia)
                           ?? GetDedicatedGPU(HardwareType.GpuAmd)
                           ?? CurrentSystem.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.GpuIntel) as LibreHardwareMonitor.Hardware.Hardware;

            if (gpuHardware != null)
            {
                gpuHardware.Update();
                var usedVRAMSensor = gpuHardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.SmallData && s.Name == "GPU Memory Used");
                if (usedVRAMSensor != null)
                {
                    double vramInGB = (double)usedVRAMSensor.Value / 1024; // Convert MB to GB
                    return $"{vramInGB:F1}";
                }
            }
            return "N/A";
        }

        private static string FetchVRAMMaxStat()
        {
            LibreHardwareMonitor.Hardware.Hardware GetDedicatedGPU(HardwareType type)
            {
                return CurrentSystem.Hardware
                       .FirstOrDefault(h => h.HardwareType == type && !h.Name.ToLower().Contains("integrated"))
                       as LibreHardwareMonitor.Hardware.Hardware;
            }

            var gpuHardware = GetDedicatedGPU(HardwareType.GpuNvidia)
                           ?? GetDedicatedGPU(HardwareType.GpuAmd)
                           ?? CurrentSystem.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.GpuIntel) as LibreHardwareMonitor.Hardware.Hardware;

            if (gpuHardware != null)
            {
                gpuHardware.Update();
                var totalVRAMSensor = gpuHardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.SmallData && s.Name == "GPU Memory Total");
                if (totalVRAMSensor != null)
                {
                    double vramMaxInGB = (double)totalVRAMSensor.Value / 1024; // Convert MB to GB
                    return $"{vramMaxInGB:F1}";
                }
            }
            return "N/A";
        }

        private static (string UsedMemory, string MaxMemory) FetchRAMStats()
        {
            var ramHardware = CurrentSystem.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Memory);
            if (ramHardware != null)
            {
                ramHardware.Update();

                // Fetch the 'Memory Available' sensor value
                var availableMemorySensor = ramHardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Data && s.Name == "Memory Available");

                // Fetch the 'Memory Used' sensor value
                var usedMemorySensor = ramHardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Data && s.Name == "Memory Used");

                if (availableMemorySensor != null && usedMemorySensor != null)
                {
                    // Calculate the total memory (in GB)
                    var totalMemory = availableMemorySensor.Value + usedMemorySensor.Value;
                    return ($"{usedMemorySensor.Value:F1}", $"{totalMemory:F1}");
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
