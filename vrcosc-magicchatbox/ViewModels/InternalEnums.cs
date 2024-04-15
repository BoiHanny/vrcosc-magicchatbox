
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
        EST,
        CST,
        PST,
        CET,
        AEST,
        GMT, 
        IST, 
        JST
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
        NotRunning
    }
}
