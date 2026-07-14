namespace vrcosc_magicchatbox.Core.Osc;

/// <summary>
/// Provides a segment of OSC chatbox text. Implementations are adapters over
/// existing modules/services — modules themselves do not implement this interface.
/// </summary>
public interface IOscProvider
{
    /// <summary>
    /// Key used for sort-order matching (e.g., "Component", "Network", "Time").
    /// Must match the canonical keys in <see cref="ViewModels.State.IntegrationDisplayState.DefaultSortOrder"/>.
    /// </summary>
    string SortKey { get; }

    /// <summary>
    /// Key used for UI opacity/dimming feedback (e.g., "ComponentStat", "NetworkStatistics").
    /// Must match the keys used by the opacity switch in the old OSCController.SetOpacity.
    /// </summary>
    string UiKey { get; }

    /// <summary>
    /// Provider priority for future trimming (Phase 3). Lower = higher priority.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Returns true if this provider should contribute to the OSC message
    /// given the current platform mode (VR vs Desktop) and its own enabled state.
    /// </summary>
    bool IsEnabledForCurrentMode(bool isVRRunning);

    /// <summary>
    /// Attempts to build this provider's text segment.
    /// Returns null if no output is available, or an <see cref="OscSegment"/> with the text.
    /// The <paramref name="context"/> provides budget information for space-aware formatting.
    /// </summary>
    OscSegment? TryBuild(OscBuildContext context);
}
