using CoreOSC;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Services;

public sealed class OscSenderService : IOscSender, IDisposable
{
    private const string CHATBOX_INPUT = "/chatbox/input";
    private const string CHATBOX_TYPING = "/chatbox/typing";
    private const string INPUT_VOICE = "/input/Voice";
    private const int TYPING_DURATION = 2000;
    private static readonly TimeSpan DuplicateKeepAliveInterval = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan SenderRetryCooldown = TimeSpan.FromSeconds(30);

    private readonly OscSettings _oscSettings;
    private readonly AppSettings _appSettings;
    private readonly TtsSettings _ttsSettings;
    private readonly IAppState _appState;
    private readonly ChatStatusDisplayState _chatStatus;
    private readonly OscDisplayState _oscDisplay;
    private readonly TtsAudioDisplayState _ttsAudio;
    private readonly Lazy<Core.Integrations.IIntegrationGate>? _gate;

    private UDPSender? _oscSender;
    private UDPSender? _secOscSender;
    private UDPSender? _thirdOscSender;
    private readonly Dictionary<string, DateTime> _senderRetryAfterUtc = new(StringComparer.Ordinal);
    private readonly HashSet<string> _loggedSenderFailures = new(StringComparer.Ordinal);
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
        TtsAudioDisplayState ttsAudio,
        Lazy<Core.Integrations.IIntegrationGate>? gate = null)
    {
        _gate = gate;
        _oscSettings = oscSettings.Value;
        _appSettings = appSettings.Value;
        _ttsSettings = ttsSettings.Value;
        _appState = appState;
        _chatStatus = chatStatus;
        _oscDisplay = oscDisplay;
        _ttsAudio = ttsAudio;
    }

    private bool SendingIsPermitted()
    {
        if (_gate == null)
            return true;

        try
        {
            return _gate.Value.PermitsSending();
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return true;
        }
    }

    private OscSettings OS => _oscSettings;
    private AppSettings AS => _appSettings;
    private TtsSettings TTS => _ttsSettings;

    public async Task<bool> SendOSCMessage(bool fx, int delay = 0, bool force = false, string? explicitText = null)
    {
        string textToSend = explicitText ?? _oscDisplay.OscToSent;

        if (!_appState.MasterSwitch || textToSend.Length > Core.Constants.OscMaxMessageLength)
            return false;

        if (!SendingIsPermitted())
            return false;

        await DeactivateTypingIndicatorAsync();

        if (explicitText is null && !string.Equals(_oscDisplay.OscToSent, textToSend, StringComparison.Ordinal))
            return false;

        if (string.IsNullOrEmpty(textToSend))
        {
            if (_lastChatboxHadContent)
                return await SentClearMessageCore(0);
            return false;
        }

        string messageSignature = CreateMessageSignature(fx, textToSend);
        if (!force && ShouldSkipDuplicateMessage(messageSignature))
            return false;

        if (!await SendMessageAsync(PrepareMessage(fx, textToSend), delay))
            return false;

        _lastChatboxHadContent = true;
        MarkMessageSent(messageSignature);
        return true;
    }

    public void SendOscParam(string address, float value)
    {
        if (!_appState.MasterSwitch) return;

        SendToTargets(new OscMessage(address, value));
    }

    public void SendOscParam(string address, int value)
    {
        if (!_appState.MasterSwitch) return;

        SendToTargets(new OscMessage(address, value));
    }

    public void SendOscParam(string address, bool value)
    {
        if (!_appState.MasterSwitch) return;

        SendToTargets(new OscMessage(address, value ? 1 : 0));
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
        await SentClearMessageCore(delay);
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

    private OscMessage PrepareMessage(bool fx, string text)
    {
        return new OscMessage(CHATBOX_INPUT, GetPreparedChatboxText(text), true, fx);
    }

    private string GetPreparedChatboxText(string text)
    {
        string blankEgg = "\u0003\u001f";
        string combinedText = text + blankEgg;

        if (combinedText.Length < 145 && _appState.Egg_Dev && AS.BlankEgg)
            return combinedText;

        return text;
    }

    private string CreateMessageSignature(bool fx, string text)
    {
        return string.Join('\u001e', fx, GetPreparedChatboxText(text));
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

    private async Task<bool> SentClearMessageCore(int delay)
    {
        if (!_appState.MasterSwitch)
            return false;

        await DeactivateTypingIndicatorAsync();

        var clearMessage = new OscMessage(CHATBOX_INPUT, "", true, false);
        if (!await SendMessageAsync(clearMessage, delay))
            return false;

        _lastChatboxHadContent = false;
        MarkMessageSent(string.Empty);
        return true;
    }

    private async Task<bool> SendMessageAsync(OscMessage message, int delay)
    {
        return await Task.Run(async () =>
        {
            if (delay > 0)
                await Task.Delay(delay);

            return SendToTargets(message);
        });
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
        await Task.Run(() =>
        {
            lock (_typingLock)
            {
                if (version != _typingIndicatorVersion)
                    return;

                SendToTargets(new OscMessage(CHATBOX_TYPING, isTyping));
            }
        });
    }

    private async Task ToggleVoiceAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                SendVoiceToggle(1);

                _ttsAudio.TTSBtnShadow = true;
                Thread.Sleep(100);

                SendVoiceToggle(0);
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
            finally
            {
                _ttsAudio.TTSBtnShadow = false;
            }
        });
    }

    private UDPSender? PrimaryLocked()
    {
        _oscSender = EnsureSender(_oscSender, OS.OscIP, OS.OscPortOut);
        return _oscSender;
    }

    private UDPSender? SecondaryLocked()
    {
        _secOscSender = EnsureSender(_secOscSender, OS.SecOSCIP, OS.SecOSCPort);
        return _secOscSender;
    }

    private UDPSender? TertiaryLocked()
    {
        _thirdOscSender = EnsureSender(_thirdOscSender, OS.ThirdOSCIP, OS.ThirdOSCPort);
        return _thirdOscSender;
    }

    private bool SendToTargets(OscMessage message)
    {
        lock (_senderLock)
        {
            bool primarySent = TrySend(PrimaryLocked(), message);

            if (OS.SecOSC)
                TrySend(SecondaryLocked(), message);

            if (OS.ThirdOSC)
                TrySend(TertiaryLocked(), message);

            return primarySent;
        }
    }

    private void SendVoiceToggle(int value)
    {
        lock (_senderLock)
        {
            var message = new OscMessage(INPUT_VOICE, value);

            if (OS.UnmuteMainOutput)
                TrySend(PrimaryLocked(), message);

            if (OS.SecOSC && OS.UnmuteSecOutput)
                TrySend(SecondaryLocked(), message);

            if (OS.ThirdOSC && OS.UnmuteThirdOutput)
                TrySend(TertiaryLocked(), message);
        }
    }

    private static bool TrySend(UDPSender? sender, OscMessage message)
    {
        if (sender == null)
            return false;

        try
        {
            sender.Send(message);
            return true;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return false;
        }
    }

    private UDPSender? EnsureSender(UDPSender? current, string address, int port)
    {
        if (current != null && address == current.Address && port == current.Port)
            return current;

        if (!IPAddress.TryParse(address, out _) && Uri.CheckHostName(address) != UriHostNameType.Dns)
            return current;

        string endpoint = $"{address}:{port}";
        if (_senderRetryAfterUtc.TryGetValue(endpoint, out DateTime retryAfterUtc) && DateTime.UtcNow < retryAfterUtc)
            return current;

        try
        {
            var replacement = new UDPSender(address, port);
            current?.Close();
            _senderRetryAfterUtc.Remove(endpoint);
            _loggedSenderFailures.Remove(endpoint);
            return replacement;
        }
        catch (Exception ex)
        {
            _senderRetryAfterUtc[endpoint] = DateTime.UtcNow + SenderRetryCooldown;
            if (_loggedSenderFailures.Add(endpoint))
                Logging.WriteException(ex, MSGBox: false);
            return current;
        }
    }

    #endregion
}
