using System.Collections.Generic;

namespace vrcosc_magicchatbox.Core.Osc;

/// <summary>
/// Immutable context passed to each <see cref="IOscProvider"/> during message assembly.
/// Providers use this to make budget-aware formatting decisions.
/// </summary>
public sealed class OscBuildContext
{
    public const int MaxOscLength = Constants.OscMaxMessageLength;

    /// <summary>Segments already committed by higher-priority providers.</summary>
    public IReadOnlyList<string> CurrentSegments { get; init; } = [];

    /// <summary>Separator string placed between segments (e.g., " ┆ " or "\n").</summary>
    public required string Separator { get; init; }

    /// <summary>User-configured prefix prepended to the final message.</summary>
    public required string Prefix { get; init; }

    /// <summary>User-configured suffix appended to the final message.</summary>
    public required string Suffix { get; init; }

    /// <summary>Whether VRChat is currently running.</summary>
    public bool IsVRRunning { get; init; }

    /// <summary>
    /// Calculates the number of characters remaining if <paramref name="candidate"/> were added.
    /// </summary>
    public int RemainingCharsIf(string candidate)
    {
        var segments = new List<string>(CurrentSegments) { candidate };
        string joined = string.Join(Separator, segments);
        if (!string.IsNullOrEmpty(joined))
            joined = $"{Prefix}{joined}{Suffix}";
        return MaxOscLength - joined.Length;
    }

    /// <summary>
    /// Returns the total length the message would be if <paramref name="candidate"/> were added.
    /// </summary>
    public int LengthIf(string candidate)
    {
        return MaxOscLength - RemainingCharsIf(candidate);
    }

    /// <summary>
    /// Returns true if <paramref name="candidate"/> would fit within the 144-char limit.
    /// </summary>
    public bool WouldFit(string candidate) => RemainingCharsIf(candidate) >= 0;
}
