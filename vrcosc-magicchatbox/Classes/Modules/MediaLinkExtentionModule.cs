using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.Modules
{
    public static class SpotifyModule
    {
        // this function gets the application name of the current active window
        public static string CurrentPlayingSong()
        {
            try
            {
                var procs = Process.GetProcessesByName("Spotify");

                foreach (var p in procs)
                {
                    string title = p.MainWindowTitle;
                    if (!p.MainWindowTitle.StartsWith("Spotify"))
                    {
                        ViewModel.Instance.SpotifyPaused = false;
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
                        ViewModel.Instance.SpotifyPaused = true;
                        return string.Empty;
                    }

                }
                return "No music";

            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
                return "Oop, an exception did happen...";
            }
        }
        public static bool SpotifyIsRunning()
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
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
                return false;
            }

        }
    }
}
