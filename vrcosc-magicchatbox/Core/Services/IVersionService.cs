using System.Threading.Tasks;

namespace vrcosc_magicchatbox.Core.Services;

/// <summary>
/// Application version checking and update logic.
/// </summary>
public interface IVersionService
{
    string GetApplicationVersion();

    /// <summary>
    /// Checks GitHub for the latest release and waits for the result.
    /// When <paramref name="checkAgain"/> is true, forces a fresh network request
    /// even if a check was already performed this session.
    /// </summary>
    Task CheckForUpdateAndWait(bool checkAgain = false);
}
