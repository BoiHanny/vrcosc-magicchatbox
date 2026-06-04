using CoreOSC;
using System;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Instance-based OSC sender service. Replaces the static OSCSender class.
/// Sends chatbox messages, typing indicators, and avatar parameters to VRChat via OSC.
/// </summary>
public sealed class OscSenderService : IOscSender, IDisposable
{
    private const string CHATBOX_INPUT = "/chatbox/input";
    private const string CHATBOX_TYPING = "/chatbox/typing";
    private const string INPUT_VOICE = "/input/Voice";
    private const int TYPING_DURATION = 2000;
    private static readonly TimeSpan DuplicateKeepAliveInterval = TimeSpan.FromSeconds(12);

    private readonly OscSettings _oscSettings;
    private readonly AppSettings _appSettings;
    private readonly TtsSettings _ttsSettings;
    private readonly IAppState _appState;
    private readonly ChatStatusDisplayState _chatStatus;
    private readonly OscDisplayState _oscDisplay;
    private readonly TtsAudioDisplayState _ttsAudio;

    private UDPSender? _oscSender;
    private UDPSender? _secOscSender;
    private UDPSender? _thirdOscSender;
    private readonly object _senderLock = new();
    private readonly object _typingLock = new();

    private bool _lastChatboxHadContent;
    private System.Timers.Timer? _typingTimer;
    private bool _typingIndicatorActive;
    private long _typingIndicatorVersion;
    private string _lastSentMessageSignature = string.Empty;
    private DateTime _lastSentMessageUtc = DateTime.MinValue;

    public OscSenderService(
        ISettingsProvider<OscSettings> oscSettings,
        ISettingsProvider<AppSettings> appSettings,
        ISettingsProvider<TtsSettings> ttsSettings,
        IAppState appState,
        ChatStatusDisplayState chatStatus,
        OscDisplayState oscDisplay,
        TtsAudioDisplayState ttsAudio)
    {
        _oscSettings = oscSettings.Value;
        _appSettings = appSettings.Value;
        _ttsSettings = ttsSettings.Value;
        _appState = appState;
        _chatStatus = chatStatus;
        _oscDisplay = oscDisplay;
        _ttsAudio = ttsAudio;
    }

    private OscSettings OS => _oscSettings;
    private AppSettings AS => _appSettings;
    private TtsSettings TTS => _ttsSettings;

    public async Task<bool> SendOSCMessage(bool fx, int delay = 0, bool force = false)
    {
        if (!_appState.MasterSwitch || _oscDisplay.OscToSent.Length > Core.Constants.OscMaxMessageLength)
            return false;

        await DeactivateTypingIndicatorAsync();

        if (string.IsNullOrEmpty(_oscDisplay.OscToSent))
        {
            if (_lastChatboxHadContent)
            {
                _lastChatboxHadContent = false;
                await SentClearMessage(0);
                return true;
            }
            return false;
        }

        string messageSignature = CreateMessageSignature(fx);
        if (!force && ShouldSkipDuplicateMessage(messageSignature))
            return false;

        await SendMessageAsync(PrepareMessage(fx), delay);
        _lastChatboxHadContent = true;
        MarkMessageSent(messageSignature);
        return true;
    }

    public void SendOscParam(string address, float value)
    {
        if (!_appState.MasterSwitch) return;

        var msg = new OscMessage(address, value);
        PrimarySender.Send(msg);
        if (OS.SecOSC) SecondarySender.Send(msg);
        if (OS.ThirdOSC) TertiarySender.Send(msg);
    }

    public void SendOscParam(string address, int value)
    {
        if (!_appState.MasterSwitch) return;

        var msg = new OscMessage(address, value);
        PrimarySender.Send(msg);
        if (OS.SecOSC) SecondarySender.Send(msg);
        if (OS.ThirdOSC) TertiarySender.Send(msg);
    }

    public void SendOscParam(string address, bool value)
    {
        if (!_appState.MasterSwitch) return;

        var msg = new OscMessage(address, value ? 1 : 0);
        PrimarySender.Send(msg);
        if (OS.SecOSC) SecondarySender.Send(msg);
        if (OS.ThirdOSC) TertiarySender.Send(msg);
    }

    public void SendTypingIndicatorAsync()
    {
        if (!_appState.MasterSwitch)
        {
            StopTypingIndicator();
            return;
        }

        bool shouldActivate;
        long version = 0;
        lock (_typingLock)
        {
            EnsureTypingTimer();
            shouldActivate = !_typingIndicatorActive;
            _typingIndicatorActive = true;
            if (shouldActivate)
                version = ++_typingIndicatorVersion;
            _typingTimer!.Stop();
            _typingTimer.Start();
        }

        _chatStatus.TypingIndicator = true;

        if (shouldActivate)
            _ = SendTypingIndicatorStateAsync(true, version);
    }

    public void StopTypingIndicator() => _ = DeactivateTypingIndicatorAsync();

    public async Task SentClearMessage(int delay)
    {
        if (!_appState.MasterSwitch)
            return;

        await DeactivateTypingIndicatorAsync();

        var clearMessage = new OscMessage(CHATBOX_INPUT, "", true, false);
        await SendMessageAsync(clearMessage, delay);
        _lastChatboxHadContent = false;
        MarkMessageSent(string.Empty);
    }

    public async Task ToggleVoice(bool force = false)
    {
        if (!ShouldToggleVoice(force))
            return;

        await ToggleVoiceAsync();
    }

    public void Dispose()
    {
        StopTypingTimer();
        _typingTimer?.Dispose();

        lock (_senderLock)
        {
            _oscSender?.Close();
            _secOscSender?.Close();
            _thirdOscSender?.Close();
        }
    }

