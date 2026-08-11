namespace vrcosc_magicchatbox.Services;

public interface IBanEnforcementService
{
    void ProcessBan(string bannedUserID, string reason);
}
