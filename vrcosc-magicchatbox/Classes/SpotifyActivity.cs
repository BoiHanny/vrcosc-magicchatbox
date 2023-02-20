using vrcosc_magicchatbox.ViewModels;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System;

namespace vrcosc_magicchatbox.Classes
{
    public class SpotifyActivity
    {
        private ViewModel _VM;
        //[DllImport("user32.dll")]
        //public static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, IntPtr extraInfo);

        //public const int KEYEVENTF_EXTENTEDKEY = 1;
        //public const int KEYEVENTF_KEYUP = 0;
        //public const int VK_MEDIA_NEXT_TRACK = 0xB0;// code to jump to next track
        //public const int VK_MEDIA_PLAY_PAUSE = 0xB3;// code to play or pause a song
        //public const int VK_MEDIA_PREV_TRACK = 0xB1;// code to jump to prev track

        public SpotifyActivity(ViewModel vm)
        {
            _VM = vm;
        }

        //public void ControlMedia(int mode)
        //{
        //    if (mode == 0)
        //        keybd_event(VK_MEDIA_PREV_TRACK, 0, KEYEVENTF_EXTENTEDKEY, IntPtr.Zero);
        //    else if (mode == 1)
        //        keybd_event(VK_MEDIA_PLAY_PAUSE, 0, KEYEVENTF_EXTENTEDKEY, IntPtr.Zero);
        //    else if (mode == 2)
        //        keybd_event(VK_MEDIA_NEXT_TRACK, 0, KEYEVENTF_EXTENTEDKEY, IntPtr.Zero);
        //}


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
