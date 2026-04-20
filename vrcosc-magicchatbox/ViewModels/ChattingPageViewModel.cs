using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Linq;
using System.Windows;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.ViewModels
{
    /// <summary>
    /// Page-specific ViewModel for the Chatting page. Owns all chat-input,
    /// IntelliChat, Whisper commands, and chat send/stop/edit/resend logic.
    /// Uses Lazy&lt;IModuleHost&gt; for lazily-created module references.
    /// </summary>
    public partial class ChattingPageViewModel : ObservableObject
    {
        private const string BoxColorNormal = "#FF6B5F98";
        private const string BoxColorWarning = "#FFFF9393";

        private readonly ChatStatusDisplayState _chatStatus;
        private readonly Lazy<IModuleHost> _moduleHost;
        private readonly IAppState _appState;
        private readonly Lazy<IChatHistoryService> _chatHistorySvc;
        private readonly Lazy<IOscSender> _oscSender;
        private readonly Lazy<IAudioService> _audioSvc;

        private IntelliChatModule? IntelliChat => _moduleHost.Value.IntelliChat;
        private WhisperModule? Whisper => _moduleHost.Value.Whisper;

        // Lazily resolved services (circular dependency at construction time)
        private readonly Lazy<ScanLoopService> _scanLoop;
        private ScanLoopService ScanLoop => _scanLoop.Value;

        private readonly Lazy<OSCController> _osc;
        private OSCController Osc => _osc.Value;

        private readonly Lazy<ITtsPlaybackService> _ttsPlayback;
        private ITtsPlaybackService TtsPlayback => _ttsPlayback.Value;

        private readonly ChatSettings CS;
        private readonly TtsSettings TTS;

        public ChatStatusDisplayState ChatStatus { get; }
        public ChatSettings ChatSettings { get; }
        public IModuleHost Modules => _moduleHost.Value;

        /// <summary>
        /// Event raised when UI should scroll the recent chat to the end.
        /// Code-behind subscribes to handle the ScrollViewer interaction.
        /// </summary>
        public event Action? ScrollToEndRequested;

        /// <summary>
        /// Initializes the chatting page ViewModel, wiring together chat status, IntelliChat,
        /// Whisper, TTS, and command services needed for the chat input UI.
        /// </summary>
        public ChattingPageViewModel(
            ChatStatusDisplayState chatStatus,
            IAppState appState,
            Lazy<IModuleHost> moduleHost,
            ISettingsProvider<ChatSettings> chatSettingsProvider,
            ISettingsProvider<TtsSettings> ttsSettingsProvider,
            Lazy<ScanLoopService> scanLoop,
            Lazy<OSCController> osc,
            Lazy<IChatHistoryService> chatHistorySvc,
            Lazy<IAudioService> audioSvc,
            Lazy<IOscSender> oscSender,
            Lazy<ITtsPlaybackService> ttsPlayback)
        {
            _chatStatus = chatStatus;
            _appState = appState;
            _moduleHost = moduleHost;
            CS = chatSettingsProvider.Value;
            TTS = ttsSettingsProvider.Value;
            ChatStatus = chatStatus;
            ChatSettings = chatSettingsProvider.Value;
            _scanLoop = scanLoop;
            _osc = osc;
            _chatHistorySvc = chatHistorySvc;
            _audioSvc = audioSvc;
            _oscSender = oscSender;
            _ttsPlayback = ttsPlayback;
        }

        [RelayCommand]
        private void SpellCheck() => IntelliChat?.PerformSpellingAndGrammarCheckAsync(_chatStatus.NewChattingTxt);

        [RelayCommand]
        private void Beautify() => IntelliChat?.PerformBeautifySentenceAsync(_chatStatus.NewChattingTxt);

        [RelayCommand]
        private void Translate() => IntelliChat?.PerformLanguageTranslationAsync(_chatStatus.NewChattingTxt);

        [RelayCommand]
        private void AcceptIntelliChat() => IntelliChat?.AcceptIntelliChatSuggestion();

        [RelayCommand]
        private void RejectIntelliChat() => IntelliChat?.RejectIntelliChatSuggestion();

        [RelayCommand]
        private void CloseIntelliError() => IntelliChat?.CloseIntelliErrorPanel();

        [RelayCommand]
        private void ConvoStarter() => IntelliChat?.GenerateConversationStarterAsync();

        [RelayCommand]
        private void ShortenChat() => IntelliChat?.ShortenTextAsync(_chatStatus.NewChattingTxt);

        [RelayCommand]
        private void PredictNextWord() => IntelliChat?.GenerateCompletionOrPredictionAsync(_chatStatus.NewChattingTxt, true);

        [RelayCommand]
        private void StartRecording() => Whisper?.StartRecording();

        [RelayCommand]
        private void StopRecording() => Whisper?.StopRecording();

        [RelayCommand]
        private void PasteChat()
        {
            try
            {
                string? clipboardText = Clipboard.GetText();
                if (!string.IsNullOrEmpty(clipboardText))
                {
                    string newText = _chatStatus.NewChattingTxt + clipboardText;
                    if (newText.Length <= Core.Constants.MaxChatMessageLength)
                        _chatStatus.NewChattingTxt = newText;
                    else
                        _chatStatus.ChatFeedbackTxt = $"Paste would exceed {Core.Constants.MaxChatMessageLength} char limit";
                }
            }
            catch (Exception ex)
            {
                Logging.WriteInfo("Clipboard access failed: " + ex.Message);
                _chatStatus.ChatFeedbackTxt = "Failed to access clipboard";
            }
        }

        [RelayCommand]
        private void ClearChatInput() => _chatStatus.NewChattingTxt = string.Empty;

        [RelayCommand]
        private void AcceptIntelliChatAndSend()
        {
            IntelliChat?.AcceptIntelliChatSuggestion();
            SendChat();
        }

        /// <summary>
        /// Sends the current chat text to VRChat OSC.
        /// </summary>
        [RelayCommand]
        public void SendChat()
        {
            string chat = _chatStatus.NewChattingTxt;
            if (string.IsNullOrWhiteSpace(chat) || chat.Length > Core.Constants.MaxChatMessageLength || !_appState.MasterSwitch)
                return;

            foreach (ChatItem item in _chatStatus.LastMessages)
            {
                item.CanLiveEdit = false;
                item.CanLiveEditRun = false;
                item.MsgReplace = string.Empty;
                item.IsRunning = false;
            }

            Osc.CreateChat(true);
            int smalldelay = CS.ChatAddSmallDelay ? (int)(CS.ChatAddSmallDelayTIME * 1000) : 0;
            _oscSender.Value.SendOSCMessage(CS.ChatFX, smalldelay);
            _chatHistorySvc.Value.SaveChatHistory();

            if (TTS.TtsTikTokEnabled)
            {
                if (_audioSvc.Value.PopulateOutputDevices())
                {
                    _chatStatus.ChatFeedbackTxt = "Requesting TTS...";
                    TtsPlayback.PlayTtsAsync(chat);
                }
                else
                {
                    _chatStatus.ChatFeedbackTxt = "Error setting output device.";
                }
            }

            _ = ScanLoop.Scantick();
            ScrollToEndRequested?.Invoke();
        }

        [RelayCommand]
        public void StopChat()
        {
            ChatItem? running = _chatStatus.LastMessages.FirstOrDefault(x => x.IsRunning);
            Osc.ClearChat(running);
            int smalldelay = CS.ChatAddSmallDelay ? (int)(CS.ChatAddSmallDelayTIME * 1000) : 0;
            _oscSender.Value.SendOSCMessage(false, smalldelay);
            _ = ScanLoop.Scantick();
            TtsPlayback.CancelAllTts();
        }

        [RelayCommand]
        public void ClearChat()
        {
            _chatStatus.LastMessages.Clear();
            _chatHistorySvc.Value.SaveChatHistory();
            StopChat();
        }

        [RelayCommand]
        public void SendAgain(ChatItem? item)
        {
            if (item == null) return;
            try
            {
                if (!_appState.MasterSwitch)
                {
                    _chatStatus.ChatFeedbackTxt = "Sent to VRChat is off";
                    return;
                }

                foreach (ChatItem ci in _chatStatus.LastMessages)
                {
                    ci.CanLiveEdit = false;
                    ci.CanLiveEditRun = false;
                    ci.MsgReplace = string.Empty;
                    ci.IsRunning = false;
                }

                item.CanLiveEdit = CS.ChatLiveEdit;
                item.MainMsg = item.Msg;
                item.LiveEditButtonTxt = "Sending...";
                item.IsRunning = true;

                string savedtxt = _chatStatus.NewChattingTxt;
                _chatStatus.NewChattingTxt = item.Msg;
                Osc.CreateChat(false);
                int smalldelay = CS.ChatAddSmallDelay ? (int)(CS.ChatAddSmallDelayTIME * 1000) : 0;
                _oscSender.Value.SendOSCMessage(CS.ChatFX && CS.ChatSendAgainFX, smalldelay);
                _chatStatus.NewChattingTxt = savedtxt;

                if (TTS.TtsTikTokEnabled && TTS.TtsOnResendChat)
                {
                    if (_audioSvc.Value.PopulateOutputDevices())
                    {
                        _chatStatus.ChatFeedbackTxt = "Requesting TTS...";
                        TtsPlayback.PlayTtsAsync(item.Msg, true);
                    }
                    else
                    {
                        _chatStatus.ChatFeedbackTxt = "Error setting output device.";
                    }
                }
                else
                {
                    _chatStatus.ChatFeedbackTxt = "Message sent again";
                }
                _ = ScanLoop.Scantick();
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
                _chatStatus.ChatFeedbackTxt = "Failed to resend message";
            }
        }

        [RelayCommand]
        public void CancelEdit(ChatItem? item)
        {
            try
            {
                ChatItem? running = _chatStatus.LastMessages.FirstOrDefault(x => x.IsRunning);
                if (running != null && !string.IsNullOrEmpty(running.MainMsg))
                {
                    running.CancelLiveEdit = true;
                    running.CanLiveEditRun = false;
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        /// <summary>
        /// Updates the chat box character count and color state.
        /// Called from code-behind TextChanged handler.
        /// </summary>
        public void UpdateChatBoxCount(int count)
        {
            _chatStatus.ChatBoxCount = $"{count}/140";
            if (count > 140)
            {
                int overmax = count - 140;
                _chatStatus.ChatBoxColor = BoxColorWarning;
                _chatStatus.ChatTopBarTxt = $"You're soaring past the 140 char limit by {overmax}.";
            }
            else if (count == 0)
            {
                _chatStatus.ChatBoxColor = BoxColorNormal;
                _chatStatus.ChatTopBarTxt = string.Empty;
            }
            else
            {
                _chatStatus.ChatBoxColor = BoxColorNormal;
                _chatStatus.ChatTopBarTxt = string.Empty;
            }

            _oscSender.Value.SendTypingIndicatorAsync();
        }

        #region Whisper Transcription Handling

        /// <summary>
        /// Handles transcription text from WhisperModule — appends to chat input,
        /// trims to 140 chars on word boundary.
        /// </summary>
        public void OnTranscriptionReceived(string newTranscription)
        {
            string current = _chatStatus.NewChattingTxt + " " + newTranscription;
            _chatStatus.NewChattingTxt = TrimToLastMaxCharacters(current, 140);
        }

        /// <summary>
        /// Handles WhisperModule SentChatMessage event — sends the current chat.
        /// Must be called on UI thread.
        /// </summary>
        public void OnWhisperSentChat() => SendChat();

        private static string TrimToLastMaxCharacters(string text, int maxCharacters)
        {
            if (text.Length <= maxCharacters) return text;

            int firstSpaceIndex = text.IndexOf(' ', text.Length - maxCharacters);
            if (firstSpaceIndex == -1)
                return text.Substring(text.Length - maxCharacters);

            return text.Substring(firstSpaceIndex).Trim();
        }

        #endregion

        #region Chat edit state machine

        /// <summary>
        /// Begins editing a chat item. Sets MsgReplace and Opacity.
        /// Code-behind handles the focus/caret UI concern after this.
        /// </summary>
        public void BeginChatEdit(ChatItem item)
        {
            item.MsgReplace = item.Msg.EndsWith(" ") ? item.Msg : item.Msg + " ";
            item.Opacity_backup = item.Opacity;
            item.Opacity = "1";
        }

        /// <summary>
        /// Confirms or cancels a chat edit. Returns true if focus should return to main input.
        /// </summary>
        public bool ConfirmChatEdit(ChatItem item)
        {
            ChatItem? running = _chatStatus.LastMessages.FirstOrDefault(x => x.IsRunning);
            if (item != null && running != null)
            {
                if (running.Msg != item.MsgReplace && !running.CancelLiveEdit)
                {
                    running.MainMsg = item.MsgReplace;
                    running.Msg = item.MsgReplace;
                    running.CanLiveEditRun = false;
                }
                else if (running.CancelLiveEdit)
                {
                    if (CS.RealTimeChatEdit)
                        running.Msg = running.MainMsg;
                    running.CancelLiveEdit = false;
                }
            }
            if (item != null)
                item.Opacity = item.Opacity_backup;
            return true;
        }

        /// <summary>
        /// Handles Enter key during chat edit. Returns true if focus should return to main input.
        /// </summary>
        public bool HandleEditEnter(string editText)
        {
            ChatItem? running = _chatStatus.LastMessages.FirstOrDefault(x => x.IsRunning);
            if (running == null) return false;

            if (CS.RealTimeChatEdit || running.Msg != editText)
            {
                running.MainMsg = editText;
                if (!CS.RealTimeChatEdit) running.Msg = editText;
                running.CanLiveEditRun = false;
            }
            return true;
        }

        /// <summary>
        /// Handles Escape key during chat edit.
        /// </summary>
        public void HandleEditEscape()
        {
            ChatItem? running = _chatStatus.LastMessages.FirstOrDefault(x => x.IsRunning);
            if (running != null && !string.IsNullOrEmpty(running.MainMsg))
            {
                running.CancelLiveEdit = true;
                running.CanLiveEditRun = false;
            }
        }

        /// <summary>
        /// Handles real-time text change during chat edit.
        /// </summary>
        public void HandleEditTextChanged(string newText)
        {
            if (!CS.RealTimeChatEdit) return;
            ChatItem? running = _chatStatus.LastMessages.FirstOrDefault(x => x.IsRunning);
            if (running != null && running.Msg != newText)
                running.Msg = newText;
        }

        #endregion
    }
}
