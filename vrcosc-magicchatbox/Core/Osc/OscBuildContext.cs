using System.Collections.Generic;

namespace vrcosc_magicchatbox.Core.Osc;

public sealed class OscBuildContext
{
    public const int MaxOscLength = Constants.OscMaxMessageLength;

    public IReadOnlyList<string> CurrentSegments { get; init; } = [];

    public required string Separator { get; init; }

    public required string Prefix { get; init; }

    public required string Suffix { get; init; }

    public bool IsVRRunning { get; init; }

    public bool AllowExternalRefresh { get; init; } = true;

    public int RemainingCharsIf(string candidate)
    {
        var segments = new List<string>(CurrentSegments) { candidate };
        string joined = string.Join(Separator, segments);
        if (!string.IsNullOrEmpty(joined))
            joined = $"{Prefix}{joined}{Suffix}";
        return MaxOscLength - joined.Length;
    }

    public int LengthIf(string candidate)
    {
        return MaxOscLength - RemainingCharsIf(candidate);
    }

    public bool WouldFit(string candidate) => RemainingCharsIf(candidate) >= 0;
}
