namespace vrcosc_magicchatbox.Core.Osc;

/// <summary>
/// How full the 144 character line is. The counter beside the preview reads as plain text until the
/// room starts running out, which is the only moment it is worth looking at.
/// </summary>
public enum OscPreviewFill
{
    Roomy,
    Tight,
    Full,
}

public static class OscPreviewFillLevel
{
    /// <summary>The share of the line that counts as running out of room.</summary>
    public const double TightFraction = 0.85;

    public static OscPreviewFill Classify(int length, int limit = OscBuildContext.MaxOscLength)
    {
        if (limit <= 0)
            return OscPreviewFill.Roomy;

        if (length >= limit)
            return OscPreviewFill.Full;

        return length >= limit * TightFraction
            ? OscPreviewFill.Tight
            : OscPreviewFill.Roomy;
    }
}
