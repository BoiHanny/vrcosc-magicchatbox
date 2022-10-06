using vrcosc_magicchatbox.ViewModels;
using SharpOSC;

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
                oscSender = new(_VM.OSCIP, _VM.OSCPort);
                oscSender.Send(new OscMessage("/chatbox/typing", false));
                oscSender.Send(new OscMessage("/chatbox/input", _VM.OSCtoSent, true));
            }
            catch (System.Exception)
            {
            }

        }

        public void BuildOSC()
        {
            string msg = "";
            if (_VM.IntgrScanWindowActivity == true)
            {
                if (_VM.IsVRRunning)
                {
                    msg = "In VR |";
                    if (_VM.IntgrScanWindowTime == true)
                        msg = msg + " My time: " + _VM.CurrentTIme + " |";
                }
                else
                {
                    msg = "On desktop in '" + _VM.FocusedWindow + "' |";
                }
            }
            if (_VM.IntgrScanSpotify)
            {
                if (_VM.SpotifyActive)
                    if (_VM.SpotifyPaused)
                    {
                        if (_VM.IsVRRunning == true)
                        {
                            msg = msg + "   Music is paused";
                        }
                        else
                        {
                            msg = msg + " Music is paused";
                        }

                    }
                    else
                    {
                        msg = msg + " Listening to '" + _VM.PlayingSongTitle + "'";
                    }
                ;
            }
            if (msg.Length > 0)
            {
                _VM.OSCtoSent = msg;
            }

        }
    }
}