    #region Private helpers

    private OscMessage PrepareMessage(bool fx)
    {
        return new OscMessage(CHATBOX_INPUT, GetPreparedChatboxText(), true, fx);
    }

    private string GetPreparedChatboxText()
    {
        string blankEgg = "\u0003\u001f";
        string combinedText = _oscDisplay.OscToSent + blankEgg;

        if (combinedText.Length < 145 && _appState.Egg_Dev && AS.BlankEgg)
            return combinedText;

        return _oscDisplay.OscToSent;
    }

    private string CreateMessageSignature(bool fx)
    {
        return string.Join('\u001e', fx, GetPreparedChatboxText());
    }

    private bool ShouldSkipDuplicateMessage(string messageSignature)
    {
        lock (_senderLock)
        {
            return string.Equals(_lastSentMessageSignature, messageSignature, StringComparison.Ordinal)
                   && DateTime.UtcNow - _lastSentMessageUtc < DuplicateKeepAliveInterval;
        }
    }

    private void MarkMessageSent(string messageSignature)
    {
        lock (_senderLock)
        {
            _lastSentMessageSignature = messageSignature;
            _lastSentMessageUtc = DateTime.UtcNow;
        }
    }

    private async Task SendMessageAsync(OscMessage message, int delay)
    {
        if (delay > 0)
            await Task.Delay(delay);

        PrimarySender.Send(message);
        if (OS.SecOSC) SecondarySender.Send(message);
        if (OS.ThirdOSC) TertiarySender.Send(message);
    }

    private bool ShouldToggleVoice(bool force)
    {
        return _appState.MasterSwitch && (TTS.AutoUnmuteTTS || force);
    }

    private void EnsureTypingTimer()
    {
        if (_typingTimer != null)
            return;

        _typingTimer = new System.Timers.Timer(TYPING_DURATION)
        {
            AutoReset = false
        };
        _typingTimer.Elapsed += (_, _) => _ = DeactivateTypingIndicatorAsync();
    }

    private void StopTypingTimer()
    {
        lock (_typingLock)
        {
            _typingTimer?.Stop();
        }
    }

    private async Task DeactivateTypingIndicatorAsync()
    {
        bool shouldDeactivate;
        long version = 0;
        lock (_typingLock)
        {
            _typingTimer?.Stop();
            shouldDeactivate = _typingIndicatorActive;
            _typingIndicatorActive = false;
            if (shouldDeactivate)
                version = ++_typingIndicatorVersion;
        }

        _chatStatus.TypingIndicator = false;

        if (shouldDeactivate && _appState.MasterSwitch)
            await SendTypingIndicatorStateAsync(false, version);
    }

    private async Task SendTypingIndicatorStateAsync(bool isTyping, long version)
    {
        lock (_typingLock)
        {
            if (version != _typingIndicatorVersion)
                return;

            var message = new OscMessage(CHATBOX_TYPING, isTyping);
            PrimarySender.Send(message);
            if (OS.SecOSC) SecondarySender.Send(message);
            if (OS.ThirdOSC) TertiarySender.Send(message);
        }
        await Task.CompletedTask;
    }

    private async Task ToggleVoiceAsync()
    {
        if (OS.UnmuteMainOutput)
            PrimarySender.Send(new OscMessage(INPUT_VOICE, 1));
        if (OS.SecOSC && OS.UnmuteSecOutput)
            SecondarySender.Send(new OscMessage(INPUT_VOICE, 1));
        if (OS.ThirdOSC && OS.UnmuteThirdOutput)
            TertiarySender.Send(new OscMessage(INPUT_VOICE, 1));

        _ttsAudio.TTSBtnShadow = true;
        await Task.Delay(100);

        if (OS.UnmuteMainOutput)
            PrimarySender.Send(new OscMessage(INPUT_VOICE, 0));
        if (OS.SecOSC && OS.UnmuteSecOutput)
            SecondarySender.Send(new OscMessage(INPUT_VOICE, 0));
        if (OS.ThirdOSC && OS.UnmuteThirdOutput)
            TertiarySender.Send(new OscMessage(INPUT_VOICE, 0));

        _ttsAudio.TTSBtnShadow = false;
    }

    private UDPSender PrimarySender
    {
        get
        {
            lock (_senderLock)
            {
                if (_oscSender == null || OS.OscIP != _oscSender.Address || OS.OscPortOut != _oscSender.Port)
                {
                    _oscSender?.Close();
                    _oscSender = new UDPSender(OS.OscIP, OS.OscPortOut);
                }
                return _oscSender;
            }
        }
    }

    private UDPSender SecondarySender
    {
        get
        {
            lock (_senderLock)
            {
                if (_secOscSender == null || OS.SecOSCIP != _secOscSender.Address || OS.SecOSCPort != _secOscSender.Port)
                {
                    _secOscSender?.Close();
                    _secOscSender = new UDPSender(OS.SecOSCIP, OS.SecOSCPort);
                }
                return _secOscSender;
            }
        }
    }

    private UDPSender TertiarySender
    {
        get
        {
            lock (_senderLock)
            {
                if (_thirdOscSender == null || OS.ThirdOSCIP != _thirdOscSender.Address || OS.ThirdOSCPort != _thirdOscSender.Port)
                {
                    _thirdOscSender?.Close();
                    _thirdOscSender = new UDPSender(OS.ThirdOSCIP, OS.ThirdOSCPort);
                }
                return _thirdOscSender;
            }
        }
    }

    #endregion
}
