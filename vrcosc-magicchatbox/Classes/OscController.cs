using vrcosc_magicchatbox.ViewModels;
using CoreOSC;
using System.Collections.Generic;
using System;
using System.Text;
using System.Linq;
using System.Globalization;
using System.Collections.ObjectModel;

namespace vrcosc_magicchatbox.Classes
{
    public class OscController
    {

        private ViewModel _VM;
        public OscController(ViewModel vm)
        {
            _VM = vm;
        }

        public UDPSender oscSender;

        public void SentOSCMessage()
        {
            try
            {
                if(_VM.OSCtoSent.Length > 0 || _VM.OSCtoSent.Length < 144)
                {
                        oscSender = new(_VM.OSCIP, _VM.OSCPortOut);
                        oscSender.Send(new OscMessage("/chatbox/typing", false));
                        oscSender.Send(new OscMessage("/chatbox/input", _VM.OSCtoSent, true));
                }
                else
                {

                }
                
            }
            catch (System.Exception)
            {
            }

        }

        public int OSCmsgLenght(List<string> content, string add)
        {
            List<string> list = new List<string>(content);
            list.Add(add);

            byte[] utf8Bytes = Encoding.UTF8.GetBytes(String.Join(" | ", list));
            string x = Encoding.UTF8.GetString(utf8Bytes);

            return x.Length;
        }

        public void BuildOSC()
        {
            _VM.Char_Limit = "Hidden";
            _VM.Spotify_Opacity = "1";
            _VM.Window_Opacity = "1";
            _VM.Time_Opacity = "1";

            string x = null;
            var Complete_msg = "";
            List<string> Uncomplete = new List<string>();
            if(_VM.IntgrStatus == true && _VM.StatusList.Count() != 0)
            {
                if (_VM.PrefixIconStatus == true)
                {
                    x = "💬 " + _VM.StatusList.FirstOrDefault(item => item.IsActive == true)?.msg;
                }
                else
                {
                    x = _VM.StatusList.FirstOrDefault(item => item.IsActive == true)?.msg;
                }

                if (OSCmsgLenght(Uncomplete, x) < 144)
                {
                    Uncomplete.Add(x);
                }
                else
                {
                    _VM.Char_Limit = "Visible";
                    _VM.Window_Opacity = "0.5";
                }
            }
            if (_VM.IntgrScanWindowActivity == true)
            {
                if (_VM.IsVRRunning)
                {
                    x = "In VR";
                    if(OSCmsgLenght(Uncomplete, x) < 144)
                    {
                        Uncomplete.Add(x);
                    }
                    else
                    {
                        _VM.Char_Limit = "Visible";
                        _VM.Window_Opacity = "0.5";
                    }


                    
                }
                else
                {
                    x = "On desktop in '" + _VM.FocusedWindow + "'";
                    if (OSCmsgLenght(Uncomplete, x) < 144)
                    {
                        Uncomplete.Add(x);
                    }
                    else
                    {
                        _VM.Char_Limit = "Visible";
                        _VM.Window_Opacity = "0.5";
                    }             
                }
            }
            if (_VM.IntgrScanWindowTime == true & _VM.OnlyShowTimeVR == true & _VM.IsVRRunning == true | _VM.IntgrScanWindowTime == true & _VM.OnlyShowTimeVR == false)
            {
                if(_VM.PrefixTime == true)
                {
                    x = "My time: ";
                }
                else
                {
                    x = "";
                }
                x = x + _VM.CurrentTime;


                if (OSCmsgLenght(Uncomplete, x) < 144)
                {
                    Uncomplete.Add(x);
                }
                else
                {
                    _VM.Char_Limit = "Visible";
                    _VM.Time_Opacity = "0.5";
                }


            }
            if (_VM.IntgrScanSpotify == true)
            {
                if (_VM.SpotifyActive == true)
                {
                    if (_VM.SpotifyPaused)
                    {
                        x = "Music paused";
                        if(OSCmsgLenght(Uncomplete, x) < 144)
                        {
                            Uncomplete.Add(x);
                        }
                        else
                        {
                            _VM.Char_Limit = "Visible";
                            _VM.Spotify_Opacity = "0.5";
                        }
                    }

                    else
                    {                    
                        if (_VM.PrefixIconMusic == true)
                        {
                            x = "🎵 '" + _VM.PlayingSongTitle + "'";
                        }
                        else
                        {
                            x = "Listening to '" + _VM.PlayingSongTitle + "'";
                        }

                        if (OSCmsgLenght(Uncomplete, x) < 144)
                        {
                            Uncomplete.Add(x);
                        }
                        else
                        {
                            _VM.Char_Limit = "Visible";
                            _VM.Spotify_Opacity = "0.5";
                        }
                        
                    
                    }
                }
            }
            if (Uncomplete.Count > 0)
            {

                Complete_msg = String.Join(" ┆ ", Uncomplete);
                if (Complete_msg.Length > 144)
                {
                    _VM.OSCtoSent = "";
                    _VM.OSCmsg_count = Complete_msg.Length;
                    _VM.OSCmsg_countUI = "MAX/144";
                }
                else
                {
                    _VM.OSCtoSent = Complete_msg;
                    _VM.OSCmsg_count = _VM.OSCtoSent.Length;
                    _VM.OSCmsg_countUI = _VM.OSCtoSent.Length + "/144";
                }
            }
            else 
            {
                _VM.OSCmsg_count = _VM.OSCtoSent.Length;
                _VM.OSCmsg_countUI = _VM.OSCtoSent.Length + "/144";
                _VM.OSCtoSent = ""; 
            }

            
        }
        public void CreateChat()
        {
            string Complete_msg = null;
            if (_VM.PrefixChat == true)
            {
                Complete_msg = "💬 " + _VM.NewChattingTxt;
            }
            else
            {
                Complete_msg = _VM.NewChattingTxt;
            }

            if(Complete_msg.Length < 4)
            {

            }
            else if (Complete_msg.Length > 144)
            {

            }
            else
            {
                _VM.ScanPauseCountDown = _VM.ScanPauseTimeout;
                _VM.ScanPause = true;
                _VM.OSCtoSent = Complete_msg;
                _VM.OSCmsg_count = _VM.OSCtoSent.Length;
                _VM.OSCmsg_countUI = _VM.OSCtoSent.Length + "/144";
                _VM.ActiveChatTxt = "Active";

                var newChatItem = new ChatItem { Msg = _VM.NewChattingTxt, CreationDate = DateTime.Now };
                _VM.LastMessages.Add(newChatItem);

                // Remove the oldest message if the list has more than 5 items
                if (_VM.LastMessages.Count > 5)
                {
                    _VM.LastMessages.RemoveAt(0);
                }

                // Update the opacities of the chat items
                double opacity = 1;
                foreach (var item in _VM.LastMessages.Reverse())
                {
                    opacity -= 0.18;
                    item.Opacity = opacity.ToString("F1", CultureInfo.InvariantCulture);
                }

                // Save the current list of chat items to a local observable collection and clear the original list
                var currentList = new ObservableCollection<ChatItem>(_VM.LastMessages);
                _VM.LastMessages.Clear();

                // Replace the original list with the new list
                foreach (var item in currentList)
                {
                    _VM.LastMessages.Add(item);
                }
                _VM.NewChattingTxt = "";
            }
        }

        internal void ClearChat()
        {
            _VM.ScanPause = false;
            _VM.OSCtoSent = "";
            _VM.OSCmsg_count = 0;
            _VM.OSCmsg_countUI = "0/144";
            _VM.ActiveChatTxt = "";
        }
    }
}
