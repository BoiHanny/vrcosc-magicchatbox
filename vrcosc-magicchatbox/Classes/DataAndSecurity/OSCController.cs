using ABI.System;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using vrcosc_magicchatbox.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.Models;

namespace vrcosc_magicchatbox.Classes.DataAndSecurity
{
    public static class OSCController
    {

        // this function clears the chat window and resets the chat related variables to their default values
        internal static void ClearChat(ChatItem lastsendchat = null)
        {
            ViewModel.Instance.ScanPause = false;
            ViewModel.Instance.OSCtoSent = string.Empty;
            ViewModel.Instance.OSCmsg_count = 0;
            ViewModel.Instance.OSCmsg_countUI = "0/144";
            ViewModel.Instance.ActiveChatTxt = string.Empty;
            if (lastsendchat != null)
            {
                lastsendchat.CanLiveEdit = false;
                lastsendchat.CanLiveEditRun = false;
                lastsendchat.MsgReplace = string.Empty;
                lastsendchat.IsRunning = false;
            }
        }


        // this function will build the current time message to be sent to VRChat and add it to the list of strings if the total length of the list is less than 144 characters
        public static void AddCurrentTime(List<string> Uncomplete)
        {
            if (ViewModel.Instance.IntgrScanWindowTime == true)
            {
                if (ViewModel.Instance.CurrentTime != null)
                {
                    string x = ViewModel.Instance.PrefixTime == true
                        ? "My time: " + ViewModel.Instance.CurrentTime
                        : ViewModel.Instance.CurrentTime;
                    TryAddToUncomplete(Uncomplete, x, "Time");
                }
            }
        }

        public static void AddNetworkStatistics(List<string> Uncomplete)
        {
            if (ViewModel.Instance.IntgrNetworkStatistics == true)
            {
                if(MainWindow.networkStatsModule == null)
                {
                    return;
                }
                // create x string based on the values in MainWindow.networkStatsModule make it all look nice and pretty
                string x = MainWindow.networkStatsModule.GenerateDescription();
                TryAddToUncomplete(Uncomplete, x, "NetworkStatistics");
            }
        }


