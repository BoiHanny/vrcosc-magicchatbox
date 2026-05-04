using System;
using System.Globalization;
using System.Linq;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Formats the current time using user TimeSettings.
/// Extracted from ComponentStatsModule.GetTime() to eliminate static coupling.
/// </summary>
public sealed class TimeFormattingService : ITimeFormattingService
{
    private readonly TimeSettings _ts;

    public TimeFormattingService(ISettingsProvider<TimeSettings> timeSettingsProvider)
    {
        _ts = timeSettingsProvider.Value;
    }

    public string GetFormattedCurrentTime()
    {
        try
        {
            DateTimeOffset localDateTime = DateTimeOffset.Now;

            var (timeZoneInfo, standardAbbr, daylightAbbr) = ResolveTimeZone(_ts.SelectedTimeZone);

            bool isDst = _ts.AutoSetDaylight
                ? timeZoneInfo.IsDaylightSavingTime(TimeZoneInfo.ConvertTime(localDateTime, timeZoneInfo))
                : _ts.UseDaylightSavingTime;

            string timezoneLabel = isDst ? daylightAbbr : standardAbbr;

            var dateTimeWithZone = GetDateTimeWithZone(
                _ts.AutoSetDaylight, _ts.TimeShowTimeZone,
                localDateTime, timeZoneInfo,
                out TimeSpan timeZoneOffset);

            string offsetSign = timeZoneOffset < TimeSpan.Zero ? "-" : "+";
            int totalOffsetHours = (int)timeZoneOffset.TotalHours;
            int offsetMinutes = Math.Abs(timeZoneOffset.Minutes);
            string offsetString = offsetMinutes == 0
                ? $"{offsetSign}{Math.Abs(totalOffsetHours)}"
                : $"{offsetSign}{Math.Abs(totalOffsetHours)}:{offsetMinutes:00}";

            string timeZoneDisplay = $" ({timezoneLabel}{offsetString})";

            return FormatTime(dateTimeWithZone, _ts.Time24H, _ts.TimeShowTimeZone, timeZoneDisplay);
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return "00:00 XX";
        }
    }

    private string FormatTime(
        DateTimeOffset dateTimeWithZone, bool time24H, bool showTimeZone, string timeZoneDisplay)
    {
        CultureInfo culture = _ts.UseSystemCulture ? CultureInfo.CurrentCulture : CultureInfo.InvariantCulture;
        string format = time24H ? "HH:mm" : "hh:mm tt";
        string formatted = dateTimeWithZone.ToString(format, culture);
        return showTimeZone ? formatted + timeZoneDisplay : formatted;
    }

    private DateTimeOffset GetDateTimeWithZone(
        bool autoSetDaylight, bool showTimeZone,
        DateTimeOffset localDateTime, TimeZoneInfo tzInfo,
        out TimeSpan offset)
    {
        if (autoSetDaylight)
        {
            if (showTimeZone)
            {
                offset = tzInfo.GetUtcOffset(localDateTime);
                return TimeZoneInfo.ConvertTime(localDateTime, tzInfo);
            }
            else
            {
                offset = TimeZoneInfo.Local.GetUtcOffset(localDateTime);
                return localDateTime;
            }
        }
        else
        {
            offset = tzInfo.BaseUtcOffset;
            if (_ts.UseDaylightSavingTime)
            {
                // Use the adjustment rule matching the current date, not just the first rule
                var rules = tzInfo.GetAdjustmentRules();
                var matchingRule = rules.FirstOrDefault(r =>
                    localDateTime.DateTime >= r.DateStart && localDateTime.DateTime <= r.DateEnd);
                TimeSpan adjustment = matchingRule?.DaylightDelta ?? TimeSpan.Zero;
                offset = offset.Add(adjustment);
            }
            return localDateTime.ToOffset(offset);
        }
    }

    private static (TimeZoneInfo Info, string Standard, string Daylight) ResolveTimeZone(Timezone tz) => tz switch
    {
        Timezone.UTC => (TimeZoneInfo.FindSystemTimeZoneById("UTC"), "UTC", "UTC"),
        Timezone.GMT => (TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time"), "GMT", "BST"),
        Timezone.EST => (TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"), "EST", "EDT"),
        Timezone.CST => (TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"), "CST", "CDT"),
        Timezone.MST => (TimeZoneInfo.FindSystemTimeZoneById("Mountain Standard Time"), "MST", "MDT"),
        Timezone.PST => (TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"), "PST", "PDT"),
        Timezone.AKST => (TimeZoneInfo.FindSystemTimeZoneById("Alaskan Standard Time"), "AKST", "AKDT"),
        Timezone.HST => (TimeZoneInfo.FindSystemTimeZoneById("Hawaiian Standard Time"), "HST", "HST"),
        Timezone.CET => (TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time"), "CET", "CEST"),
        Timezone.EET => (TimeZoneInfo.FindSystemTimeZoneById("E. Europe Standard Time"), "EET", "EEST"),
        Timezone.IST => (TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"), "IST", "IST"),
        Timezone.CSTChina => (TimeZoneInfo.FindSystemTimeZoneById("China Standard Time"), "CST", "CST"),
        Timezone.JST => (TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time"), "JST", "JST"),
        Timezone.KST => (TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time"), "KST", "KST"),
        Timezone.MSK => (TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time"), "MSK", "MSK"),
        Timezone.AEST => (TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time"), "AEST", "AEDT"),
        Timezone.NZST => (TimeZoneInfo.FindSystemTimeZoneById("New Zealand Standard Time"), "NZST", "NZDT"),
        Timezone.BRT => (TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"), "BRT", "BRST"),
        Timezone.SAST => (TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time"), "SAST", "SAST"),
        _ => (TimeZoneInfo.Local, TimeZoneInfo.Local.StandardName, TimeZoneInfo.Local.DaylightName),
    };
}
