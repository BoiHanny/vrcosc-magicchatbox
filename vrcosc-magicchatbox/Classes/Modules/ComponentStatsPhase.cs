using System;
using System.Globalization;

namespace vrcosc_magicchatbox.Classes.Modules;

public enum ComponentStatsPhase
{
    Off,
    Starting,
    Running,
    Stopping,
}

public static class ComponentStatsStatus
{
    public const string StartingText = "starting…";
    public const string StoppingText = "stopping…";

    public static string Describe(ComponentStatsPhase phase, DateTime? lastUpdate)
        => phase switch
        {
            ComponentStatsPhase.Starting => StartingText,
            ComponentStatsPhase.Stopping => StoppingText,

            ComponentStatsPhase.Running => lastUpdate.HasValue
                ? lastUpdate.Value.ToString("T", CultureInfo.CurrentCulture)
                : StartingText,

            _ => string.Empty,
        };
}
