using System.Threading.Tasks;

namespace vrcosc_magicchatbox.Core.Services;

public interface IMediaLinkPersistenceService
{
    Task LoadMediaSessionsAsync();
    void SaveMediaSessions();
    Task LoadSeekbarStylesAsync();
    void SaveSeekbarStyles();
    void AddNewSeekbarStyle();
    void DeleteSelectedSeekbarStyleAndSelectDefault();
    void ExportSeekbarStyles(string filePath);
    int ImportSeekbarStyles(string filePath);
}
