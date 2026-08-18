using System;
using System.Globalization;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Services;

public sealed class TimeFormattingService : ITimeFormattingService
{
    private readonly TimeSettings _ts;

    public TimeFormattingService(ISettingsProvider<TimeSettings> timeSettingsProvider)
    {
        _ts = timeSettingsProvider.Value;
    }

    public string GetFormattedCurrentTime() => GetFormattedTime(DateTimeOffset.Now);

    public string GetFormattedTime(DateTimeOffset instant)
    {
        try
        {
            var (zone, standardAbbr, daylightAbbr) = _ts.TimeShowTimeZone
                ? ResolveTimeZone(_ts.SelectedTimeZone)
                : (TimeZoneInfo.Local, string.Empty, string.Empty);

            bool isDst = _ts.UseDaylightSavingTime && zone.IsDaylightSavingTime(instant);
            TimeSpan offset = isDst ? zone.GetUtcOffset(instant) : zone.BaseUtcOffset;

            string formatted = FormatTime(instant.ToOffset(offset), _ts.Time24H);

            return _ts.TimeShowTimeZone
                ? $"{formatted} ({(isDst ? daylightAbbr : standardAbbr)}{FormatOffset(offset)})"
                : formatted;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return "00:00 XX";
        }
    }

    private string FormatTime(DateTimeOffset dateTimeWithZone, bool time24H)
    {
        CultureInfo culture = _ts.UseSystemCulture ? CultureInfo.CurrentCulture : CultureInfo.InvariantCulture;
        return dateTimeWithZone.ToString(time24H ? "HH:mm" : "hh:mm tt", culture);
    }

    private static string FormatOffset(TimeSpan offset)
    {
        string sign = offset < TimeSpan.Zero ? "-" : "+";
        int hours = Math.Abs((int)offset.TotalHours);
        int minutes = Math.Abs(offset.Minutes);
        return minutes == 0 ? $"{sign}{hours}" : $"{sign}{hours}:{minutes:00}";
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
