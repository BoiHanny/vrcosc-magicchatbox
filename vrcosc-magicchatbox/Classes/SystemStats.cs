using System;
using System.Globalization;
using System.Linq;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;
using static vrcosc_magicchatbox.ViewModels.ViewModel;

namespace vrcosc_magicchatbox.Classes
{
    public static class SystemStats
    {

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
                    ViewModel.Instance.SetComponentStatMaxValue(StatsComponentType.CPU, "100 %");
                }

                // GPU stats
                var gpuStatItem = GetStatsItem(StatsComponentType.GPU);
                if (gpuStatItem != null && gpuStatItem.IsEnabled)
                {
                    string gpuValue = FetchGPUStat();
                    ViewModel.Instance.UpdateComponentStat(StatsComponentType.GPU, gpuValue);
                    ViewModel.Instance.SetComponentStatMaxValue(StatsComponentType.GPU, "100 %");
                }

                // RAM stats
                var ramStatItem = GetStatsItem(StatsComponentType.RAM);
                if (ramStatItem != null && ramStatItem.IsEnabled)
                {
                    string ramValue = FetchRAMStat();
                    string ramValueMax = FetchRAMMaxStat();
                    ViewModel.Instance.UpdateComponentStat(StatsComponentType.RAM, ramValue);
                    ViewModel.Instance.SetComponentStatMaxValue(StatsComponentType.RAM, ramValueMax);
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


        private static string FetchCPUStat()
        {
            // Replace with actual CPU fetching logic.
            return "67";
        }

        private static string FetchGPUStat()
        {
            // Replace with actual GPU fetching logic.
            return "88";
        }

        private static string FetchRAMStat()
        {
            // Replace with actual RAM fetching logic.
            return "12";
        }

        private static string FetchVRAMStat()
        {
            // Replace with actual VRAM fetching logic.
            return "5.5";
        }

        private static string FetchFPSStat()
        {
            // Replace with actual FPS fetching logic.
            return "88";
        }

        private static string FetchRAMMaxStat()
        {
            //only one time??


            // Replace with actual FPS fetching logic.
            return "16 GB";
        }

        private static string FetchVRAMMaxStat()
        {
            //only one time??


            // Replace with actual FPS fetching logic.
            return "24 GB";
        }


    }
}
