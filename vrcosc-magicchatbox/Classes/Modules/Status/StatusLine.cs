using vrcosc_magicchatbox.Core.Osc.Text;

namespace vrcosc_magicchatbox.Classes.Modules.Status;

public static class StatusLine
{
    public static string Compose(string? message, string? icon, bool prefixIcon, int budget)
    {
        string plain = new SegmentWriter().Field(OscText.Value(message)).Text;
        if (plain.Length == 0)
            return string.Empty;

        if (!prefixIcon || string.IsNullOrWhiteSpace(icon))
            return SegmentWriter.Fit(budget, () => plain);

        return SegmentWriter.Fit(
            budget,
            () => new SegmentWriter().Field(OscText.Raw(icon), OscText.Value(message)).Text,
            () => plain);
    }
}
