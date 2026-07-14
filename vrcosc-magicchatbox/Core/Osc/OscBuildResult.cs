using System.Collections.Generic;

namespace vrcosc_magicchatbox.Core.Osc;

/// <summary>
/// Immutable result of an OSC message build pass.
/// The builder produces this; a presenter consumes it to update UI state.
/// </summary>
public sealed class OscBuildResult
{
    /// <summary>The final assembled OSC message string (empty if nothing to send).</summary>
    public required string Message { get; init; }

    /// <summary>Length of the final message.</summary>
    public int Length => Message.Length;

    /// <summary>True if any provider's output was dropped due to the 144-char limit.</summary>
    public bool ExceededLimit { get; init; }

    /// <summary>Provider UiKeys that were successfully included.</summary>
    public required IReadOnlyList<string> IncludedProviders { get; init; }

    /// <summary>Provider UiKeys that were trimmed (text didn't fit).</summary>
    public required IReadOnlyList<string> TrimmedProviders { get; init; }
}
