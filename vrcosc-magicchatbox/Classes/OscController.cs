using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        public void SentOSCMessage(string message)
        {



        }

    }
}
