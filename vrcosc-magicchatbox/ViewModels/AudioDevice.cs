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
        public string Name { get; set; }
        public string Id { get; set; }
        public int DataFlow { get; set; }

        public AudioDevice(string name, string id, int dataFlow)
        {
            Name = name;
            Id = id;
            DataFlow = dataFlow;
        }
    }
}
