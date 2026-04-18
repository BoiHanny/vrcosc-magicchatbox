using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.IO;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Loads and saves the application process history (ScannedApps) to disk.
/// </summary>
public sealed class AppHistoryService : IAppHistoryService
{
    private readonly IEnvironmentService _env;
    private readonly WindowActivityDisplayState _windowActivity;
    private readonly IUiDispatcher _dispatcher;

    public AppHistoryService(IEnvironmentService env, WindowActivityDisplayState windowActivity, IUiDispatcher dispatcher)
    {
        _env = env;
        _windowActivity = windowActivity;
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Loads the app history from disk, falling back to an empty collection on failure.
    /// </summary>
    public void LoadAppHistory()
    {
        try
        {
            string appHistoryPath = Path.Combine(_env.DataPath, "AppHistory.json");
            string appHistoryLegacy = Path.Combine(_env.DataPath, "AppHistory.xml");
            string appHistoryFile = File.Exists(appHistoryPath) ? appHistoryPath
                                  : File.Exists(appHistoryLegacy) ? appHistoryLegacy : null;

            if (appHistoryFile != null)
            {
                string json = File.ReadAllText(appHistoryFile);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var scannedApps = JsonConvert.DeserializeObject<ObservableCollection<ProcessInfo>>(json);
                    _dispatcher.Invoke(() =>
                    {
                        _windowActivity.ScannedApps = scannedApps ?? new();
                    });
                }
            }
            else
            {
                Logging.WriteInfo("AppHistory history has never been created, not a problem :P");
                _dispatcher.Invoke(() =>
                {
                    _windowActivity.ScannedApps = new();
                });
            }
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            _dispatcher.Invoke(() =>
            {
                _windowActivity.ScannedApps = new();
            });
        }
    }

    /// <summary>
    /// Serializes the current app history to disk as JSON.
    /// </summary>
    public void SaveAppHistory()
    {
        try
        {
            if (CreateIfMissing(_env.DataPath) == true)
            {
                string json = JsonConvert.SerializeObject(_windowActivity.ScannedApps);

                if (string.IsNullOrEmpty(json))
                {
                    return;
                }

                File.WriteAllText(Path.Combine(_env.DataPath, "AppHistory.json"), json);
            }
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
    }

    /// <summary>
    /// Ensures the specified directory exists, creating it if necessary.
    /// Returns false on IO failure.
    /// </summary>
    public bool CreateIfMissing(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                DirectoryInfo di = Directory.CreateDirectory(path);
                return true;
            }
            return true;
        }
        catch (IOException ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return false;
        }
    }
}
