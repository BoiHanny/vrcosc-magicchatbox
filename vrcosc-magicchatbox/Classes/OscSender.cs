using vrcosc_magicchatbox.ViewModels;
using CoreOSC;
using System.Collections.Generic;
using System;
using System.Text;
using System.Linq;
using System.Globalization;
using System.Collections.ObjectModel;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using System.Threading;

namespace vrcosc_magicchatbox.Classes
{
    public static class OscSender
    {

        public static UDPSender oscSender;

        public static void SendOSCMessage(bool FX)
        {

            if (ViewModel.Instance.MasterSwitch == true)
            {
                try
                {
                    if (ViewModel.Instance.OSCtoSent.Length > 0 && ViewModel.Instance.OSCtoSent.Length <= 144)
                    {
                        oscSender = new(ViewModel.Instance.OSCIP, ViewModel.Instance.OSCPortOut);
                        oscSender.Send(new OscMessage("/chatbox/input", ViewModel.Instance.OSCtoSent, true, FX));
                    }
                }
                catch (Exception ex)
                {
                    Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
                }
            }

        }


        public static void ToggleVoice(bool force = false)
        {
            if (ViewModel.Instance.MasterSwitch && ViewModel.Instance.AutoUnmuteTTS || force)
            {
                try
                {
                    oscSender = new(ViewModel.Instance.OSCIP, ViewModel.Instance.OSCPortOut);
                    oscSender.Send(new OscMessage("/input/Voice", 1));
                    Thread.Sleep(600);
                    oscSender.Send(new OscMessage("/input/Voice", 0));
                }
                catch (Exception ex)
                {
                    Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
                }
            }
        }

        public static void TypingIndicator(bool Typing)
        {
            if (ViewModel.Instance.MasterSwitch == true)
            {
                try
                {
                    ViewModel.Instance.TypingIndicator = Typing;
                    oscSender = new(ViewModel.Instance.OSCIP, ViewModel.Instance.OSCPortOut);
                    oscSender.Send(new OscMessage("/chatbox/typing", Typing));
                }
                catch (Exception ex)
                {
                    Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
                }
            }
        }


        public static int OSCmsgLenght(List<string> content, string add)
        {
            List<string> list = new List<string>(content);
            list.Add(add);

            byte[] utf8Bytes = Encoding.UTF8.GetBytes(String.Join(" | ", list));
            string x = Encoding.UTF8.GetString(utf8Bytes);

            return x.Length;
        }

        public static void BuildOSC()
        {
            try
            {
                ViewModel.Instance.Char_Limit = "Hidden";
                ViewModel.Instance.Spotify_Opacity = "1";
                ViewModel.Instance.Window_Opacity = "1";
                ViewModel.Instance.Time_Opacity = "1";

                string x = null;
                var Complete_msg = "";
                List<string> Uncomplete = new List<string>();
                if (ViewModel.Instance.IntgrStatus == true && ViewModel.Instance.StatusList.Count() != 0)
                {
                    if (ViewModel.Instance.PrefixIconStatus == true)
                    {
                        x = "💬 " + ViewModel.Instance.StatusList.FirstOrDefault(item => item.IsActive == true)?.msg;
                    }
                    else
                    {
                        x = ViewModel.Instance.StatusList.FirstOrDefault(item => item.IsActive == true)?.msg;
                    }

                    if (OSCmsgLenght(Uncomplete, x) < 144)
                    {
                        Uncomplete.Add(x);
                    }
                    else
                    {
                        ViewModel.Instance.Char_Limit = "Visible";
                        ViewModel.Instance.Window_Opacity = "0.5";
                    }
                }
                if (ViewModel.Instance.IntgrScanWindowActivity == true)
                {
                    if (ViewModel.Instance.IsVRRunning)
                    {
                        x = "In VR";
                        if (OSCmsgLenght(Uncomplete, x) < 144)
                        {
                            Uncomplete.Add(x);
                        }
                        else
                        {
                            ViewModel.Instance.Char_Limit = "Visible";
                            ViewModel.Instance.Window_Opacity = "0.5";
                        }



                    }
                    else
                    {
                        x = "On desktop in '" + ViewModel.Instance.FocusedWindow + "'";
                        if (OSCmsgLenght(Uncomplete, x) < 144)
                        {
                            Uncomplete.Add(x);
                        }
                        else
                        {
                            ViewModel.Instance.Char_Limit = "Visible";
                            ViewModel.Instance.Window_Opacity = "0.5";
                        }
                    }
                }
                if (ViewModel.Instance.IntgrScanWindowTime == true & ViewModel.Instance.OnlyShowTimeVR == true & ViewModel.Instance.IsVRRunning == true | ViewModel.Instance.IntgrScanWindowTime == true & ViewModel.Instance.OnlyShowTimeVR == false)
                {
                    if (ViewModel.Instance.PrefixTime == true)
                    {
                        x = "My time: ";
                    }
                    else
                    {
                        x = "";
                    }
                    x = x + ViewModel.Instance.CurrentTime;


                    if (OSCmsgLenght(Uncomplete, x) < 144)
                    {
                        Uncomplete.Add(x);
                    }
                    else
                    {
                        ViewModel.Instance.Char_Limit = "Visible";
                        ViewModel.Instance.Time_Opacity = "0.5";
                    }


                }
                if (ViewModel.Instance.IntgrScanSpotify == true)
                {
                    if (ViewModel.Instance.SpotifyActive == true)
                    {
                        if (ViewModel.Instance.SpotifyPaused)
                        {
                            x = "";
                            if (ViewModel.Instance.PauseIconMusic == true && ViewModel.Instance.PrefixIconMusic == true)
                            {
                                x = "⏸";
                            }
                            else
                            {
                                x = "Music paused";
                            }
                            if (OSCmsgLenght(Uncomplete, x) < 144)
                            {
                                Uncomplete.Add(x);
                            }
                            else
                            {
                                ViewModel.Instance.Char_Limit = "Visible";
                                ViewModel.Instance.Spotify_Opacity = "0.5";
                            }
                        }

                        else
                        {
                            if (ViewModel.Instance.PrefixIconMusic == true)
                            {
                                x = "🎵 '" + ViewModel.Instance.PlayingSongTitle + "'";
                            }
                            else
                            {
                                x = "Listening to '" + ViewModel.Instance.PlayingSongTitle + "'";
                            }

                            if (OSCmsgLenght(Uncomplete, x) < 144)
                            {
                                Uncomplete.Add(x);
                            }
                            else
                            {
                                ViewModel.Instance.Char_Limit = "Visible";
                                ViewModel.Instance.Spotify_Opacity = "0.5";
                            }


                        }
                    }
                }
                if (Uncomplete.Count > 0)
                {

                    Complete_msg = String.Join(" ┆ ", Uncomplete);
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
                else
                {
                    ViewModel.Instance.OSCmsg_count = ViewModel.Instance.OSCtoSent.Length;
                    ViewModel.Instance.OSCmsg_countUI = ViewModel.Instance.OSCtoSent.Length + "/144";
                    ViewModel.Instance.OSCtoSent = "";
                }
            }
            catch (Exception ex)
            {

                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
            }
            


        }
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
