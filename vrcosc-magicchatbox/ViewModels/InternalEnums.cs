
using System.ComponentModel;

namespace vrcosc_magicchatbox.ViewModels
{
    public enum SortProperty
    {
        ProcessName,
        UsedNewMethod,
        ApplyCustomAppName,
        IsPrivateApp,
        FocusCount,
        ShowInfo
    }

    public enum Timezone
    {
        UTC,
        GMT,
        EST,
        CST,
        MST,
        PST,
        AKST,
        HST,
        CET,
        EET,
        IST,
        CSTChina,
        JST,
        KST,
        MSK,
        AEST,
        NZST,
        BRT,
        SAST
    }

    public enum WeatherLayoutMode
    {
        [Description("Single line")]
        SingleLine,
        [Description("Two lines")]
        TwoLines
    }

    public enum WeatherOrder
    {
        [Description("Time first")]
        TimeFirst,
        [Description("Weather first")]
        WeatherFirst
    }

    public enum WeatherUnitOverride
    {
        [Description("Use global unit")]
        UseGlobal,
        [Description("Celsius (C)")]
        Celsius,
        [Description("Fahrenheit (F)")]
        Fahrenheit
    }

    public enum WeatherFallbackMode
    {
        [Description("Hide on error")]
        Hide,
        [Description("Keep last value")]
        KeepLast,
        [Description("Show N/A")]
        ShowNA
    }

    public enum WeatherWindUnitOverride
    {
        [Description("Use global (based on temperature unit)")]
        UseGlobal,
        [Description("Kilometers per hour (km/h)")]
        KilometersPerHour,
        [Description("Miles per hour (mph)")]
        MilesPerHour
    }

    public enum WeatherLocationMode
    {
        [Description("Custom city")]
        CustomCity,
        [Description("Custom coordinates")]
        CustomCoordinates,
        [Description("IP-based (requires consent)")]
        IPBased
    }

    public enum StatsComponentType
    {
        GPU,
        CPU,
        RAM,
        VRAM,
        FPS,
        Unknown
    }

    public enum soundpadState
    {
        Playing,
        Paused,
        Stopped,
        NotRunning,
        Unknown
    }

    public enum TrackerBatterySortMode
    {
        [Description("As detected")]
        None,
        [Description("Name (A-Z)")]
        Name,
        [Description("Battery low to high")]
        BatteryLowToHigh,
        [Description("Battery high to low")]
        BatteryHighToLow,
        [Description("Type, then name")]
        TypeThenName
    }
}
