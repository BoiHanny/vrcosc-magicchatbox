using vrcosc_magicchatbox.Core.Osc.Text;

namespace vrcosc_magicchatbox.Services;

public static class TimeSegmentFormatter
{
    public const string PrefixLabel = "My time";

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
