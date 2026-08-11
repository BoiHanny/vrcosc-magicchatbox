namespace vrcosc_magicchatbox.Services;

public interface INavigationService
{
    bool OpenUrl(string url);

    bool OpenUrl(string url, string[] allowedDomains);

    bool OpenFolder(string folderPath);

    bool OpenFileInExplorer(string filePath);
}
