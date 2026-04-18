using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Loads, saves, and initializes the status item list for the chatbox status display.
/// </summary>
public sealed class StatusListService : IStatusListService
{
    private readonly ChatStatusDisplayState _chatStatus;
    private readonly IAppState _appState;
    private readonly IEnvironmentService _env;
    private readonly IUiDispatcher _dispatcher;

    public StatusListService(ChatStatusDisplayState chatStatus, IAppState appState, IEnvironmentService env, IUiDispatcher dispatcher)
    {
        _chatStatus = chatStatus;
        _appState = appState;
        _env = env;
        _dispatcher = dispatcher;
    }

    public void LoadStatusList()
    {
        try
        {
            string statusListPath = Path.Combine(_env.DataPath, "StatusList.json");
            string statusListLegacy = Path.Combine(_env.DataPath, "StatusList.xml");
            string statusListFile = File.Exists(statusListPath) ? statusListPath
                                  : File.Exists(statusListLegacy) ? statusListLegacy : null;
            if (statusListFile != null)
            {
                string json = File.ReadAllText(statusListFile);
                UpdateStatusListFromJson(json);
            }
            else
            {
                InitializeStatusListWithDefaults();
            }
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            EnsureStatusListInitialized();
        }
    }

    public void SaveStatusList()
    {
        try
        {
            var dataPath = _env.DataPath;
            if (!string.IsNullOrEmpty(dataPath))
            {
                Directory.CreateDirectory(dataPath);
                var json = JsonConvert.SerializeObject(_chatStatus.StatusList);
                File.WriteAllText(Path.Combine(dataPath, "StatusList.json"), json);
            }
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
    }

    private void SetStatusList(ObservableCollection<StatusItem> statusList, bool checkSpecialMessages)
    {
        void Apply()
        {
            _chatStatus.StatusList = statusList;
            if (checkSpecialMessages)
            {
                CheckForSpecialMessages(statusList);
            }
        }

        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(Apply);
        }
        else
        {
            Apply();
        }
    }

    private void UpdateStatusListFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim().Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            Logging.WriteInfo("StatusList history is empty or null.");
            SetStatusList(new ObservableCollection<StatusItem>(), checkSpecialMessages: false);
            return;
        }

        try
        {
            var statusList = JsonConvert.DeserializeObject<ObservableCollection<StatusItem>>(json);
            if (statusList != null)
            {
                SetStatusList(statusList, checkSpecialMessages: true);
            }
        }
        catch (JsonException jsonEx)
        {
            Logging.WriteException(jsonEx, MSGBox: true);
            SetStatusList(new ObservableCollection<StatusItem>(), checkSpecialMessages: false);
        }
    }

    private void CheckForSpecialMessages(ObservableCollection<StatusItem> statusList)
    {
        if (statusList.Any(x => x.msg.Equals("boihanny", StringComparison.OrdinalIgnoreCase) ||
                                x.msg.Equals("sr4 series", StringComparison.OrdinalIgnoreCase)))
        {
            _appState.Egg_Dev = true;
        }
        if (statusList.Any(x => x.msg.Equals("bussyboys", StringComparison.OrdinalIgnoreCase)))
        {
            _appState.BussyBoysMode = true;
        }
    }

    private void InitializeStatusListWithDefaults()
    {
        var defaults = new ObservableCollection<StatusItem>
        {
            new StatusItem { CreationDate = DateTime.Now, IsActive = true, msg = "Enjoy 💖", MSGID = GenerateRandomId() },
            new StatusItem { CreationDate = DateTime.Now, IsActive = false, msg = "Below you can create your own status", MSGID = GenerateRandomId() },
            new StatusItem { CreationDate = DateTime.Now, IsActive = false, msg = "Activate it by clicking the power icon", MSGID = GenerateRandomId() }
        };
        SetStatusList(defaults, checkSpecialMessages: false);
        SaveStatusList();
    }

    private void EnsureStatusListInitialized()
    {
        if (_chatStatus.StatusList == null)
        {
            SetStatusList(new ObservableCollection<StatusItem>(), checkSpecialMessages: false);
        }
    }

    private static int GenerateRandomId()
    {
        Random random = new Random();
        return random.Next(Core.Constants.StatusRandomIdMin, Core.Constants.StatusRandomIdMax);
    }
}
