using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Classes.DataAndSecurity;

public class ChatStateManager
{
    private readonly ChatSettings _chatSettings;
    private readonly AppSettings _appSettings;
    private readonly ChatStatusDisplayState _chatStatus;
    private readonly OscDisplayState _oscDisplay;
    private readonly EmojiService _emojis;
    private readonly IUiDispatcher _dispatcher;

    public ChatStateManager(
        ChatSettings chatSettings,
        AppSettings appSettings,
        ChatStatusDisplayState chatStatus,
        OscDisplayState oscDisplay,
        EmojiService emojis,
        IUiDispatcher dispatcher)
    {
        _chatSettings = chatSettings;
        _appSettings = appSettings;
        _chatStatus = chatStatus;
        _oscDisplay = oscDisplay;
        _emojis = emojis;
        _dispatcher = dispatcher;
    }

    public void ClearChat(ChatItem? lastSendChat = null)
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

    public bool CreateChat(bool createItem, string? messageText = null)
    {
        try
        {
            string sourceMessage = messageText ?? _chatStatus.NewChattingTxt;
            string completeMsg = _chatSettings.PrefixChat == true
                ? _emojis.GetNextEmoji(true) + " " + sourceMessage
                : sourceMessage;

            if (completeMsg.Length == 0 || completeMsg.Length > Core.Constants.OscMaxMessageLength)
                return false;

            _chatStatus.ScanPauseCountDown = _appSettings.ScanPauseTimeout;
            _chatStatus.ScanPause = true;
            _oscDisplay.OscToSent = completeMsg;
            _oscDisplay.OscMsgCount = completeMsg.Length;
            _oscDisplay.OscMsgCountUI = $"{completeMsg.Length}/{Core.Constants.OscMaxMessageLength}";

            if (createItem)
            {
                AddChatHistoryItem(sourceMessage);
                if (messageText is null)
                    _chatStatus.NewChattingTxt = string.Empty;
            }

            return true;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return false;
        }
    }

    private void AddChatHistoryItem(string messageText)
    {
        int randomId = Random.Shared.Next(Core.Constants.StatusRandomIdMin, Core.Constants.StatusRandomIdMax);

        var newChatItem = new ChatItem(_chatStatus)
        {
            Msg = messageText,
            MainMsg = messageText,
            CreationDate = DateTime.Now,
            ID = randomId,
            IsRunning = true,
            CanLiveEdit = _chatSettings.ChatLiveEdit,
            LiveEditButtonTxt = EditLabel(_chatSettings)
        };

        void Apply()
        {
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

            _chatStatus.LastMessages.Add(newChatItem);

            if (_chatStatus.LastMessages.Count > 5)
                _chatStatus.LastMessages.RemoveAt(0);

            FadeOlderMessages(_chatStatus.LastMessages);
        }

        if (_dispatcher.CheckAccess())
            Apply();
        else
            _dispatcher.Invoke(Apply);
    }

    public static string EditLabel(ChatSettings settings)
        => settings.RealTimeChatEdit ? "Live edit" : "Edit";

    public static void FadeOlderMessages(IList<ChatItem> messages)
    {
        const double step = 0.16;
        const double floor = 0.36;

        double opacity = 1;
        for (int i = messages.Count - 1; i >= 0; i--)
        {
            messages[i].Opacity = opacity.ToString("F2", CultureInfo.InvariantCulture);
            opacity = Math.Max(floor, opacity - step);
        }
    }
}
