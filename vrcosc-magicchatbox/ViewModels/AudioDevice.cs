using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vrcosc_magicchatbox.ViewModels
{
    public class AudioDevice
    {
        public string FriendlyName { get; set; }
        public string ID { get; set; }
        public int DeviceNumber { get; set; }

        public AudioDevice(string friendlyName, string id, int deviceNumber)
        {
            FriendlyName = friendlyName;
            ID = id;
            DeviceNumber = deviceNumber;
        }
    }
}
