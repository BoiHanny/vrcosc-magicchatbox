using CoreOSC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes
{
    public static class OSCSender
    {
        public static UDPSender oscSender;
        public static UDPSender SecOscSender;

        // This method sends an OSC packet to a specified address and port with the ViewModel's OSC input
        // If FX is true, the OSC message is formatted to be displayed as FX text
        public static async Task SendOSCMessage(bool FX, int delay = 0)
        {
            // Check if the master switch is on
            if (!ViewModel.Instance.MasterSwitch)
            {
                return;
            }

            // Check if the OSC input is null or too long
            if (string.IsNullOrEmpty(ViewModel.Instance.OSCtoSent) || ViewModel.Instance.OSCtoSent.Length > 144)
            {
                return;
            }

            try
            {
                // Check if we need to close the current sender and create a new one with the updated IP and port
                if (oscSender != null && (ViewModel.Instance.OSCIP != oscSender.Address || ViewModel.Instance.OSCPortOut != oscSender.Port))
                {
                    oscSender.Close();
                    oscSender = null;
                }

                // Check if we need to close the SECcurrent sender and create a new one with the updated IP and port
                if (SecOscSender != null && (ViewModel.Instance.OSCIP != SecOscSender.Address || ViewModel.Instance.SecOSCPort != SecOscSender.Port))
                {
                    oscSender.Close();
                    oscSender = null;
                }

                // Create a new sender if there is none
                if (oscSender == null)
                {
                    oscSender = new UDPSender(ViewModel.Instance.OSCIP, ViewModel.Instance.OSCPortOut);
                }

                // Create a new SECsender if there is none
                if (SecOscSender == null)
                {
                    SecOscSender = new UDPSender(ViewModel.Instance.OSCIP, ViewModel.Instance.SecOSCPort);
                }

                string BlankEgg = "\u0003\u001f";
                string combinedText = ViewModel.Instance.OSCtoSent + BlankEgg;



                // Send the OSC message in a separate thread
                await Task.Run(() =>
                {
                    if (delay > 0)
                        Thread.Sleep(delay);
                    if (combinedText.Length < 145 & ViewModel.Instance.Egg_Dev && ViewModel.Instance.BlankEgg)
                    {
                        oscSender.Send(new OscMessage("/chatbox/input", combinedText, true, FX));
                        if (ViewModel.Instance.SecOSC)
                        {
                            SecOscSender.Send(new OscMessage("/chatbox/input", combinedText, true, FX));
                        }
                    }
                    else
                    {
                        oscSender.Send(new OscMessage("/chatbox/input", ViewModel.Instance.OSCtoSent, true, FX));
                        if (ViewModel.Instance.SecOSC)
                        {
                            SecOscSender.Send(new OscMessage("/chatbox/input", ViewModel.Instance.OSCtoSent, true, FX));
                        }
                    }

                });
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
                return;
            }

        }


        // this method sends an OSC message to toggle the TTS button on and off in VRChat
        // if force is true, the TTS button is forced to be toggled on
        public static async Task ToggleVoice(bool force = false)
        {
            // Check if the master switch is on and if the auto unmute TTS is on or if we force the TTS but only if the master switch is on
            if (ViewModel.Instance.MasterSwitch && !ViewModel.Instance.AutoUnmuteTTS || !force && !ViewModel.Instance.MasterSwitch)
            {
                return;
            }

            try
            {
                // Check if we need to close the current sender and create a new one with the updated IP and port
                if (oscSender != null && (ViewModel.Instance.OSCIP != oscSender.Address || ViewModel.Instance.OSCPortOut != oscSender.Port))
                {
                    oscSender.Close();
                    oscSender = null;
                }

                // Check if we need to close the SECcurrent sender and create a new one with the updated IP and port
                if (SecOscSender != null && (ViewModel.Instance.OSCIP != SecOscSender.Address || ViewModel.Instance.SecOSCPort != SecOscSender.Port))
                {
                    oscSender.Close();
                    oscSender = null;
                }

                // Create a new sender if there is none
                if (oscSender == null)
                {
                    oscSender = new UDPSender(ViewModel.Instance.OSCIP, ViewModel.Instance.OSCPortOut);
                }

                // Create a new SECsender if there is none
                if (SecOscSender == null)
                {
                    SecOscSender = new UDPSender(ViewModel.Instance.OSCIP, ViewModel.Instance.SecOSCPort);
                }

                // Send the OSC message in a separate thread
                await Task.Run(() =>
                {
                    oscSender.Send(new OscMessage("/input/Voice", 1));
                    if (ViewModel.Instance.SecOSC)
                    {
                        SecOscSender.Send(new OscMessage("/input/Voice", 1));
                    }
                    ViewModel.Instance.TTSBtnShadow = true;
                    Thread.Sleep(100);
                    oscSender.Send(new OscMessage("/input/Voice", 0));
                    if (ViewModel.Instance.SecOSC)
                    {
                        SecOscSender.Send(new OscMessage("/input/Voice", 1));
                    }
                    ViewModel.Instance.TTSBtnShadow = false;
                });
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
            }
        }


        // this method will change the typing indicator in VRChat to the current state of the method call
        // if typing is true, the typing indicator will be on
        public static async Task TypingIndicatorAsync(bool Typing)
        {
            // Check if the master switch is on
            if (!ViewModel.Instance.MasterSwitch)
            {
                return;
            }

            //Set the TypingIndicator in the ViewModel to the current state from the method call
            ViewModel.Instance.TypingIndicator = Typing;
            try
            {
                // Check if we need to close the current sender and create a new one with the updated IP and port
                if (oscSender != null && (ViewModel.Instance.OSCIP != oscSender.Address || ViewModel.Instance.OSCPortOut != oscSender.Port))
                {
                    oscSender.Close();
                    oscSender = null;
                }

                // Check if we need to close the SECcurrent sender and create a new one with the updated IP and port
                if (SecOscSender != null && (ViewModel.Instance.OSCIP != SecOscSender.Address || ViewModel.Instance.SecOSCPort != SecOscSender.Port))
                {
                    SecOscSender.Close();
                    SecOscSender = null;
                }

                // Create a new sender if there is none
                if (oscSender == null)
                {
                    oscSender = new UDPSender(ViewModel.Instance.OSCIP, ViewModel.Instance.OSCPortOut);
                }

                // Create a new SECsender if there is none
                if (SecOscSender == null)
                {
                    SecOscSender = new UDPSender(ViewModel.Instance.OSCIP, ViewModel.Instance.SecOSCPort);
                }

                // Send the OSC message in a separate thread
                await Task.Run(() =>
                {
                    oscSender.Send(new OscMessage("/chatbox/typing", Typing));
                    if (ViewModel.Instance.SecOSC)
                    {
                        SecOscSender.Send(new OscMessage("/chatbox/typing", Typing));
                    }
                });

            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
            }
        }
    }
}
