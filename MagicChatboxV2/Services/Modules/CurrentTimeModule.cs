using MagicChatboxV2.UIVM.Models;
using Serilog;
using System;
using System.Globalization;
using System.Linq;

namespace MagicChatboxV2.Services.Modules
{
    public class CurrentTimeSettings : ISettings
    {
        public enum TimeZone
        {
            UTC,
            EST,
            CST,
            PST,
            CET,
            AEST,
            GMT,
            IST,
            JST
        }

        public TimeZone SelectedTimeZone { get; set; } = TimeZone.EST;
        public bool AutoSetDaylight { get; set; } = true;
        public bool ManualDstAdjustment { get; set; } = false;
        public bool ConvertTimeToTimeZone { get; set; } = false;
        public bool Time24H { get; set; } = true;

        public void Dispose() { }
    }

    public class CurrentTimeModule : IModule
    {
        public string ModuleName => "Current time";
        public bool IsActive { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsEnabled_VR { get; set; }
        public bool IsEnabled_DESKTOP { get; set; }



        public DateTime LastUpdated { get; private set; }

        public event EventHandler DataUpdated;

        public ISettings Settings { get; set; }
        public int ModulePosition { get; set; }
        public int ModuleMemberGroupNumbers { get; set; }

        public CurrentTimeModule()
        {
            Settings = new CurrentTimeSettings();
            IsActive = true;
            IsEnabled = true;
            IsEnabled_VR = true;
            IsEnabled_DESKTOP = true;
        }

        public void Initialize()
        {
            Console.WriteLine($"{ModuleName} initialized.");
        }

        public void StartUpdates()
        {
            // Start background updates if required
        }

        public void StopUpdates()
        {
            // Stop background updates if required
        }

        public void UpdateData()
        {
            try
            {
                LastUpdated = DateTime.Now;
                DataUpdated?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to update data for {ModuleName}");
            }

        }

        public string GetFormattedOutput()
        {
            try
            {
                var settings = (CurrentTimeSettings)Settings;
                DateTimeOffset localDateTime = DateTimeOffset.Now;
                TimeZoneInfo timeZoneInfo = GetTimeZoneInfo(settings.SelectedTimeZone);
                TimeSpan timeZoneOffset;
                var dateTimeWithZone = GetDateTimeWithZone(
                        settings.AutoSetDaylight,
                        settings.ManualDstAdjustment,
                        settings.ConvertTimeToTimeZone,
                        localDateTime,
                        timeZoneInfo,
                        out timeZoneOffset);

                string timeZoneDisplay = settings.ConvertTimeToTimeZone ?
                    $" ({GetTimeZoneLabel(settings.SelectedTimeZone)}{(timeZoneOffset < TimeSpan.Zero ? "" : "+")}{timeZoneOffset.Hours:00})" :
                    "";

                return GetFormattedTime(
                    dateTimeWithZone,
                    settings.Time24H,
                    settings.ConvertTimeToTimeZone,
                    timeZoneDisplay);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error formatting current time");
                return "Time Unavailable";
            }
        }

        public string UpdateAndGetOutput()
        {
            UpdateData();
            return GetFormattedOutput();
        }

        public void SaveState()
        {
            // Save state if required
        }

        public void LoadState()
        {
            // Load state if required
        }

        public void Dispose()
        {
            Settings.Dispose();
        }

        private TimeZoneInfo GetTimeZoneInfo(CurrentTimeSettings.TimeZone selectedTimeZone)
        {
            string timeZoneId = selectedTimeZone switch
            {
                CurrentTimeSettings.TimeZone.UTC => "UTC",
                CurrentTimeSettings.TimeZone.EST => "Eastern Standard Time",
                CurrentTimeSettings.TimeZone.CST => "Central Standard Time",
                CurrentTimeSettings.TimeZone.PST => "Pacific Standard Time",
                CurrentTimeSettings.TimeZone.CET => "Central European Standard Time",
                CurrentTimeSettings.TimeZone.AEST => "E. Australia Standard Time",
                CurrentTimeSettings.TimeZone.GMT => "GMT Standard Time",
                CurrentTimeSettings.TimeZone.IST => "India Standard Time",
                CurrentTimeSettings.TimeZone.JST => "Tokyo Standard Time",
                _ => TimeZoneInfo.Local.Id
            };
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }

        private string GetTimeZoneLabel(CurrentTimeSettings.TimeZone timeZone)
        {
            return timeZone.ToString();
        }

        private DateTimeOffset GetDateTimeWithZone(
            bool autoSetDaylight,
            bool manualDstAdjustment,
            bool timeShowTimeZone,
            DateTimeOffset localDateTime,
            TimeZoneInfo timeZoneInfo,
            out TimeSpan timeZoneOffset)
        {
            if (!timeShowTimeZone)
            {
                // Use local time directly without conversion.
                timeZoneOffset = TimeZoneInfo.Local.GetUtcOffset(localDateTime);
                return localDateTime;
            }
            else
            {
                if (autoSetDaylight)
                {
                    // Convert time considering daylight saving time.
                    timeZoneOffset = timeZoneInfo.GetUtcOffset(localDateTime);
                    return TimeZoneInfo.ConvertTime(localDateTime, timeZoneInfo);
                }
                else
                {
                    // Convert time without considering daylight saving time adjustments.
                    timeZoneOffset = timeZoneInfo.BaseUtcOffset;

                    // Apply manual DST adjustment if specified
                    if (manualDstAdjustment && timeZoneInfo.SupportsDaylightSavingTime && timeZoneInfo.IsDaylightSavingTime(localDateTime))
                    {
                        timeZoneOffset = timeZoneOffset.Add(TimeSpan.FromHours(1));
                    }

                    // Adjust dateTimeWithZone to the manually calculated offset
                    var adjustedDateTime = new DateTimeOffset(localDateTime.DateTime, timeZoneOffset);
                    return TimeZoneInfo.ConvertTime(adjustedDateTime, timeZoneInfo);
                }
            }
        }




        private string GetFormattedTime(
            DateTimeOffset dateTimeWithZone,
            bool time24H,
            bool timeShowTimeZone,
            string timeZoneDisplay)
        {
            string timeFormat;
            if (time24H)
            {
                // Use 24-hour format
                timeFormat = "HH:mm";
            }
            else
            {
                // Use 12-hour format with AM/PM
                timeFormat = "hh:mm tt";
            }

            // If the current culture doesn't support AM/PM as expected, explicitly use a culture that does.
            CultureInfo culture = new CultureInfo("en-US");

            // Format the dateTimeWithZone using the specified format and culture
            string formattedTime = dateTimeWithZone.ToString($"{timeFormat}{timeZoneDisplay}", culture);

            return formattedTime;
        }
    }
}
