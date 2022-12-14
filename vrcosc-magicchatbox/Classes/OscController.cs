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

        public void BuildOSC()
        {
            var Complete_msg = "";
            List<string> Uncomplete = new List<string>();

            if (_VM.IntgrScanWindowActivity == true)
            {
                if (_VM.IsVRRunning)
                { Uncomplete.Add("In VR"); }
                else
                { Uncomplete.Add("On desktop in '" + _VM.FocusedWindow); }
            }
            if (_VM.IntgrScanWindowTime == true & _VM.IsVRRunning == true)
            { Uncomplete.Add("My time: " + _VM.CurrentTIme); }
            if (_VM.IntgrScanSpotify == true)
            {
                if (_VM.SpotifyActive == true)
                {
                    if (_VM.SpotifyPaused)
                    { Uncomplete.Add("Music paused"); }
                    else
                    { Uncomplete.Add("Listening to '" + _VM.PlayingSongTitle + "'"); }
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
            else { _VM.OSCtoSent = ""; }


        }
    }
}
