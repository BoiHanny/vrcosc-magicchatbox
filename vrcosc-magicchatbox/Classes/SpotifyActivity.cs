using vrcosc_magicchatbox.ViewModels;
using System.Diagnostics;
using System;
using System.Text.RegularExpressions;

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
            try
            {
                var procs = Process.GetProcessesByName("Spotify");

                foreach (var p in procs)
                {
                    string title = p.MainWindowTitle;
                    if (!p.MainWindowTitle.StartsWith("Spotify"))
                    {
                        _VM.SpotifyPaused = false;
                        Match match = Regex.Match(title, @"(.+?) - (.+)");
                        if (match.Success)
                        {
                            string artist = match.Groups[1].Value;
                            string song = match.Groups[2].Value;
                            return $"{song} ᵇʸ {artist}";
                        }
                        else
                        {
                            return title;
                        }
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
            catch (System.Exception)
            {
                return "Oop, an exception did happen...";
            }
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
