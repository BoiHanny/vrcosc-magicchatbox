using System;
using System.IO;
using System.IO.Compression;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.State;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Enforces user bans by archiving local data and terminating the application.
/// </summary>
public class BanEnforcementService : IBanEnforcementService
{
    private readonly IAppState _appState;
    private readonly IEnvironmentService _env;

    public BanEnforcementService(IAppState appState, IEnvironmentService env)
    {
        _appState = appState;
        _env = env;
    }

    public void ProcessBan(string bannedUserID, string reason)
    {
        string dataPath = _env.DataPath;
        string backupFolderPath = Path.Combine(dataPath, "backup");
        string zipTempPath = Path.Combine(Path.GetTempPath(), "MagicChatboxBannedData.zip");

        try
        {
            if (!File.Exists(zipTempPath))
            {
                if (Directory.Exists(backupFolderPath))
                    Directory.Delete(backupFolderPath, recursive: true);

                string[] filesInDataPath = Directory.GetFiles(dataPath);
                if (filesInDataPath.Length == 0)
                    return;

                ZipFilesInDirectory(dataPath, zipTempPath);

                if (Directory.Exists(dataPath))
                    Directory.Delete(dataPath, recursive: true);
            }

            _appState.MainWindowBlurEffect = 10;

            Logging.WriteException(
                new Exception("You have been banned from using MagicChatbox.\n\n" +
                   $"Reason: {reason}\n\n" +
                              "There is no need to appeal this ban; we have a zero-tolerance policy."),
                MSGBox: true,
                exitapp: false,
                autoclose: true);

            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: true, exitapp: false);
        }
    }

    private static void ZipFilesInDirectory(string sourceDirectory, string zipPath)
    {
        using var zipToCreate = new FileStream(zipPath, FileMode.Create);
        using var archive = new ZipArchive(zipToCreate, ZipArchiveMode.Create);
        foreach (string filePath in Directory.GetFiles(sourceDirectory))
        {
            string entryName = Path.GetFileName(filePath);
            archive.CreateEntryFromFile(filePath, entryName);
        }
    }
}
