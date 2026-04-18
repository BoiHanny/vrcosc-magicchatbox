namespace vrcosc_magicchatbox.Core.Services;

/// <summary>
/// Manages status list persistence and CRUD (StatusList.json).
/// Load populates ChatStatusDisplayState.StatusList.
/// Save serializes the current shared state.
/// </summary>
public interface IStatusListService
{
    void LoadStatusList();
    void SaveStatusList();
}
