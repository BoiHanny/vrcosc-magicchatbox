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

                if (!p.MainWindowTitle.StartsWith("Spotify"))
                {
                    _VM.SpotifyPaused = false;
                    return p.MainWindowTitle;
                }
                else
                {
                    _VM.SpotifyPaused = true;
                    return "";
                }
            }
                return "No music";
            _VM.SpotifyPaused = true;
        }

        public bool SpotifyIsRunning()
        {
            try
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
            catch (System.Exception)
            {
                return false;
            }

        }

    }
}
