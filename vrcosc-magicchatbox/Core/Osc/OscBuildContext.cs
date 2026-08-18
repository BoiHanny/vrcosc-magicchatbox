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
        var segments = CurrentSegments;
        int used = Prefix.Length + Suffix.Length + (candidate?.Length ?? 0);

        for (int i = 0; i < segments.Count; i++)
            used += segments[i]?.Length ?? 0;

        used += Separator.Length * segments.Count;

        return MaxOscLength - used;
    }

    public int LengthIf(string candidate)
    {
        return MaxOscLength - RemainingCharsIf(candidate);
    }

    public bool WouldFit(string candidate) => RemainingCharsIf(candidate) >= 0;
}
