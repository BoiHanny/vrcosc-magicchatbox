using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using vrcosc_magicchatbox.ViewModels.Models;

namespace vrcosc_magicchatbox.Classes.DataAndSecurity
{
    public class AvatarSyncController
    {
        public static void RunSync(OSCParameter osctask)
        {
            if (osctask.IsBuiltIn == true)
            {
                if (osctask.Name == "AvatarChange")
                {
                    OSCParameter parameter = OSCParameters.GetParameter("AvatarChange");
                    OSCAvatar oSCAvatar = new OSCAvatar(parameter.GetLatestValue().ToString());
                }
            }
            else
            {
            }
        }
    }
}
