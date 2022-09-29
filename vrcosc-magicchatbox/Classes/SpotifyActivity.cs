
using vrcosc_magicchatbox.ViewModels;
using System.Diagnostics;

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
