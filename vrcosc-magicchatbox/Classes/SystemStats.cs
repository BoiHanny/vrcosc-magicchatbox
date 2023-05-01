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
        public static string GetTime()
        {
            try
            {
                CultureInfo userCulture = CultureInfo.CurrentCulture;
                DateTimeOffset localDateTime = DateTimeOffset.Now;
                TimeZoneInfo timeZoneInfo;
                string TimezoneLabel = null;

                switch (ViewModel.Instance.SelectedTimeZone)
                {
                    case Timezone.UTC:
                        timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("UTC");
                        TimezoneLabel = "UTC";
                        break;
                    case Timezone.EST:
                        timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                        TimezoneLabel = "EST";
                        break;
                    case Timezone.CST:
                        timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
                        TimezoneLabel = "CST";
                        break;
                    case Timezone.PST:
                        timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
                        TimezoneLabel = "PST";
                        break;
                    case Timezone.CET:
                        timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
                        TimezoneLabel = "CET";
                        break;
                    case Timezone.AEST:
                        timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("E. Australia Standard Time");
                        TimezoneLabel = "AEST";
                        break;
                    default:
                        timeZoneInfo = TimeZoneInfo.Local;
                        TimezoneLabel = "??";
                        break;
                }

                DateTimeOffset dateTimeWithZone;
                TimeSpan timeZoneOffset;

                if (ViewModel.Instance.AutoSetDaylight)
                {
                    dateTimeWithZone = TimeZoneInfo.ConvertTime(localDateTime, timeZoneInfo);
                    timeZoneOffset = timeZoneInfo.GetUtcOffset(localDateTime);
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

                string timeZoneOffsetStr = timeZoneOffset.ToString("hh");
                string timeZoneDisplay = $" ({TimezoneLabel}{(timeZoneOffset < TimeSpan.Zero ? "-" : "+")}{timeZoneOffsetStr})";

                if (ViewModel.Instance.Time24H)
                {
                    return dateTimeWithZone.ToString($"HH:mm{(ViewModel.Instance.TimeShowTimeZone ? timeZoneDisplay : "")}", userCulture);
                }
                else
                {
                    return dateTimeWithZone.ToString($"hh:mm tt{(ViewModel.Instance.TimeShowTimeZone ? timeZoneDisplay : "")}", userCulture).ToUpper();
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
                return "00:00 XX";
            }
        }



    }
}
