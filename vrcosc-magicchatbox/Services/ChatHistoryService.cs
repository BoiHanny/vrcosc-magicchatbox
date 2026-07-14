using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.IO;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
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
    private readonly IUiDispatcher _dispatcher;

    public ChatHistoryService(
        IEnvironmentService env,
        ChatStatusDisplayState chatStatus,
        IAppHistoryService appHistory,
        IUiDispatcher dispatcher)
    {
        _env = env;
        _chatStatus = chatStatus;
        _appHistory = appHistory;
        _dispatcher = dispatcher;
    }

    public void LoadChatHistory()
    {
        try
        {
            if (_chatStatus.LastMessages == null)
                _dispatcher.BeginInvoke(() => _chatStatus.LastMessages ??= new());

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
                    foreach (var item in loadedMessages)
                    {
                        item.CanLiveEdit = false;
                    }

                    _dispatcher.BeginInvoke(() => _chatStatus.LastMessages = loadedMessages);
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
                _dispatcher.BeginInvoke(() => _chatStatus.LastMessages = new());
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

            if (_appHistory.CreateIfMissing(_env.DataPath) != true)
            {
                return;
            }

            // Persist empty collections explicitly. Previously this method short-circuited
            // when the collection was empty, which meant clearing chat history left the
            // stale on-disk file behind and it resurrected on next launch.
            string json = JsonConvert.SerializeObject(_chatStatus.LastMessages, Formatting.Indented);

            if (json == null)
            {
                return;
            }

            string filePath = Path.Combine(_env.DataPath, "LastMessages.json");
            AtomicFileWriter.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
    }
}
