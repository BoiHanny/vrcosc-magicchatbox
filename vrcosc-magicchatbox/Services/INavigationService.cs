namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Safe URL/process launcher. Validates schemes and domains before launching.
/// Replaces raw Process.Start calls with validated navigation.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Opens a URL in the default browser after validating the scheme is https.
    /// </summary>
    bool OpenUrl(string url);

    /// <summary>
    /// Opens a URL if it matches the allowed domain whitelist.
    /// </summary>
    bool OpenUrl(string url, string[] allowedDomains);

    /// <summary>
    /// Opens a local folder in the default file manager (Explorer).
    /// Validates the path exists and is a directory.
    /// </summary>
    bool OpenFolder(string folderPath);
}
