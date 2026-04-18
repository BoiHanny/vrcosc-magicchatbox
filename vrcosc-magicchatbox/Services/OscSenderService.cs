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
    private const int COOLDOWN_DURATION = 1000;
    private const string INPUT_VOICE = "/input/Voice";
    private const int TYPING_DURATION = 2000;

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

    private System.Timers.Timer? _cooldownTimer;
    private bool _isInCooldown;
    private bool _lastChatboxHadContent;
    private System.Timers.Timer? _typingTimer;

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

    public async Task SendOSCMessage(bool fx, int delay = 0)
    {
        if (!_appState.MasterSwitch || _oscDisplay.OscToSent.Length > Core.Constants.OscMaxMessageLength)
            return;

        if (string.IsNullOrEmpty(_oscDisplay.OscToSent))
        {
            if (_lastChatboxHadContent)
            {
                _lastChatboxHadContent = false;
                await SentClearMessage(0);
            }
            return;
        }

        await SendMessageAsync(PrepareMessage(fx), delay);
        _lastChatboxHadContent = true;
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
        if (!_appState.MasterSwitch || _isInCooldown)
            return;

        _ = ActivateTypingIndicator();

        if (_typingTimer == null)
        {
            _typingTimer = new System.Timers.Timer(TYPING_DURATION);
            _typingTimer.Elapsed += (s, e) =>
            {
                _ = DeactivateTypingIndicator();
                StartCooldown();
            };
            _typingTimer.AutoReset = false;
        }
        else
        {
            _typingTimer.Stop();
            _typingTimer.Start();
        }
    }

    public async Task SentClearMessage(int delay)
    {
        if (!_appState.MasterSwitch)
            return;

        var clearMessage = new OscMessage(CHATBOX_INPUT, "", true, false);
        await SendMessageAsync(clearMessage, delay);
        _lastChatboxHadContent = false;
    }

    public async Task ToggleVoice(bool force = false)
    {
        if (!ShouldToggleVoice(force))
            return;

        await ToggleVoiceAsync();
    }

    public void Dispose()
    {
        _cooldownTimer?.Dispose();
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
        string blankEgg = "\u0003\u001f";
        string combinedText = _oscDisplay.OscToSent + blankEgg;

        if (combinedText.Length < 145 && _appState.Egg_Dev && AS.BlankEgg)
            return new OscMessage(CHATBOX_INPUT, combinedText, true, fx);
        else
            return new OscMessage(CHATBOX_INPUT, _oscDisplay.OscToSent, true, fx);
    }

    private async Task SendMessageAsync(OscMessage message, int delay)
    {
        await Task.Run(async () =>
        {
            if (delay > 0)
                await Task.Delay(delay);

            PrimarySender.Send(message);
            if (OS.SecOSC) SecondarySender.Send(message);
            if (OS.ThirdOSC) TertiarySender.Send(message);
        });
    }

    private bool ShouldToggleVoice(bool force)
    {
        return _appState.MasterSwitch && (TTS.AutoUnmuteTTS || force);
    }

    private void StartCooldown()
    {
        if (_cooldownTimer == null)
        {
            _cooldownTimer = new System.Timers.Timer(COOLDOWN_DURATION);
            _cooldownTimer.Elapsed += (s, e) => _isInCooldown = false;
            _cooldownTimer.AutoReset = false;
        }
        else
        {
            _cooldownTimer.Stop();
        }

        _isInCooldown = true;
        _cooldownTimer.Start();
    }

    private async Task ActivateTypingIndicator()
    {
        _chatStatus.TypingIndicator = true;

        await Task.Run(() =>
        {
            PrimarySender.Send(new OscMessage(CHATBOX_TYPING, true));
            if (OS.SecOSC) SecondarySender.Send(new OscMessage(CHATBOX_TYPING, true));
            if (OS.ThirdOSC) TertiarySender.Send(new OscMessage(CHATBOX_TYPING, true));
        });
    }

    private async Task DeactivateTypingIndicator()
    {
        _chatStatus.TypingIndicator = false;

        await Task.Run(() =>
        {
            PrimarySender.Send(new OscMessage(CHATBOX_TYPING, false));
            if (OS.SecOSC) SecondarySender.Send(new OscMessage(CHATBOX_TYPING, false));
            if (OS.ThirdOSC) TertiarySender.Send(new OscMessage(CHATBOX_TYPING, false));
        });
    }

    private async Task ToggleVoiceAsync()
    {
        await Task.Run(() =>
        {
            if (OS.UnmuteMainOutput)
                PrimarySender.Send(new OscMessage(INPUT_VOICE, 1));
            if (OS.SecOSC && OS.UnmuteSecOutput)
                SecondarySender.Send(new OscMessage(INPUT_VOICE, 1));
            if (OS.ThirdOSC && OS.UnmuteThirdOutput)
                TertiarySender.Send(new OscMessage(INPUT_VOICE, 1));

            _ttsAudio.TTSBtnShadow = true;
            Thread.Sleep(100);

            if (OS.UnmuteMainOutput)
                PrimarySender.Send(new OscMessage(INPUT_VOICE, 0));
            if (OS.SecOSC && OS.UnmuteSecOutput)
                SecondarySender.Send(new OscMessage(INPUT_VOICE, 0));
            if (OS.ThirdOSC && OS.UnmuteThirdOutput)
                TertiarySender.Send(new OscMessage(INPUT_VOICE, 0));

            _ttsAudio.TTSBtnShadow = false;
        });
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
