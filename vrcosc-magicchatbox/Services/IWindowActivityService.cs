using vrcosc_magicchatbox.Classes.Modules;

namespace vrcosc_magicchatbox.Services;

public interface IWindowActivityService
{
    WindowActivitySettings Settings { get; }
    void SaveSettings();
    string GetForegroundProcessName();
    bool IsOSCServerSuspended();
    void KillOSCServer();
    int ResetWindowActivity();
    int SmartCleanup();
    int CleanAndKeepAppsWithSettings();
}
