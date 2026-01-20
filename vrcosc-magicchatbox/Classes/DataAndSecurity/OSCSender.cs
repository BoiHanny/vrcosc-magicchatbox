using CoreOSC;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.DataAndSecurity;

public static class OSCSender
{
    private const string CHATBOX_INPUT = "/chatbox/input";
    private const string CHATBOX_TYPING = "/chatbox/typing";
    private const int COOLDOWN_DURATION = 1000;  // 1 second
    private const string INPUT_VOICE = "/input/Voice";

    private const int TYPING_DURATION = 2000;   // 2 seconds

    private static UDPSender? _oscSender;
    private static UDPSender? _secOscSender;
    private static UDPSender? _thirdOscSender;
    private static System.Timers.Timer cooldownTimer;
    private static bool isInCooldown = false;
    private static bool _lastChatboxHadContent = false;

    private static System.Timers.Timer typingTimer;

    private static async void ActivateTypingIndicator()
    {
        ViewModel.Instance.TypingIndicator = true;

        await Task.Run(() =>
        {
            OscSender.Send(new OscMessage(CHATBOX_TYPING, true));
            if (ViewModel.Instance.SecOSC)
            {
                SecOscSender.Send(new OscMessage(CHATBOX_TYPING, true));
            }
            if (ViewModel.Instance.ThirdOSC)
            {
                ThirdOscSender.Send(new OscMessage(CHATBOX_TYPING, true));
            }
        });
    }

    private static async void DeactivateTypingIndicator()
    {
        ViewModel.Instance.TypingIndicator = false;

        await Task.Run(() =>
        {
            OscSender.Send(new OscMessage(CHATBOX_TYPING, false));
            if (ViewModel.Instance.SecOSC)
            {
                SecOscSender.Send(new OscMessage(CHATBOX_TYPING, false));
            }
            if (ViewModel.Instance.ThirdOSC)
            {
                ThirdOscSender.Send(new OscMessage(CHATBOX_TYPING, false));
            }
        });
    }

    private static OscMessage PrepareMessage(bool FX)
    {
        string BlankEgg = "\u0003\u001f";
        string combinedText = ViewModel.Instance.OSCtoSent + BlankEgg;

        if (combinedText.Length < 145 && ViewModel.Instance.Egg_Dev && ViewModel.Instance.BlankEgg)
        {
            return new OscMessage(CHATBOX_INPUT, combinedText, true, FX);
        }
        else
        {
            return new OscMessage(CHATBOX_INPUT, ViewModel.Instance.OSCtoSent, true, FX);
        }
    }

    private static async Task SendMessageAsync(OscMessage message, int delay)
    {
        await Task.Run(() =>
        {
            if (delay > 0)
                Thread.Sleep(delay);

            OscSender.Send(message);
            if (ViewModel.Instance.SecOSC)
            {
                SecOscSender.Send(message);
            }
            if (ViewModel.Instance.ThirdOSC)
            {
                ThirdOscSender.Send(message);
            }
        });
    }

    private static bool ShouldToggleVoice(bool force)
    {
        return ViewModel.Instance.MasterSwitch && (ViewModel.Instance.AutoUnmuteTTS || force);
    }

    private static void StartCooldown()
    {
        if (cooldownTimer == null)
        {
            cooldownTimer = new System.Timers.Timer(COOLDOWN_DURATION);
            cooldownTimer.Elapsed += (s, e) => isInCooldown = false;
            cooldownTimer.AutoReset = false;
        }
        else
        {
            cooldownTimer.Stop();
        }

        isInCooldown = true;
        cooldownTimer.Start();
    }

    private static async Task ToggleVoiceAsync()
    {
        await Task.Run(() =>
        {
            if (ViewModel.Instance.UnmuteMainOutput)
                OscSender.Send(new OscMessage(INPUT_VOICE, 1));

            if (ViewModel.Instance.SecOSC && ViewModel.Instance.UnmuteSecOutput)
                SecOscSender.Send(new OscMessage(INPUT_VOICE, 1));
            if (ViewModel.Instance.ThirdOSC && ViewModel.Instance.UnmuteThirdOutput)
                ThirdOscSender.Send(new OscMessage(INPUT_VOICE, 1));

            ViewModel.Instance.TTSBtnShadow = true;
            Thread.Sleep(100);

            if (ViewModel.Instance.UnmuteMainOutput)
                OscSender.Send(new OscMessage(INPUT_VOICE, 0));
            if (ViewModel.Instance.SecOSC && ViewModel.Instance.UnmuteSecOutput)
                SecOscSender.Send(new OscMessage(INPUT_VOICE, 0));
            if (ViewModel.Instance.ThirdOSC && ViewModel.Instance.UnmuteThirdOutput)
                ThirdOscSender.Send(new OscMessage(INPUT_VOICE, 0));

            ViewModel.Instance.TTSBtnShadow = false;
        });
    }

