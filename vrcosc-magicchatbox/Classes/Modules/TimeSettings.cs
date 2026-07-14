using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Settings for the time display module, including timezone selection and format options.
/// </summary>
public partial class TimeSettings : VersionedSettings
{
    /// <summary>
    /// Static lookup for timezone display names. Formerly on ViewModel.
    /// </summary>
    public static Dictionary<Timezone, string> TimezoneFriendlyNames { get; } = new()
    {
        { Timezone.UTC, "Coordinated Universal Time (UTC)" },
        { Timezone.GMT, "Greenwich Mean Time (GMT)" },
        { Timezone.EST, "Eastern Time (US & Canada)" },
        { Timezone.CST, "Central Time (US & Canada)" },
        { Timezone.MST, "Mountain Time (US & Canada)" },
        { Timezone.PST, "Pacific Time (US & Canada)" },
        { Timezone.AKST, "Alaska Time (AKST)" },
        { Timezone.HST, "Hawaii Standard Time (HST)" },
        { Timezone.CET, "Central European Time (CET)" },
        { Timezone.EET, "Eastern European Time (EET)" },
        { Timezone.IST, "India Standard Time (IST)" },
        { Timezone.CSTChina, "China Standard Time (CST)" },
        { Timezone.JST, "Japan Standard Time (JST)" },
        { Timezone.KST, "Korea Standard Time (KST)" },
        { Timezone.MSK, "Moscow Standard Time (MSK)" },
        { Timezone.AEST, "Australian Eastern Time (AET)" },
        { Timezone.NZST, "New Zealand Time (NZT)" },
        { Timezone.BRT, "Brasília Time (BRT)" },
        { Timezone.SAST, "South Africa Standard Time (SAST)" },
    };

    [ObservableProperty] private bool _time24H = false;
    [ObservableProperty] private bool _prefixTime = false;
    [ObservableProperty] private bool _timeShowTimeZone = false;
    [ObservableProperty] private Timezone _selectedTimeZone = Timezone.UTC;
    [ObservableProperty] private bool _useDaylightSavingTime = true;
    [ObservableProperty] private bool _autoSetDaylight = true;
    [ObservableProperty] private bool _useSystemCulture = false;
    [ObservableProperty] private DateTime _bussyBoysDate = DateTime.Now;
    [ObservableProperty] private bool _bussyBoysDateEnable = false;
    [ObservableProperty] private bool _bussyBoysMultiMODE = false;
}
