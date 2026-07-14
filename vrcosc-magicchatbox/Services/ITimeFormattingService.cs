namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Formats the current time according to user TimeSettings preferences.
/// Extracted from ComponentStatsModule.GetTime() to eliminate static coupling.
/// </summary>
public interface ITimeFormattingService
{
    /// <summary>
    /// Returns a formatted time string using the user's selected timezone, format, and DST settings.
    /// </summary>
    string GetFormattedCurrentTime();
}
