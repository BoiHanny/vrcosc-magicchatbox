using System;
using vrcosc_magicchatbox.ViewModels;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vrcosc_magicchatbox.Classes
{
    public class SpotifyActivity
    {
        private ViewModel _VM;
        public SpotifyActivity(ViewModel vm)
        {
            _VM = vm;
        }

        public string CurrentPlayingSong()
        {
                var procs = Process.GetProcessesByName("Spotify");

                foreach (var p in procs)
                {

                if(!p.MainWindowTitle.StartsWith("Spotify"))
                    return  p.MainWindowTitle;
                else
                {
                    return "Music Paused";
                }
                }
                return "No music";
        }

        public bool SpotifyIsRunning()
        {
            if (Process.GetProcessesByName("Spotify").Length > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
            
        }

    }
}
