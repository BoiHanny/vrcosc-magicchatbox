using System;
using System.Globalization;
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
                TimeSpan timeZoneOffset;
                string TimezoneLabel = null;

                switch (ViewModel.Instance.SelectedTimeZone)
                {
                    case Timezone.UTC:
                        timeZoneOffset = TimeSpan.Zero;
                        TimezoneLabel = "UTC";
                        break;
                    case Timezone.EST:
                        timeZoneOffset = TimeSpan.FromHours(-5);
                        TimezoneLabel = "EST";
                        break;
                    case Timezone.CST:
                        timeZoneOffset = TimeSpan.FromHours(-6);
                        TimezoneLabel = "CST";
                        break;
                    case Timezone.PST:
                        timeZoneOffset = TimeSpan.FromHours(-8);
                        TimezoneLabel = "PST";
                        break;
                    case Timezone.CET:
                        timeZoneOffset = TimeSpan.FromHours(1);
                        TimezoneLabel = "CET";
                        break;
                    case Timezone.AEST:
                        timeZoneOffset = TimeSpan.FromHours(10);
                        TimezoneLabel = "AEST";
                        break;
                    default:
                        timeZoneOffset = localDateTime.Offset;
                        TimezoneLabel = "??";
                        break;
                }

                DateTimeOffset dateTimeWithZone = localDateTime.ToOffset(timeZoneOffset);
                string formattedTimeZoneOffset = timeZoneOffset.ToString("hh");
                string timeZoneInfo = $" ({TimezoneLabel.Replace(" ", "")}{(timeZoneOffset < TimeSpan.Zero ? "-" : "+")}{formattedTimeZoneOffset})";






                if (ViewModel.Instance.Time24H)
                {
                    return dateTimeWithZone.ToString($"HH:mm{(ViewModel.Instance.TimeShowTimeZone ? timeZoneInfo : "")}", userCulture);
                }
                else
                {
                    return dateTimeWithZone.ToString($"hh:mm tt{(ViewModel.Instance.TimeShowTimeZone ? timeZoneInfo : "")}", userCulture).ToUpper();


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
