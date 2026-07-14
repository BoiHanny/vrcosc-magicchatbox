using vrcosc_magicchatbox.Classes.Modules;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Service for tracking the user's foreground window/application activity
/// and integrating it with the VRChat OSC chatbox.
/// </summary>
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
