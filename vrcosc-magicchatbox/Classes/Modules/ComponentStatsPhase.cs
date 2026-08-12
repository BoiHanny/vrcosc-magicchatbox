using System;
using System.Globalization;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Where the component stats integration is in its lifecycle. The tile reads this instead of
/// inferring its state from the master toggle, because opening the sensor service takes long
/// enough that "switched on" and "producing readings" are not the same thing.
/// </summary>
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

    /// <summary>
    /// The small line beside the tile title. An empty result means show nothing at all: with no
    /// reading to report, blank is honest where a stale or invented timestamp is not.
    /// </summary>
    public static string Describe(ComponentStatsPhase phase, DateTime? lastUpdate)
        => phase switch
        {
            ComponentStatsPhase.Starting => StartingText,
            ComponentStatsPhase.Stopping => StoppingText,

            // Running without a timestamp is the window between the sensors coming up and the first
            // set of readings landing, which reads as part of the same start to anyone watching.
            ComponentStatsPhase.Running => lastUpdate.HasValue
                ? lastUpdate.Value.ToString("T", CultureInfo.CurrentCulture)
                : StartingText,

            _ => string.Empty,
        };
}
