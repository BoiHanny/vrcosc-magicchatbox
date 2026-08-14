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

        // The prefix and suffix are always sent, so they always cost. Skipping them while the line
        // was still empty told the first provider it had more room than it does.
        return MaxOscLength - (Prefix.Length + joined.Length + Suffix.Length);
    }

    public int LengthIf(string candidate)
    {
        return MaxOscLength - RemainingCharsIf(candidate);
    }

    public bool WouldFit(string candidate) => RemainingCharsIf(candidate) >= 0;
}
