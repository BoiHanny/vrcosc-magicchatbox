using System.Collections.Generic;

namespace vrcosc_magicchatbox.Core.Osc;

public sealed class OscBuildResult
{
    public required string Message { get; init; }

    public int Length => Message.Length;

    public bool ExceededLimit { get; init; }

    public required IReadOnlyList<string> IncludedProviders { get; init; }

    public required IReadOnlyList<string> TrimmedProviders { get; init; }

    /// <summary>
    /// What every segment the build produced spends of the 144, keyed by UiKey. Trimmed providers are
    /// in here as well with the length they asked for, which is the only account the UI can give of
    /// why they were dropped.
    /// </summary>
    public required IReadOnlyDictionary<string, int> SegmentLengths { get; init; }
}
