using vrcosc_magicchatbox.ViewModels;
using SharpOSC;
using System.Collections.Generic;
using System;

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
                if(_VM.OSCtoSent.Length > 0)
                {
                    oscSender = new(_VM.OSCIP, _VM.OSCPort);
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
            string x = String.Join(" | ", list);
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
                        _VM.Char_Limit = "Visable";
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
                        _VM.Char_Limit = "Visable";
                        _VM.Window_Opacity = "0.5";
                    }             
                }
            }
            if (_VM.IntgrScanWindowTime == true)
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
                    _VM.Char_Limit = "Visable";
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
                            _VM.Char_Limit = "Visable";
                            _VM.Spotify_Opacity = "0.5";
                        }
                    }

                    else
                    {
                        x = "Listening to '" + _VM.PlayingSongTitle + "'";
                        if (OSCmsgLenght(Uncomplete, x) < 144)
                        {
                            Uncomplete.Add(x);
                        }
                        else
                        {
                            _VM.Char_Limit = "Visable";
                            _VM.Spotify_Opacity = "0.5";
                        }
                        
                    
                    }
                }
            }
            if (Uncomplete.Count > 0)
            {

                Complete_msg = String.Join(" | ", Uncomplete);
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
    }
}
