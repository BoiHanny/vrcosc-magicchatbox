namespace MagicChatboxV2.UIVM.Models;

public interface IModule : IDisposable
{
    string ModuleName { get; }
    ISettings Settings { get; set; }
    bool IsActive { get; set; }
    bool IsEnabled { get; set; }
    bool IsEnabled_VR { get; set; }
    bool IsEnabled_DESKTOP { get; set; }
    int ModulePosition { get; set; }

    int ModuleMemberGroupNumbers { get; set; }
    DateTime LastUpdated { get; }

    void Initialize();
    void StartUpdates();
    void StopUpdates();
    void UpdateData();
    string GetFormattedOutput();
    string UpdateAndGetOutput();
    void SaveState();
    void LoadState();
    event EventHandler DataUpdated;
}



