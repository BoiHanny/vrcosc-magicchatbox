using System.Collections.Generic;

namespace vrcosc_magicchatbox.Core.Osc;

public sealed class OscBuildResult
{
    public required string Message { get; init; }

    public int Length => Message.Length;

    public bool ExceededLimit { get; init; }

    public required IReadOnlyList<string> IncludedProviders { get; init; }

    public required IReadOnlyList<string> TrimmedProviders { get; init; }

    public required IReadOnlyDictionary<string, int> SegmentLengths { get; init; }
}
