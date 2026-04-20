using System.Threading.Tasks;

namespace vrcosc_magicchatbox.Core.Services;

/// <summary>
/// Manages MediaLink session persistence and seekbar styles.
/// Load/Save operate on MediaLinkDisplayState shared state.
/// </summary>
public interface IMediaLinkPersistenceService
{
    Task LoadMediaSessionsAsync();
    void SaveMediaSessions();
    Task LoadSeekbarStylesAsync();
    void SaveSeekbarStyles();
    void AddNewSeekbarStyle();
    void DeleteSelectedSeekbarStyleAndSelectDefault();
}
