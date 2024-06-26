using CommunityToolkit.Mvvm.ComponentModel;
using MagicChatboxV2.Models;
using MagicChatboxV2.Services;
using Serilog;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace MagicChatboxV2.Modules
{
    public partial class CurrentTimeSettings : ObservableObject, ISettings
    {
        [ObservableProperty]
        private bool enabled;

        [ObservableProperty]
        private bool enabledVR;

        [ObservableProperty]
        private bool enabledDesktop;

        [ObservableProperty]
        private string settingVersion;

        [ObservableProperty]
        private int modulePosition;

        [ObservableProperty]
        private int moduleMemberGroupNumbers;

        [ObservableProperty]
        private CurrentTimeZone selectedTimeZone = CurrentTimeZone.EST;

        [ObservableProperty]
        private bool autoSetDaylight = true;

        [ObservableProperty]
        private bool convertTimeToTimeZone = false;

        [ObservableProperty]
        private bool time24H = true;

        [ObservableProperty]
        private bool useSystemCulture = true;

        public void Dispose()
        {
            // Dispose of unmanaged resources here.
        }

        public enum CurrentTimeZone
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
    }

    public partial class CurrentTimeModule : ObservableObject, IModule<CurrentTimeSettings>
    {
        public string ModuleName => "Current time";
        public string ModuleVersion => "1.0.0"; // Example version
        public string ModuleDescription => "Displays the current time in various time zones."; // Example description

        [ObservableProperty]
        private bool isActive;

        [ObservableProperty]
        private DateTime lastUpdated;

        public event EventHandler DataUpdated;

        [ObservableProperty]
        private CurrentTimeSettings settings;

        private readonly ISettingsService _settingsService;
        private readonly IAppOutputService _appOutputService;

        public CurrentTimeModule(ISettingsService settingsService, IAppOutputService appOutputService)
        {
            _settingsService = settingsService;
            _appOutputService = appOutputService;
            Settings = new CurrentTimeSettings();
            IsActive = true;
        }

        public async Task InitializeAsync()
        {
            await LoadStateAsync();
            Console.WriteLine($"{ModuleName} initialized.");
        }

        public async Task LoadStateAsync()
        {
            Settings = await _settingsService.LoadSettingsAsync<CurrentTimeSettings>();
        }

        public async Task SaveStateAsync()
        {
            await _settingsService.SaveSettingsAsync(Settings);
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
                _appOutputService.LogError($"Failed to update data for {ModuleName}: {ex.Message}");
            }
        }

        public string GetFormattedOutput()
        {
            return GetFormattedTime(DateTimeOffset.Now, Settings);
        }

        public string UpdateAndGetOutput()
        {
            UpdateData();
            return GetFormattedOutput();
        }

        private TimeZoneInfo GetTimeZoneInfo(CurrentTimeSettings.CurrentTimeZone selectedTimeZone)
        {
            string timeZoneId = selectedTimeZone switch
            {
                CurrentTimeSettings.CurrentTimeZone.UTC => "UTC",
                CurrentTimeSettings.CurrentTimeZone.EST => "Eastern Standard Time",
                CurrentTimeSettings.CurrentTimeZone.CST => "Central Standard Time",
                CurrentTimeSettings.CurrentTimeZone.PST => "Pacific Standard Time",
                CurrentTimeSettings.CurrentTimeZone.CET => "Central European Standard Time",
                CurrentTimeSettings.CurrentTimeZone.AEST => "E. Australia Standard Time",
                CurrentTimeSettings.CurrentTimeZone.GMT => "GMT Standard Time",
                CurrentTimeSettings.CurrentTimeZone.IST => "India Standard Time",
                CurrentTimeSettings.CurrentTimeZone.JST => "Tokyo Standard Time",
                _ => TimeZoneInfo.Local.Id
            };
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }

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
                if (timeZoneInfo.SupportsDaylightSavingTime && timeZoneInfo.IsDaylightSavingTime(localDateTime))
                {
                    TimeSpan adjustment = timeZoneInfo.GetAdjustmentRules().FirstOrDefault()?.DaylightDelta ?? TimeSpan.Zero;
                    timeZoneOffset = timeZoneOffset.Add(adjustment);
                }
                dateTimeWithZone = localDateTime.ToOffset(timeZoneOffset);
            }
            return dateTimeWithZone;
        }

        public static string GetFormattedTime(DateTimeOffset dateTimeWithZone, CurrentTimeSettings settings)
        {
            TimeZoneInfo timeZoneInfo = settings.ConvertTimeToTimeZone ? TimeZoneInfo.FindSystemTimeZoneById(settings.SelectedTimeZone.ToString()) : TimeZoneInfo.Local;
            TimeSpan timeZoneOffset;
            dateTimeWithZone = GetDateTimeWithZone(
                settings.AutoSetDaylight,
                settings.ConvertTimeToTimeZone,
                dateTimeWithZone,
                timeZoneInfo,
                out timeZoneOffset);

            string timeZoneDisplay = settings.ConvertTimeToTimeZone ?
                $" ({settings.SelectedTimeZone}{(timeZoneOffset < TimeSpan.Zero ? "" : "+")}{timeZoneOffset.Hours:00})" :
                "";

            CultureInfo userCulture = settings.UseSystemCulture ? CultureInfo.CurrentCulture : CultureInfo.InvariantCulture;
            string timeFormat = settings.Time24H ? "HH:mm" : "hh:mm tt";

            string formattedTime = dateTimeWithZone.ToString(timeFormat, userCulture);

            return settings.ConvertTimeToTimeZone ? formattedTime + timeZoneDisplay : formattedTime;
        }

        public void Dispose()
        {
            Settings.Dispose();
        }
    }
}
