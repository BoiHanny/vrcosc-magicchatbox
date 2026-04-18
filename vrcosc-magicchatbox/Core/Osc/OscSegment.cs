namespace vrcosc_magicchatbox.Core.Osc;

/// <summary>
/// A single segment of text produced by a provider for the OSC message.
/// </summary>
public sealed class OscSegment
{
    /// <summary>The text content to include in the OSC message.</summary>
    public required string Text { get; init; }

    /// <summary>Optional shorter fallback when space is tight (Phase 3).</summary>
    public string? CompactText { get; init; }
}
