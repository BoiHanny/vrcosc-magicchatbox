using System.Threading.Tasks;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Coordinates saving all application state to disk.
/// Used before shutdown, restart-as-admin, and emergency saves.
/// </summary>
public interface IStatePersistenceCoordinator
{
    /// <summary>
    /// Saves all settings, module data, and runtime state to disk synchronously.
    /// </summary>
    void PersistAllState();

    /// <summary>
    /// Sends a clear OSC message, waits for it to propagate, then saves all state.
    /// Intended for graceful shutdown.
    /// </summary>
    Task PrepareForShutdownAsync();
}
