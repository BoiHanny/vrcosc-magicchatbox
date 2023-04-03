using vrcosc_magicchatbox.ViewModels;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System;
using vrcosc_magicchatbox.Classes.DataAndSecurity;

namespace vrcosc_magicchatbox.Classes
{
    public static class SpotifyActivity
    {
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
                        return "";
                    }
                }
                return "No music";
                ViewModel.Instance.SpotifyPaused = true;
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
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
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
                return false;
            }

        }

    }
}
