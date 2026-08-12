namespace vrcosc_magicchatbox.Core.Services;

public interface IAppHistoryService
{
    void LoadAppHistory();
    void SaveAppHistory();

    bool CreateIfMissing(string path);
}
