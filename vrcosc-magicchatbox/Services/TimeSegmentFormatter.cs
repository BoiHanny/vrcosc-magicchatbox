using vrcosc_magicchatbox.Core.Osc.Text;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// How the clock is written onto the chatbox line.
/// </summary>
/// <remarks>
/// Shared with the settings preview on purpose. A preview that composes the line its own way is a
/// second implementation that will drift, and the user finds out it drifted by reading their own
/// chatbox in a world.
/// </remarks>
public static class TimeSegmentFormatter
{
    /// <summary>The word placed in front of the clock when the label is switched on.</summary>
    public const string PrefixLabel = "My time";

    /// <summary>
    /// The clock as the chatbox receives it. The time itself is what the reader is here for, so it
    /// stays full size and only the label is raised; the writer places the space.
    /// </summary>
    public static string Compose(string? clock, bool showLabel)
    {
        string value = clock ?? string.Empty;
        if (value.Length == 0)
            return string.Empty;

        return showLabel
            ? new SegmentWriter().Field(OscText.Label(PrefixLabel), OscText.Value(value)).Text
            : value;
    }
}
