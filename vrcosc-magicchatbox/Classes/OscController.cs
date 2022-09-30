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
            oscSender = new(_VM.OSCIP, _VM.OSCPort);
            oscSender.Send(new OscMessage("/chatbox/typing", false));
            oscSender.Send(new OscMessage("/chatbox/input", _VM.OSCtoSent, true));
        }


    }
}