        // this function will build the heart rate message to be sent to VRChat and add it to the list of strings if the total length of the list is less than 144 characters
        public static void AddHeartRate(List<string> Uncomplete)
        {
            if (ViewModel.Instance.IntgrHeartRate == true && ViewModel.Instance.HeartRate > 0)
            {
                // Pick the correct heart icon
                string heartIcon = ViewModel.Instance.MagicHeartRateIcons || ViewModel.Instance.ShowTemperatureText ? ViewModel.Instance.HeartRateIcon : "💖";

                if (ViewModel.Instance.HeartRateTitle)
                {
                    string hrTitle = "Heart rate" + (ViewModel.Instance.SeperateWithENTERS ? "\v" : ": ");
                    string x = ViewModel.Instance.ShowBPMSuffix
                        ? ViewModel.Instance.HeartRate + " bpm"
                        : (ViewModel.Instance.SeperateWithENTERS ? heartIcon + " " : string.Empty) + ViewModel.Instance.HeartRate;

                    if (ViewModel.Instance.ShowHeartRateTrendIndicator)
                    {
                        x = x + ViewModel.Instance.HeartRateTrendIndicator;
                    }
                    TryAddToUncomplete(Uncomplete, hrTitle + x, "HeartRate");
                }
                else
                {
                    string x = ViewModel.Instance.ShowBPMSuffix
                        ? ViewModel.Instance.HeartRate + " bpm"
                        : heartIcon + " " + ViewModel.Instance.HeartRate;

                    if (ViewModel.Instance.ShowHeartRateTrendIndicator)
                    {
                        x = x + ViewModel.Instance.HeartRateTrendIndicator;
                    }
                    TryAddToUncomplete(Uncomplete, x, "HeartRate");
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
                    var isPaused = mediaSession.PlaybackStatus ==
                        Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused;
                    var isPlaying = mediaSession.PlaybackStatus ==
                        Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

                    if (isPaused || isPlaying)
                    {
                        var mediaType = mediaSession.IsVideo ? "Video" : "Music";
                        var prefix = mediaSession.IsVideo ? "🎬" : "🎵";
                        var mediaAction = mediaSession.IsVideo ? "Watching" : "Listening to";

                        if (isPaused)
                        {
                            x = ViewModel.Instance.PauseIconMusic && ViewModel.Instance.PrefixIconMusic
                                ? "⏸"
                                : $"{mediaType} paused";
                        }
                        else
                        {
                            var mediaLinkTitle = CreateMediaLinkTitle(mediaSession);
                            if (string.IsNullOrEmpty(mediaLinkTitle))
                            {
                                x = ViewModel.Instance.PauseIconMusic && ViewModel.Instance.PrefixIconMusic
                                    ? "⏸"
                                    : "Paused";
                            }
                            else
                            {
                                x = ViewModel.Instance.PrefixIconMusic
                                    ? $"{prefix} '{mediaLinkTitle}'"
                                    : $"{mediaAction} '{mediaLinkTitle}'";
                            }

                            if (ViewModel.Instance.MediaLinkShowTime && !mediaSession.IsLiveTime && mediaSession.TimePeekEnabled)
                            {
                                x = x + DataController.TransformToSuperscript($" {FormatTimeSpan(mediaSession.CurrentTime)} l {FormatTimeSpan(mediaSession.FullTime)}");
                            }
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
                        x = ViewModel.Instance.PauseIconMusic == true && ViewModel.Instance.PrefixIconMusic == true
                            ? "⏸"
                            : "Music paused";
                        TryAddToUncomplete(Uncomplete, x, "Spotify");
                    }
                    else
                    {
                        if (ViewModel.Instance.PlayingSongTitle.Length > 0)
                        {
                            x = ViewModel.Instance.PrefixIconMusic == true
                                ? "🎵 '" + ViewModel.Instance.PlayingSongTitle + "'"
                                : "Listening to '" + ViewModel.Instance.PlayingSongTitle + "'";
                            TryAddToUncomplete(Uncomplete, x, "Spotify");
                        }
                        else
                        {
                            x = ViewModel.Instance.PauseIconMusic == true && ViewModel.Instance.PrefixIconMusic == true
                                ? "⏸"
                                : "Music paused";
                            TryAddToUncomplete(Uncomplete, x, "Spotify");
                        }
                    }
                }
            }
        }


        // this function will build the status message to be sent to VRChat and add it to the list of strings if the total length of the list is less than 144 characters
        public static void AddStatusMessage(List<string> Uncomplete)
        {
            if (ViewModel.Instance.IntgrStatus == true && ViewModel.Instance.StatusList.Count() != 0)
            {
                string? x = ViewModel.Instance.PrefixIconStatus == true
                    ? "💬 " + ViewModel.Instance.StatusList.FirstOrDefault(item => item.IsActive == true)?.msg
                    : ViewModel.Instance.StatusList.FirstOrDefault(item => item.IsActive == true)?.msg;
                TryAddToUncomplete(Uncomplete, x, "Window");
            }
        }

        public static void AddComponentStat(List<string> Uncomplete)
        {
            if (ViewModel.Instance.IntgrComponentStats && !string.IsNullOrEmpty(ViewModel.Instance.ComponentStatCombined) && ViewModel.Instance.ComponentStatsRunning)
            {
                string? x = ViewModel.Instance.ComponentStatCombined;
                TryAddToUncomplete(Uncomplete, x, "ComponentStat");
            }
        }


        // this function will build the window activity message to be sent to VRChat and add it to the list of strings if the total length of the list is less than 144 characters
        public static void AddWindowActivity(List<string> Uncomplete)
        {
            if (ViewModel.Instance.IntgrScanWindowActivity && ViewModel.Instance.FocusedWindow.Length > 0)
            {
                StringBuilder x = new StringBuilder();

                if (ViewModel.Instance.IsVRRunning)
                {
                    x.Append("In VR");
                    if (ViewModel.Instance.IntgrScanForce)
                    {
                        x.Append(" ᶠᵒᶜᵘˢˢⁱⁿᵍ ⁱⁿ ");
                        x.Append(ViewModel.Instance.FocusedWindow);
                        x.Append("");
                    }
                }
                else
                {
                    x.Append("On desktop ⁱⁿ ");
                    x.Append(ViewModel.Instance.FocusedWindow);
                    x.Append("");
                }

                TryAddToUncomplete(Uncomplete, x.ToString(), "Window");
            }
        }



        // this function is for building the final OSC message to be sent to VRChat and it will set the opacity of the controls in the UI based on the length of the message
        // it will also set the OSCtoSent property in the ViewModel to the final OSC message
        public static void BuildOSC()
        {
            //  Create a list of strings to hold the OSC message
            var Complete_msg = string.Empty;
            List<string> Uncomplete = new List<string>();

            // Mapping the functions with their respective boolean properties
            var functionMap = new Dictionary<Func<bool>, Action<List<string>>>
            {
                {
                    () => ViewModel.Instance.IntgrStatus_VR &&
                    ViewModel.Instance.IsVRRunning ||
                    ViewModel.Instance.IntgrStatus_DESKTOP &&
                    !ViewModel.Instance.IsVRRunning,
                    AddStatusMessage
                },

                {
                    () => ViewModel.Instance.IntgrWindowActivity_VR &&
                    ViewModel.Instance.IsVRRunning ||
                    ViewModel.Instance.IntgrWindowActivity_DESKTOP &&
                    !ViewModel.Instance.IsVRRunning,
                    AddWindowActivity
                },

                {
                    () => ViewModel.Instance.IntgrHeartRate_VR &&
                    ViewModel.Instance.IsVRRunning && ViewModel.Instance.PulsoidAuthConnected ||
                    ViewModel.Instance.IntgrHeartRate_DESKTOP &&
                    !ViewModel.Instance.IsVRRunning && ViewModel.Instance.PulsoidAuthConnected,
                    AddHeartRate
                },

                {
                    () => ViewModel.Instance.IntgrComponentStats_VR &&
                    ViewModel.Instance.IsVRRunning ||
                    ViewModel.Instance.IntgrComponentStats_DESKTOP &&
                    !ViewModel.Instance.IsVRRunning,
                    AddComponentStat
                },

                {
                     () => ViewModel.Instance.IntgrNetworkStatistics_VR &&
                    ViewModel.Instance.IsVRRunning ||
                    ViewModel.Instance.IntgrNetworkStatistics_DESKTOP &&
                    !ViewModel.Instance.IsVRRunning,
                    AddNetworkStatistics
                },

                {
                    () => ViewModel.Instance.IntgrCurrentTime_VR &&
                    ViewModel.Instance.IsVRRunning ||
                    ViewModel.Instance.IntgrCurrentTime_DESKTOP &&
                    !ViewModel.Instance.IsVRRunning,
                    AddCurrentTime
                },

                {
                    () => ViewModel.Instance.IntgrSpotifyStatus_VR &&
                    ViewModel.Instance.IsVRRunning ||
                    ViewModel.Instance.IntgrSpotifyStatus_DESKTOP &&
                    !ViewModel.Instance.IsVRRunning,
                    AddSpotifyStatus
                },

                {
                    () => ViewModel.Instance.IntgrMediaLink_VR &&
                    ViewModel.Instance.IsVRRunning ||
                    ViewModel.Instance.IntgrMediaLink_DESKTOP &&
                    !ViewModel.Instance.IsVRRunning,
                    AddMediaLink
                },
            };

            try
            {
                // Reset the opacity of all controls
                ViewModel.Instance.Char_Limit = "Hidden";
                SetOpacity("Spotify", "1");
                SetOpacity("HeartRate", "1");
                SetOpacity("ComponentStat", "1");
                SetOpacity("NetworkStatistics", "1");
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
            catch (System.Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
            }

            // Join the list of strings into one string and set the OSCtoSent property in the ViewModel to the final OSC message
            if (ViewModel.Instance.SeperateWithENTERS)
            {
                Complete_msg = string.Join("\v", Uncomplete);
            }
            else
            {
                Complete_msg = string.Join(" ┆ ", Uncomplete);
            }


            // set ui elements based on the length of the final OSC message and set the OSCtoSent property in the ViewModel to the final OSC message
            if (Complete_msg.Length > 144)
            {
                ViewModel.Instance.OSCtoSent = string.Empty;
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
        // this function calculates the length of the OSC message to be sent to VRChat and returns it as an int
        // it takes a list of strings and a string to add to the list as parameters
        public static int CalculateOSCMsgLength(List<string> content, string add)
        {
            List<string> list = new List<string>(content) { add };
            string joinedString = string.Join(" | ", list);
            return joinedString.Length;
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

                        if (ViewModel.Instance.ChatLiveEdit)
                            foreach (var item in ViewModel.Instance.LastMessages)
                            {
                                item.CanLiveEdit = false;
                                item.CanLiveEditRun = false;
                                item.MsgReplace = string.Empty;
                                item.IsRunning = false;
                            }

                        var newChatItem = new ChatItem()
                        {
                            Msg = ViewModel.Instance.NewChattingTxt,
                            MainMsg = ViewModel.Instance.NewChattingTxt,
                            CreationDate = DateTime.Now,
                            ID = randomId,
                            IsRunning = true,
                            CanLiveEdit = ViewModel.Instance.ChatLiveEdit
                        };
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
                        ViewModel.Instance.NewChattingTxt = string.Empty;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
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

            return mediaLinkTitle.Length > 0 ? mediaLinkTitle.ToString() : string.Empty;
        }

        public static string FormatTimeSpan(System.TimeSpan timeSpan)
        {
            string formattedTime;
            if (timeSpan.Hours > 0)
            {
                formattedTime = $"{timeSpan.Hours}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
            }
            else
            {
                formattedTime = $"{timeSpan.Minutes}:{timeSpan.Seconds:D2}";
            }
            return formattedTime;
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
                case "ComponentStat":
                    ViewModel.Instance.ComponentStat_Opacity = opacity;
                    break;
                case "NetworkStatistics":
                    ViewModel.Instance.NetworkStats_Opacity = opacity;
                    break;
                case "MediaLink":
                    ViewModel.Instance.MediaLink_Opacity = opacity;
                    break;
                default:
                    break;
            }
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
    }
}
