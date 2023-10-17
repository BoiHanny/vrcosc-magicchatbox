
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
        AEST
    }

    public enum OSCParameterType
    {
        Int32,
        Single,
        Boolean,
        String
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
}
