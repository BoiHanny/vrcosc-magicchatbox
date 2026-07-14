namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Enforces bans issued by the application's allowlist service.
/// </summary>
public interface IBanEnforcementService
{
    void ProcessBan(string bannedUserID, string reason);
}
