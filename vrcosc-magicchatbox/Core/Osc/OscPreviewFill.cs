namespace vrcosc_magicchatbox.Core.Osc;

public enum OscPreviewFill
{
    Roomy,
    Tight,
    Full,
}

public static class OscPreviewFillLevel
{
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
