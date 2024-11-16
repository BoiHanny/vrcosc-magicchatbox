using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Media;
using vrcosc_magicchatbox.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.Models;
using static vrcosc_magicchatbox.Classes.Modules.MediaLinkModule;
using static WindowsMediaController.MediaManager;

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
                if(DataController.networkStatisticsModule == null)
                {
                    return;
                }
                // create x string based on the values in MainWindow.networkStatsModule make it all look nice and pretty
                string x = DataController.networkStatisticsModule.GenerateDescription();
                if(string.IsNullOrEmpty(x))
                {
                    return;
                }
                TryAddToUncomplete(Uncomplete, x, "NetworkStatistics");
            }
        }


        // this function will build the heart rate message to be sent to VRChat and add it to the list of strings if the total length of the list is less than 144 characters
        public static void AddHeartRate(List<string> Uncomplete)
        {
            if (ViewModel.Instance.IntgrHeartRate)
            {
                string output = ViewModel.Instance.HeartRateConnector.GetHeartRateString();
                if (string.IsNullOrEmpty(output))
                {
                    return;
                }
                TryAddToUncomplete(Uncomplete, output, "HeartRate");
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

                                if (!mediaSession.IsLiveTime && mediaSession.TimePeekEnabled)
                                {
                                    x = CreateTimeStamp(x, mediaSession, ViewModel.Instance.MediaLinkTimeSeekStyle, Uncomplete);
                                }
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



        private static string CreateTimeStamp(string x, MediaSessionInfo mediaSession, MediaLinkTimeSeekbar style, List<string> uncomplete)
        {
            TimeSpan currentTime = mediaSession.CurrentTime;
            TimeSpan fullTime = mediaSession.FullTime;

            // Validate times
            if (currentTime.TotalSeconds < 0 || fullTime.TotalSeconds < 0 || currentTime > fullTime)
            {
                return x;
            }

            double percentage = fullTime.TotalSeconds == 0 ? 0 : (currentTime.TotalSeconds / fullTime.TotalSeconds) * 100;
            int availableSpace = 140 - CalculateOSCMsgLength(uncomplete, x);
            var selectedStyle = ViewModel.Instance.SelectedMediaLinkSeekbarStyle;

            switch (style)
            {
                case MediaLinkTimeSeekbar.NumbersAndSeekBar:
                    string progressBarContent = CreateProgressBar(percentage, mediaSession, selectedStyle);
                    if (!string.IsNullOrWhiteSpace(progressBarContent) && CalculateOSCMsgLength(uncomplete, x + "\n" + progressBarContent) <= 144)
                    {
                        return ViewModel.Instance.SelectedMediaLinkSeekbarStyle.ProgressBarOnTop ? progressBarContent + "\n" + x : x + "\n" + progressBarContent;
                    }
                    else if (ViewModel.Instance.AutoDowngradeSeekbar)
                    {
                        goto case MediaLinkTimeSeekbar.SmallNumbers;
                    }
                    break;

                case MediaLinkTimeSeekbar.SmallNumbers:
                    string smallNumbersContent = $"{DataController.TransformToSuperscript(FormatTimeSpan(currentTime))} l {DataController.TransformToSuperscript(FormatTimeSpan(fullTime))}";
                    if (CalculateOSCMsgLength(uncomplete, x + smallNumbersContent) <= 144)
                    {
                        return x + " " + smallNumbersContent;
                    }
                    else if (ViewModel.Instance.AutoDowngradeSeekbar)
                    {
                        goto case MediaLinkTimeSeekbar.None;
                    }
                    break;

                case MediaLinkTimeSeekbar.None:
                default:
                    return x;
            }

            return x;
        }

        private static string CreateProgressBar(double percentage, MediaSessionInfo mediaSession, MediaLinkStyle style)
        {
            try
            {
                // Ensure characters are not empty or null before accessing
                if (string.IsNullOrEmpty(style.FilledCharacter) ||
                    string.IsNullOrEmpty(style.MiddleCharacter) ||
                    string.IsNullOrEmpty(style.NonFilledCharacter))
                {
                    return string.Empty;
                }

                int totalBlocks = style.ProgressBarLength;

                string currentTime = style.DisplayTime && style.ShowTimeInSuperscript
                    ? DataController.TransformToSuperscript(FormatTimeSpan(mediaSession.CurrentTime))
                    : FormatTimeSpan(mediaSession.CurrentTime);

                string fullTime = style.DisplayTime && style.ShowTimeInSuperscript
                    ? DataController.TransformToSuperscript(FormatTimeSpan(mediaSession.FullTime))
                    : FormatTimeSpan(mediaSession.FullTime);

                if (style.DisplayTime && totalBlocks > 0)
                {
                    int timeStringLength = currentTime.Length + fullTime.Length + 1;

                    if (totalBlocks > timeStringLength)
                    {
                        totalBlocks -= timeStringLength;
                    }
                }

                int filledBlocks = (int)(percentage / (100.0 / totalBlocks));

                // Use the entire string for emojis and multi-character strings
                string filledBar = string.Concat(Enumerable.Repeat(style.FilledCharacter, filledBlocks));
                string emptyBar = string.Concat(Enumerable.Repeat(style.NonFilledCharacter, totalBlocks - filledBlocks));
                string progressBar = filledBar + style.MiddleCharacter + emptyBar;

                return CreateTimeStamp(currentTime, fullTime, progressBar, style);
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
                return string.Empty;
            }
        }

        private static string CreateTimeStamp(string currentTime, string fullTime, string progressBar, MediaLinkStyle style)
        {
            string space = style.SpaceAgainObjects ? " " : string.Empty;
            string preSuffixSpace = style.SpaceBetweenPreSuffixAndTime ? " " : string.Empty;

            if (style.DisplayTime)
            {
                if (style.TimePreSuffixOnTheInside)
                {
                    return string.IsNullOrWhiteSpace(style.TimePrefix) || string.IsNullOrWhiteSpace(style.TimeSuffix) ?
                        $"{currentTime}{space}{progressBar}{space}{fullTime}" :
                        $"{currentTime}{preSuffixSpace}{style.TimePrefix}{progressBar}{style.TimeSuffix}{preSuffixSpace}{fullTime}";
                }
                else
                {
                    return $"{style.TimePrefix}{preSuffixSpace}{currentTime}{space}{progressBar}{space}{fullTime}{preSuffixSpace}{style.TimeSuffix}";
                }
            }
            else
            {
                return style.TimePreSuffixOnTheInside ?
                    $"{style.TimePrefix}{progressBar}{style.TimeSuffix}" :
                    $"{style.TimePrefix}{preSuffixSpace}{progressBar}{preSuffixSpace}{style.TimeSuffix}";
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
            if(ViewModel.Instance.AfkModule != null && ViewModel.Instance.AfkModule.IsAfk && ViewModel.Instance.AfkModule.Settings.EnableAfkDetection)
            {
                string x = ViewModel.Instance.AfkModule.GenerateAFKString();
                TryAddToUncomplete(Uncomplete, x, "Status");
                return;
            }
            if (ViewModel.Instance.IntgrStatus == true && ViewModel.Instance.StatusList.Count() != 0)
            {
                // Cycle status if enabled
                if (ViewModel.Instance.CycleStatus)
                {
                    CycleStatus();
                }

                StatusItem? activeItem = ViewModel.Instance.StatusList.FirstOrDefault(item => item.IsActive == true);
                if (activeItem != null)
                {
                    // Update LastUsed property for the active item
                    activeItem.LastUsed = DateTime.Now;

                    string icon = ViewModel.Instance.GetNextEmoji();
                    if (ViewModel.Instance.IzuruBaeMode && !string.IsNullOrWhiteSpace(ViewModel.Instance.EggPrefixIconStatus))
                    {
                        icon = ViewModel.Instance.EggPrefixIconStatus.Substring(0, char.IsSurrogatePair(ViewModel.Instance.EggPrefixIconStatus, 0) ? 2 : 1);
                    }

                    string? x = ViewModel.Instance.PrefixIconStatus == true
                        ? $"{icon} " + activeItem.msg
                        : activeItem.msg;

                    if (x != null)
                    {
                        TryAddToUncomplete(Uncomplete, x, "Status");
                    }
                }
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
                    x.Append(ViewModel.Instance.WindowActivityVRTitle);
                    if (ViewModel.Instance.IntgrScanForce)
                    {
                        x.Append($" {ViewModel.Instance.WindowActivityVRFocusTitle} {ViewModel.Instance.FocusedWindow}");
                    }
                }
                else
                {
                    x.Append(ViewModel.Instance.WindowActivityDesktopTitle);

                    if (ViewModel.Instance.WindowActivityShowFocusedApp)
                    {
                        x.Append($" {ViewModel.Instance.WindowActivityDesktopFocusTitle} {ViewModel.Instance.FocusedWindow}");
                    }
                }

                TryAddToUncomplete(Uncomplete, x.ToString(), "Window");
            }
        }

        public static void CycleStatus()
        {
            if (ViewModel.Instance == null || ViewModel.Instance.StatusList == null || !ViewModel.Instance.StatusList.Any())
                return;

            var cycleItems = ViewModel.Instance.StatusList.Where(item => item.UseInCycle).ToList();
            if (cycleItems.Count == 0) return;

            TimeSpan elapsedTime = DateTime.Now - ViewModel.Instance.LastSwitchCycle;

            if (elapsedTime >= TimeSpan.FromSeconds(ViewModel.Instance.SwitchStatusInterval))
            {



                if(ViewModel.Instance.IsRandomCycling)
                {
                    foreach (var item in ViewModel.Instance.StatusList)
                    {
                        item.IsActive = false;
                    }
                    try
                    {
                        var rnd = new Random();
                        var weights = cycleItems.Select(item =>
                        {
                            var timeWeight = (DateTime.Now - item.LastUsed).TotalSeconds;
                            var randomFactor = rnd.NextDouble(); // Adding randomness
                            return timeWeight * randomFactor; // Combine time weight with random factor
                        }).ToList();

                        int selectedIndex = WeightedRandomIndex(weights);
                        cycleItems[selectedIndex].IsActive = true;

                        ViewModel.Instance.LastSwitchCycle = DateTime.Now;
                    }
                    catch (Exception ex)
                    {
                        Logging.WriteException(ex, MSGBox: false);
                    }
                }
                else
                {
                    var activeItem = cycleItems.FirstOrDefault(item => item.IsActive);
                    if (activeItem != null)
                    {
                        var currentIndex = cycleItems.IndexOf(activeItem);
                        var nextIndex = currentIndex + 1;
                        if (nextIndex >= cycleItems.Count)
                        {
                            nextIndex = 0;
                        }

                        activeItem.IsActive = false;
                        cycleItems[nextIndex].IsActive = true;

                        ViewModel.Instance.LastSwitchCycle = DateTime.Now;
                    }
                }

            }
        }


        private static int WeightedRandomIndex(List<double> weights)
        {
            Random rnd = new Random();
            double totalWeight = weights.Sum();
            double randomPoint = rnd.NextDouble() * totalWeight;

            for (int i = 0; i < weights.Count; i++)
            {
                if (randomPoint < weights[i])
                    return i;
                randomPoint -= weights[i];
            }

            return weights.Count - 1;
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
                    !ViewModel.Instance.IsVRRunning || ViewModel.Instance.AfkModule.IsAfk && ViewModel.Instance.AfkModule.Settings.EnableAfkDetection,
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
                    () => ViewModel.Instance.IntgrSoundpad_VR &&
                    ViewModel.Instance.IsVRRunning ||
                    ViewModel.Instance.IntgrSoundpad_DESKTOP &&
                    !ViewModel.Instance.IsVRRunning,
                    AddSoundpad
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
                SetOpacity("Soundpad", "1");
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
                Logging.WriteException(ex, MSGBox: false);
            }

            // Join the list of strings into one string and set the OSCtoSent property in the ViewModel to the final OSC message
            if (ViewModel.Instance.SeperateWithENTERS)
            {
                Complete_msg = string.Join("\n", Uncomplete);
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

        private static void AddSoundpad(List<string> list)
        {
            if(ViewModel.Instance.IntgrSoundpad)
            {
                string playingSong = $"{ViewModel.Instance.SoundpadModule.GetPlayingSong()}";

                if (string.IsNullOrEmpty(ViewModel.Instance.SoundpadModule.GetPlayingSong()))
                {
                    return;
                }

                string x = ViewModel.Instance.PrefixIconSoundpad == true
                    ? "🎶 " + $"'{playingSong}'"
                    : $"'{playingSong}'";
                TryAddToUncomplete(list, x, "Soundpad");
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
                    string icon = ViewModel.Instance.GetNextEmoji(true);
                    if (ViewModel.Instance.IzuruBaeMode && !string.IsNullOrWhiteSpace(ViewModel.Instance.EggPrefixIconStatus))
                    {
                        icon = ViewModel.Instance.EggPrefixIconStatus.Substring(0, char.IsSurrogatePair(ViewModel.Instance.EggPrefixIconStatus, 0) ? 2 : 1);
                    }
                    Complete_msg = icon + " " + ViewModel.Instance.NewChattingTxt;
                }
                else
                {
                    Complete_msg = ViewModel.Instance.NewChattingTxt;
                }

                if (Complete_msg.Length == 0)
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
                Logging.WriteException(ex, MSGBox: false);
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
                case "Status":
                    ViewModel.Instance.Status_Opacity = opacity;
                    break;
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
                case "Soundpad":
                    ViewModel.Instance.Soundpad_Opacity = opacity;
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
            if (CalculateOSCMsgLength(uncomplete, x) < 144 && !string.IsNullOrEmpty(x))
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
