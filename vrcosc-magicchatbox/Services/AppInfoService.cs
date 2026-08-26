using System;
using System.Reflection;
using vrcosc_magicchatbox.Classes.DataAndSecurity;

namespace vrcosc_magicchatbox.Services;

public class AppInfoService : IAppInfoService
{
    public string GetApplicationVersion()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            string? versionString = assembly.GetName().Version?.ToString();
            if (versionString == null)
            {
                Logging.WriteInfo("Error reading version: assembly version is unavailable.");
                return "69.420.666";
            }
            return new ViewModels.Models.Version(versionString).VersionNumber;
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"Error reading version: {ex.Message}");
            return "69.420.666";
        }
    }
}
