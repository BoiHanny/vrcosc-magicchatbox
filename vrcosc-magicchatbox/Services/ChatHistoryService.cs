using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.IO;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Loads and saves chat message history (LastMessages) to disk.
/// </summary>
public sealed class ChatHistoryService : IChatHistoryService
{
    private readonly IEnvironmentService _env;
    private readonly ChatStatusDisplayState _chatStatus;
    private readonly IAppHistoryService _appHistory;

    public ChatHistoryService(IEnvironmentService env, ChatStatusDisplayState chatStatus, IAppHistoryService appHistory)
    {
        _env = env;
        _chatStatus = chatStatus;
        _appHistory = appHistory;
    }

    public void LoadChatHistory()
    {
        try
        {
            if (_chatStatus.LastMessages == null)
            {
                _chatStatus.LastMessages = new();
            }

            if (File.Exists(Path.Combine(_env.DataPath, "LastMessages.json"))
                || File.Exists(Path.Combine(_env.DataPath, "LastMessages.xml")))
            {
                string lastMsgPath = File.Exists(Path.Combine(_env.DataPath, "LastMessages.json"))
                    ? Path.Combine(_env.DataPath, "LastMessages.json")
                    : Path.Combine(_env.DataPath, "LastMessages.xml");
                string json = File.ReadAllText(lastMsgPath);

                if (string.IsNullOrWhiteSpace(json) || json.Trim().Equals("null", StringComparison.OrdinalIgnoreCase))
                {
                    Logging.WriteInfo("LastMessages history is null or empty, not problem :P");
                    return;
                }

                var loadedMessages = JsonConvert.DeserializeObject<ObservableCollection<ChatItem>>(json);

                if (loadedMessages != null)
                {
                    _chatStatus.LastMessages = loadedMessages;
                    foreach (var item in _chatStatus.LastMessages)
                    {
                        item.CanLiveEdit = false;
                    }
                }
            }
            else
            {
                Logging.WriteInfo("LastMessages history has never been created, not problem :P");
            }
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);

            if (_chatStatus?.LastMessages == null)
            {
                _chatStatus.LastMessages = new();
            }
        }
    }

    public void SaveChatHistory()
    {
        try
        {
            if (_chatStatus?.LastMessages == null)
            {
                return;
            }

            if (_appHistory.CreateIfMissing(_env.DataPath) == true)
            {
                if (_chatStatus.LastMessages.Count == 0)
                {
                    return;
                }

                string json = JsonConvert.SerializeObject(_chatStatus.LastMessages, Formatting.Indented);

                if (string.IsNullOrEmpty(json))
                {
                    return;
                }

                string filePath = Path.Combine(_env.DataPath, "LastMessages.json");

                if (string.IsNullOrEmpty(filePath))
                {
                    return;
                }

                File.WriteAllText(filePath, json);
            }
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
    }
}
