using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vrcosc_magicchatbox.ViewModels
{
    public class Version
    {
        public string VersionNumber { get; set; }
        public string ReleaseDate { get; set; }
        public string ReleaseNotes { get; set; }

        public Version(string version)
        {
            VersionNumber = version;
            ReleaseDate = "";
            ReleaseNotes = "";
        }

    }
}
