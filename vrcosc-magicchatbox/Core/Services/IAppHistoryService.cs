namespace vrcosc_magicchatbox.Core.Services;

/// <summary>
/// Manages app history persistence (AppHistory.json).
/// Load populates WindowActivityDisplayState.ScannedApps.
/// Save serializes the current shared state.
/// </summary>
public interface IAppHistoryService
{
    void LoadAppHistory();
    void SaveAppHistory();

    /// <summary>
    /// Creates the file at <paramref name="path"/> if it does not already exist.
    /// Returns true if the file was created; false if it already existed.
    /// </summary>
    bool CreateIfMissing(string path);
}
