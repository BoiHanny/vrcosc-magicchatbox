using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace vrcosc_magicchatbox.ViewModels
{
    public class OSCAvatar
    {
        string avatarID { get; set; }   
        string avatarName { get; set; } = string.Empty;

        DateTime firstDetected { get; set; }

        DateTime lastDetected { get; set; }

        bool isOnline { get; set; } = false;
        bool isSelected { get; set; } = false;

        List<string> linkedOSCParameter { get; set; } = new List<string>();

        public OSCAvatar(string avataris)
        {
            avatarID = avataris;
            firstDetected = DateTime.Now;
            lastDetected = DateTime.Now;
        }

    }
}
