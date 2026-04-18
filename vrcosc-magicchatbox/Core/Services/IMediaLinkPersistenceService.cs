namespace vrcosc_magicchatbox.Core.Services;

/// <summary>
/// Manages MediaLink session persistence and seekbar styles.
/// Load/Save operate on MediaLinkDisplayState shared state.
/// </summary>
public interface IMediaLinkPersistenceService
{
    void LoadMediaSessions();
    void SaveMediaSessions();
    void LoadSeekbarStyles();
    void SaveSeekbarStyles();
    void AddNewSeekbarStyle();
    void DeleteSelectedSeekbarStyleAndSelectDefault();
}
