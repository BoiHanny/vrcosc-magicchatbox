using System;
using System.Reflection;
using vrcosc_magicchatbox.Classes.DataAndSecurity;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Reads application metadata (version) from the executing assembly.
/// </summary>
public class AppInfoService : IAppInfoService
{
    public string GetApplicationVersion()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            string versionString = assembly.GetName().Version.ToString();
            return new ViewModels.Models.Version(versionString).VersionNumber;
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"Error reading version: {ex.Message}");
            return "69.420.666";
        }
    }
}
