namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Centralizes all environment-specific paths used by the application.
/// Eliminates hardcoded paths scattered across modules.
/// Must be configured before any path-consuming service is resolved.
/// </summary>
public interface IEnvironmentService
{
    /// <summary>AppData\Roaming\Vrcosc-MagicChatbox (or profile-specific variant).</summary>
    string DataPath { get; }

    /// <summary>LocalAppData\Vrcosc-MagicChatbox\logs (matches NLog.config).</summary>
    string LogPath { get; }

    /// <summary>Default VRChat installation directory (Steam).</summary>
    string VrcPath { get; }

    /// <summary>
    /// Switches DataPath to a profile-specific folder.
    /// Must be called at startup before any service reads DataPath.
    /// </summary>
    void SetCustomProfile(int profileNumber);
}
