using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Classes.DataAndSecurity;

/// <summary>
/// Manages chat state: creating chat messages, clearing chat, and maintaining message history.
/// Registered as DI singleton.
/// </summary>
public class ChatStateManager
{
    private readonly ChatSettings _chatSettings;
    private readonly AppSettings _appSettings;
    private readonly ChatStatusDisplayState _chatStatus;
    private readonly OscDisplayState _oscDisplay;
    private readonly EmojiService _emojis;

    public ChatStateManager(
        ChatSettings chatSettings,
        AppSettings appSettings,
        ChatStatusDisplayState chatStatus,
        OscDisplayState oscDisplay,
        EmojiService emojis)
    {
        _chatSettings = chatSettings;
        _appSettings = appSettings;
        _chatStatus = chatStatus;
        _oscDisplay = oscDisplay;
        _emojis = emojis;
    }

    /// <summary>
    /// Clears the active chat and resets related ViewModel state.
    /// </summary>
    public void ClearChat(ChatItem lastSendChat = null)
    {
        _chatStatus.ScanPause = false;
        _oscDisplay.OscToSent = string.Empty;
        _oscDisplay.OscMsgCount = 0;
        _oscDisplay.OscMsgCountUI = $"0/{Core.Constants.OscMaxMessageLength}";
        if (lastSendChat != null)
        {
            lastSendChat.CanLiveEdit = false;
            lastSendChat.CanLiveEditRun = false;
            lastSendChat.MsgReplace = string.Empty;
            lastSendChat.IsRunning = false;
        }
    }

    /// <summary>
    /// Creates a chat message from the current NewChattingTxt, applies prefix if enabled,
    /// sets scan-pause state, and optionally adds a ChatItem to the message history.
    /// </summary>
    public void CreateChat(bool createItem)
    {
        try
        {
            string completeMsg = _chatSettings.PrefixChat == true
                ? _emojis.GetNextEmoji(true) + " " + _chatStatus.NewChattingTxt
                : _chatStatus.NewChattingTxt;

            if (completeMsg.Length == 0 || completeMsg.Length > Core.Constants.OscMaxMessageLength)
                return;

            _chatStatus.ScanPauseCountDown = _appSettings.ScanPauseTimeout;
            _chatStatus.ScanPause = true;
            _oscDisplay.OscToSent = completeMsg;
            _oscDisplay.OscMsgCount = completeMsg.Length;
            _oscDisplay.OscMsgCountUI = $"{completeMsg.Length}/{Core.Constants.OscMaxMessageLength}";

            if (createItem)
            {
                AddChatHistoryItem(_chatStatus.NewChattingTxt);
                _chatStatus.NewChattingTxt = string.Empty;
            }
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
    }

    private void AddChatHistoryItem(string messageText)
    {
        var random = new Random();
        int randomId = random.Next(Core.Constants.StatusRandomIdMin, Core.Constants.StatusRandomIdMax);

        if (_chatSettings.ChatLiveEdit)
        {
            foreach (var item in _chatStatus.LastMessages)
            {
                item.CanLiveEdit = false;
                item.CanLiveEditRun = false;
                item.MsgReplace = string.Empty;
                item.IsRunning = false;
            }
        }

        var newChatItem = new ChatItem(_chatStatus)
        {
            Msg = messageText,
            MainMsg = messageText,
            CreationDate = DateTime.Now,
            ID = randomId,
            IsRunning = true,
            CanLiveEdit = _chatSettings.ChatLiveEdit
        };
        _chatStatus.LastMessages.Add(newChatItem);

        if (_chatStatus.LastMessages.Count > 5)
            _chatStatus.LastMessages.RemoveAt(0);

        // Assign fading opacity to history items
        double opacity = 1;
        foreach (var item in _chatStatus.LastMessages.AsEnumerable().Reverse())
        {
            opacity -= 0.18;
            item.Opacity = opacity.ToString("F1", CultureInfo.InvariantCulture);
        }

        // Force collection refresh for UI binding
        var currentList = new ObservableCollection<ChatItem>(_chatStatus.LastMessages);
        _chatStatus.LastMessages.Clear();
        foreach (var item in currentList)
            _chatStatus.LastMessages.Add(item);
    }
}
