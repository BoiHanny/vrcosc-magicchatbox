using System.Collections.Generic;
using OpenVR.NET;

namespace vrcosc_magicchatbox.Classes.Modules
{
    class SteamVRModule
    {
        private VR VR;
        List<uint> connectedTrackers = new();

        public SteamVRModule()
        {
            VR = new VR();
            VR.TryStart();
        }

        public List<uint> GetConnectedTrackers()
        {
            
            foreach (var device in VR.ActiveDevices)
            {
                if (device.IsEnabled)
                    connectedTrackers.Add(device.DeviceIndex);
            }
            return connectedTrackers;
        }

        
    }
}