    private static UDPSender OscSender
    {
        get
        {
            if (_oscSender == null || ViewModel.Instance.OSCIP != _oscSender.Address || ViewModel.Instance.OSCPortOut != _oscSender.Port)
            {
                _oscSender?.Close();
                _oscSender = new UDPSender(ViewModel.Instance.OSCIP, ViewModel.Instance.OSCPortOut);
            }
            return _oscSender;
        }
    }

    private static UDPSender SecOscSender
    {
        get
        {
            if (_secOscSender == null || ViewModel.Instance.SecOSCIP != _secOscSender.Address || ViewModel.Instance.SecOSCPort != _secOscSender.Port)
            {
                _secOscSender?.Close();
                _secOscSender = new UDPSender(ViewModel.Instance.SecOSCIP, ViewModel.Instance.SecOSCPort);
            }
            return _secOscSender;
        }
    }

    private static UDPSender ThirdOscSender
    {
        get
        {
            if (_thirdOscSender == null || ViewModel.Instance.ThirdOSCIP != _thirdOscSender.Address || ViewModel.Instance.ThirdOSCPort != _thirdOscSender.Port)
            {
                _thirdOscSender?.Close();
                _thirdOscSender = new UDPSender(ViewModel.Instance.ThirdOSCIP, ViewModel.Instance.ThirdOSCPort);
            }
            return _thirdOscSender;
        }
    }

    public static async Task SendOSCMessage(bool FX, int delay = 0)
    {
        if (!ViewModel.Instance.MasterSwitch || ViewModel.Instance.OSCtoSent.Length > 144)
            return;

        if (string.IsNullOrEmpty(ViewModel.Instance.OSCtoSent))
        {
            if (_lastChatboxHadContent)
            {
                _lastChatboxHadContent = false;
                await SentClearMessage(0);
            }
            return;
        }

        await SendMessageAsync(PrepareMessage(FX), delay);
        _lastChatboxHadContent = true;
    }

    public static void SendOscParam(string address, float value)
    {
        if (!ViewModel.Instance.MasterSwitch) return;

        var msg = new OscMessage(address, value);
        OscSender.Send(msg);
        if (ViewModel.Instance.SecOSC)
            SecOscSender.Send(msg);
        if (ViewModel.Instance.ThirdOSC)
            ThirdOscSender.Send(msg);
    }

    public static void SendOscParam(string address, int value)
    {
        if (!ViewModel.Instance.MasterSwitch) return;

        var msg = new OscMessage(address, value);
        OscSender.Send(msg);
        if (ViewModel.Instance.SecOSC)
            SecOscSender.Send(msg);
        if (ViewModel.Instance.ThirdOSC)
            ThirdOscSender.Send(msg);
    }

    public static void SendOscParam(string address, bool value)
    {
        if (!ViewModel.Instance.MasterSwitch) return;

        var msg = new OscMessage(address, value ? 1 : 0);
        OscSender.Send(msg);
        if (ViewModel.Instance.SecOSC)
            SecOscSender.Send(msg);
        if (ViewModel.Instance.ThirdOSC)
            ThirdOscSender.Send(msg);
    }

    public static void SendTypingIndicatorAsync()
    {
        if (!ViewModel.Instance.MasterSwitch)
            return;

        if (isInCooldown)
            return;

        ActivateTypingIndicator();

        if (typingTimer == null)
        {
            typingTimer = new System.Timers.Timer(TYPING_DURATION);
            typingTimer.Elapsed += (s, e) =>
            {
                DeactivateTypingIndicator();
                StartCooldown();
            };
            typingTimer.AutoReset = false;
        }
        else
        {
            typingTimer.Stop();
            typingTimer.Start();
        }
    }

    public static async Task SentClearMessage(int delay)
    {
        if (!ViewModel.Instance.MasterSwitch)
            return;

        var clearMessage = new OscMessage(CHATBOX_INPUT, "", true, false);
        await SendMessageAsync(clearMessage, delay);
        _lastChatboxHadContent = false;
    }

    public static async Task ToggleVoice(bool force = false)
    {
        if (!ShouldToggleVoice(force))
            return;

        await ToggleVoiceAsync();
    }
}
