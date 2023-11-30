using CoreOSC;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.DataAndSecurity
{
    public static class OSCSender
    {
        private const string CHATBOX_INPUT = "/chatbox/input";
        private const string INPUT_VOICE = "/input/Voice";
        private const string CHATBOX_TYPING = "/chatbox/typing";

        private const int TYPING_DURATION = 2000;   // 3 seconds
        private const int COOLDOWN_DURATION = 1000;  // 0.5 seconds

        private static System.Timers.Timer typingTimer;
        private static System.Timers.Timer cooldownTimer;
        private static bool isInCooldown = false;

        private static UDPSender _oscSender;
        private static UDPSender _secOscSender;

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
                if (_secOscSender == null || ViewModel.Instance.OSCIP != _secOscSender.Address || ViewModel.Instance.SecOSCPort != _secOscSender.Port)
                {
                    _secOscSender?.Close();
                    _secOscSender = new UDPSender(ViewModel.Instance.OSCIP, ViewModel.Instance.SecOSCPort);
                }
                return _secOscSender;
            }
        }

        // This method sends an OSC packet to a specified address and port with the ViewModel's OSC input
        public static async Task SendOSCMessage(bool FX, int delay = 0)
        {
            if (!ViewModel.Instance.MasterSwitch || string.IsNullOrEmpty(ViewModel.Instance.OSCtoSent) || ViewModel.Instance.OSCtoSent.Length > 144)
                return;

            await SendMessageAsync(PrepareMessage(FX), delay);
        }

        public static async Task ToggleVoice(bool force = false)
        {
            if (!ShouldToggleVoice(force))
                return;

            await ToggleVoiceAsync();
        }

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
            });
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


        private static bool ShouldToggleVoice(bool force)
        {
            return ViewModel.Instance.MasterSwitch && ViewModel.Instance.AutoUnmuteTTS || !force && ViewModel.Instance.MasterSwitch;
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
            });
        }

        private static async Task ToggleVoiceAsync()
        {
            await Task.Run(() =>
            {
                OscSender.Send(new OscMessage(INPUT_VOICE, 1));
                if (ViewModel.Instance.SecOSC)
                    SecOscSender.Send(new OscMessage(INPUT_VOICE, 1));

                ViewModel.Instance.TTSBtnShadow = true;
                Thread.Sleep(100);

                OscSender.Send(new OscMessage(INPUT_VOICE, 0));
                if (ViewModel.Instance.SecOSC)
                    SecOscSender.Send(new OscMessage(INPUT_VOICE, 0));

                ViewModel.Instance.TTSBtnShadow = false;
            });
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
                // Reset the timer if it's already active
                typingTimer.Stop();
                typingTimer.Start();
            }
        }
    }
}

