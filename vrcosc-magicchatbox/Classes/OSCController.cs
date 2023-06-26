using CoreOSC;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes
{
    public static class OSCController
    {

        public static UDPSender oscSender;
        public static UDPSender SecOscSender;

        // This method sends an OSC packet to a specified address and port with the ViewModel's OSC input
        // If FX is true, the OSC message is formatted to be displayed as FX text
        public static async Task SendOSCMessage(bool FX)
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
                        if (combinedText.Length < 145 & ViewModel.Instance.Egg_Dev && ViewModel.Instance.BlankEgg)
                        {
                        oscSender.Send(new OscMessage("/chatbox/input", combinedText, true, FX));
                        if(ViewModel.Instance.SecOSC)
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


        // this function calculates the length of the OSC message to be sent to VRChat and returns it as an int
        // it takes a list of strings and a string to add to the list as parameters
        public static int CalculateOSCMsgLength(List<string> content, string add)
        {
            List<string> list = new List<string>(content) { add };
            string joinedString = String.Join(" | ", list);
            return joinedString.Length;
        }


        // this function will add a string to a list of strings if the total length of the list is less than 144 characters
        public static void TryAddToUncomplete(List<string> uncomplete, string x, string controlToChange)
        {
            if (CalculateOSCMsgLength(uncomplete, x) < 144)
            {
                uncomplete.Add(x);
            }
            else
            {
                ViewModel.Instance.Char_Limit = "Visible";
                SetOpacity(controlToChange, "0.5");
            }
        }


        // this function will set the opacity of a control in the UI to the value of the opacity parameter based on the control name
        public static void SetOpacity(string controlName, string opacity)
        {
            switch (controlName)
            {
                case "Window":
                    ViewModel.Instance.Window_Opacity = opacity;
                    break;
                case "Time":
                    ViewModel.Instance.Time_Opacity = opacity;
                    break;
                case "Spotify":
                    ViewModel.Instance.Spotify_Opacity = opacity;
                    break;
                case "HeartRate":
                    ViewModel.Instance.HeartRate_Opacity = opacity;
                    break;
                case "MediaLink":
                    ViewModel.Instance.MediaLink_Opacity = opacity;
                    break;
                default:
                    break;
            }
        }


        // this function is for building the final OSC message to be sent to VRChat and it will set the opacity of the controls in the UI based on the length of the message
        // it will also set the OSCtoSent property in the ViewModel to the final OSC message 
        public static void BuildOSC()
        {
            //  Create a list of strings to hold the OSC message
            var Complete_msg = "";
            List<string> Uncomplete = new List<string>();

            // Mapping the functions with their respective boolean properties
            var functionMap = new Dictionary<Func<bool>, Action<List<string>>>
            {
                { () => ViewModel.Instance.IntgrStatus_VR && ViewModel.Instance.IsVRRunning
                        || ViewModel.Instance.IntgrStatus_DESKTOP && !ViewModel.Instance.IsVRRunning, AddStatusMessage },

                { () => ViewModel.Instance.IntgrWindowActivity_VR && ViewModel.Instance.IsVRRunning
                        || ViewModel.Instance.IntgrWindowActivity_DESKTOP && !ViewModel.Instance.IsVRRunning, AddWindowActivity },

                { () => ViewModel.Instance.IntgrHeartRate_VR && ViewModel.Instance.IsVRRunning
                        || ViewModel.Instance.IntgrHeartRate_DESKTOP && !ViewModel.Instance.IsVRRunning, AddHeartRate },

                { () => ViewModel.Instance.IntgrCurrentTime_VR && ViewModel.Instance.IsVRRunning
                        || ViewModel.Instance.IntgrCurrentTime_DESKTOP && !ViewModel.Instance.IsVRRunning, AddCurrentTime },

                { () => ViewModel.Instance.IntgrSpotifyStatus_VR && ViewModel.Instance.IsVRRunning
                        || ViewModel.Instance.IntgrSpotifyStatus_DESKTOP && !ViewModel.Instance.IsVRRunning, AddSpotifyStatus },

                { () => ViewModel.Instance.IntgrMediaLink_VR && ViewModel.Instance.IsVRRunning
                        || ViewModel.Instance.IntgrMediaLink_DESKTOP && !ViewModel.Instance.IsVRRunning, AddMediaLink },
            };

            try
            {
                // Reset the opacity of all controls
                ViewModel.Instance.Char_Limit = "Hidden";
                SetOpacity("Spotify", "1");
                SetOpacity("HeartRate", "1");
                SetOpacity("Window", "1");
                SetOpacity("Time", "1");
                SetOpacity("MediaLink", "1");

                // Add the strings to the list if the total length of the list is less than 144 characters
                foreach (var kvp in functionMap)
                {
                    if (kvp.Key.Invoke())
                    {
                        kvp.Value.Invoke(Uncomplete);
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
            }

            // Join the list of strings into one string and set the OSCtoSent property in the ViewModel to the final OSC message
            if (ViewModel.Instance.SeperateWithENTERS)
            {
                var sb = new StringBuilder();
                foreach (var item in Uncomplete)
                {
                    sb.Append(item);
                    sb.Append("\v");
                }
                Complete_msg = sb.ToString();
            }
            else
            {
                var sb = new StringBuilder();
                foreach (var item in Uncomplete)
                {
                    sb.Append(item);
                    sb.Append(" ┆ ");
                }
                Complete_msg = sb.ToString();
            }



            // set ui elements based on the length of the final OSC message and set the OSCtoSent property in the ViewModel to the final OSC message
            if (Complete_msg.Length > 144)
                {
                    ViewModel.Instance.OSCtoSent = "";
                    ViewModel.Instance.OSCmsg_count = Complete_msg.Length;
                    ViewModel.Instance.OSCmsg_countUI = "MAX/144";
                }
                else
                {
                    ViewModel.Instance.OSCtoSent = Complete_msg;
                    ViewModel.Instance.OSCmsg_count = ViewModel.Instance.OSCtoSent.Length;
                    ViewModel.Instance.OSCmsg_countUI = ViewModel.Instance.OSCtoSent.Length + "/144";
                }
        }


        // this function will build the status message to be sent to VRChat and add it to the list of strings if the total length of the list is less than 144 characters
        public static void AddStatusMessage(List<string> Uncomplete)
        {
            if (ViewModel.Instance.IntgrStatus == true && ViewModel.Instance.StatusList.Count() != 0)
            {
                string? x = ViewModel.Instance.PrefixIconStatus == true ? "💬 " + ViewModel.Instance.StatusList.FirstOrDefault(item => item.IsActive == true)?.msg
                                                                        : ViewModel.Instance.StatusList.FirstOrDefault(item => item.IsActive == true)?.msg;
                TryAddToUncomplete(Uncomplete, x, "Window");
            }
        }


        // this function will build the window activity message to be sent to VRChat and add it to the list of strings if the total length of the list is less than 144 characters
        public static void AddWindowActivity(List<string> Uncomplete)
        {
            if (ViewModel.Instance.IntgrScanWindowActivity == true)
            {
                string x = ViewModel.Instance.IsVRRunning ? "In VR"
                                                            : "On desktop in '" + ViewModel.Instance.FocusedWindow + "'";
                TryAddToUncomplete(Uncomplete, x, "Window");
            }
        }


        // this function will build the heart rate message to be sent to VRChat and add it to the list of strings if the total length of the list is less than 144 characters
        public static void AddHeartRate(List<string> Uncomplete)
        {
            if (ViewModel.Instance.IntgrHeartRate == true && ViewModel.Instance.HeartRate > 0)
            {
                string x = (ViewModel.Instance.ShowBPMSuffix ? ViewModel.Instance.HeartRate + " BPM" : "💖 " + ViewModel.Instance.HeartRate);
                if(ViewModel.Instance.ShowHeartRateTrendIndicator)
                {
                    x = x + ViewModel.Instance.HeartRateTrendIndicator;
                }
                TryAddToUncomplete(Uncomplete, x, "HeartRate");
            }
        }



        // this function will build the current time message to be sent to VRChat and add it to the list of strings if the total length of the list is less than 144 characters
        public static void AddCurrentTime(List<string> Uncomplete)
        {
            if (ViewModel.Instance.IntgrScanWindowTime == true)
            {
                string x = ViewModel.Instance.PrefixTime == true ? "My time: " + ViewModel.Instance.CurrentTime
                                                                  : ViewModel.Instance.CurrentTime;
                TryAddToUncomplete(Uncomplete, x, "Time");
            }
        }


        // this function will build the spotify status message to be sent to VRChat and add it to the list of strings if the total length of the list is less than 144 characters
        public static void AddSpotifyStatus(List<string> Uncomplete)
        {
            if (ViewModel.Instance.IntgrScanSpotify_OLD == true)
            {
                if (ViewModel.Instance.SpotifyActive == true)
                {
                    string x;
                    if (ViewModel.Instance.SpotifyPaused)
                    {
                        x = ViewModel.Instance.PauseIconMusic == true && ViewModel.Instance.PrefixIconMusic == true ? "⏸"
                                                                                                                     : "Music paused";
                        TryAddToUncomplete(Uncomplete, x, "Spotify");
                    }
                    else
                    {
                        if (ViewModel.Instance.PlayingSongTitle.Length > 0)
                        {
                            x = ViewModel.Instance.PrefixIconMusic == true ? "🎵 '" + ViewModel.Instance.PlayingSongTitle + "'"
                                                                           : "Listening to '" + ViewModel.Instance.PlayingSongTitle + "'";
                            TryAddToUncomplete(Uncomplete, x, "Spotify");
                        }
                        else
                        {
                            // Insert the code for handling the PlayingSongTitle Length being 0. 
                            // This includes creating a new window and showing the information message.
                            // ...
                        }
                    }
                }
            }
        }

        public static void AddMediaLink(List<string> Uncomplete)
        {
            if (ViewModel.Instance.IntgrScanMediaLink)
            {
                string x;
                MediaSessionInfo mediaSession = ViewModel.Instance.MediaSessions.FirstOrDefault(item => item.IsActive);

                if (mediaSession != null)
                {
                    var isPaused = mediaSession.PlaybackStatus == Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused;
                    var isPlaying = mediaSession.PlaybackStatus == Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

                    if (isPaused || isPlaying)
                    {
                        var mediaType = mediaSession.IsVideo ? "Video" : "Music";
                        var prefix = mediaSession.IsVideo ? "🎬" : "🎵";
                        var mediaAction = mediaSession.IsVideo ? "Watching" : "Listening to";

                        if (isPaused)
                        {
                            x = ViewModel.Instance.PauseIconMusic && ViewModel.Instance.PrefixIconMusic ? "⏸" : $"{mediaType} paused";
                        }
                        else // isPlaying
                        {
                            var mediaLinkTitle = CreateMediaLinkTitle(mediaSession);
                            x = ViewModel.Instance.PrefixIconMusic ? $"{prefix} '{mediaLinkTitle}'" : $"{mediaAction} '{mediaLinkTitle}'";
                        }

                        TryAddToUncomplete(Uncomplete, x, "MediaLink");
                    }
                }
                else
                {
                    x = ViewModel.Instance.PauseIconMusic && ViewModel.Instance.PrefixIconMusic ? "⏸" : "Paused";
                    TryAddToUncomplete(Uncomplete, x, "MediaLink");
                }
            }
        }



        //make a function to create the song title take the 3 bools in mediasessioninfo IsVideo, ShowArtist, ShowTitle
        public static string CreateMediaLinkTitle(MediaSessionInfo mediaSession)
        {
            StringBuilder mediaLinkTitle = new StringBuilder();

            if (mediaSession.ShowTitle && !string.IsNullOrEmpty(mediaSession.Title))
            {
                mediaLinkTitle.Append(mediaSession.Title);
            }

            if (mediaSession.ShowArtist && !string.IsNullOrEmpty(mediaSession.Artist))
            {
                if (mediaLinkTitle.Length > 0)
                {
                    mediaLinkTitle.Append(" ᵇʸ ");
                }

                mediaLinkTitle.Append(mediaSession.Artist);
            }

            return mediaLinkTitle.Length > 0 ? mediaLinkTitle.ToString() : "⏸";
        }







        // this function will create a new chat message and add it to the list of strings if the total length of the list is less than 144 characters
        // this function will also set the OSCtoSent property in the ViewModel to the final OSC message
        public static void CreateChat(bool createItem)
        {
            try
            {
                string Complete_msg = null;
                if (ViewModel.Instance.PrefixChat == true)
                {
                    Complete_msg = "💬 " + ViewModel.Instance.NewChattingTxt;
                }
                else
                {
                    Complete_msg = ViewModel.Instance.NewChattingTxt;
                }

                if (Complete_msg.Length < 4)
                {

                }
                else if (Complete_msg.Length > 144)
                {

                }
                else
                {
                    ViewModel.Instance.ScanPauseCountDown = ViewModel.Instance.ScanPauseTimeout;
                    ViewModel.Instance.ScanPause = true;
                    ViewModel.Instance.OSCtoSent = Complete_msg;
                    ViewModel.Instance.OSCmsg_count = ViewModel.Instance.OSCtoSent.Length;
                    ViewModel.Instance.OSCmsg_countUI = ViewModel.Instance.OSCtoSent.Length + "/144";
                    ViewModel.Instance.ActiveChatTxt = "Active";

                    if (createItem == true)
                    {
                        Random random = new Random();
                        int randomId = random.Next(10, 99999999);

                        var newChatItem = new ChatItem() { Msg = ViewModel.Instance.NewChattingTxt, CreationDate = DateTime.Now, ID = randomId };
                        ViewModel.Instance.LastMessages.Add(newChatItem);

                        if (ViewModel.Instance.LastMessages.Count > 5)
                        {
                            ViewModel.Instance.LastMessages.RemoveAt(0);
                        }

                        double opacity = 1;
                        foreach (var item in ViewModel.Instance.LastMessages.Reverse())
                        {
                            opacity -= 0.18;
                            item.Opacity = opacity.ToString("F1", CultureInfo.InvariantCulture);
                        }

                        var currentList = new ObservableCollection<ChatItem>(ViewModel.Instance.LastMessages);
                        ViewModel.Instance.LastMessages.Clear();

                        foreach (var item in currentList)
                        {
                            ViewModel.Instance.LastMessages.Add(item);
                        }
                        ViewModel.Instance.NewChattingTxt = "";
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
            }

        }

        // this function clears the chat window and resets the chat related variables to their default values
        internal static void ClearChat()
        {
            ViewModel.Instance.ScanPause = false;
            ViewModel.Instance.OSCtoSent = "";
            ViewModel.Instance.OSCmsg_count = 0;
            ViewModel.Instance.OSCmsg_countUI = "0/144";
            ViewModel.Instance.ActiveChatTxt = "";
        }
    }
}
