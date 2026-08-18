namespace vrcosc_magicchatbox.Services;

public interface IProcessPresenceService
{
    bool IsRunning(string processName);

    void Invalidate(string processName);

    void InvalidateAll();
}
