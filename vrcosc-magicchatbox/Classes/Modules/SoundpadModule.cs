using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Timers;

namespace vrcosc_magicchatbox.Classes.Modules
{
    internal class SoundpadModule
    {
        private static string SoundpadLocation;
        private static Timer stateTimer;
        public static bool IsSoundpadRunning { get; private set; }
        public static string CurrentState { get; private set; }

        static SoundpadModule()
        {
            SoundpadLocation = GetSoundpadLocation();
            IsSoundpadRunning = !string.IsNullOrEmpty(SoundpadLocation);

            // Initialize the timer to check Soundpad state every second (1000 ms)
            stateTimer = new Timer(1000);
            stateTimer.Elapsed += UpdateSoundpadState;
            stateTimer.Start();
        }

        private static void UpdateSoundpadState(object sender, ElapsedEventArgs e)
        {
            var soundpadProc = Process.GetProcessesByName("Soundpad").FirstOrDefault();
            if (soundpadProc != null)
            {
                IsSoundpadRunning = true;
                string title = soundpadProc.MainWindowTitle;

                if (title.Contains(" II"))
                {
                    CurrentState = "Paused";
                }
                else if (!string.IsNullOrWhiteSpace(title) && !title.Equals("Soundpad"))
                {
                    CurrentState = "Playing: " + title.Replace("Soundpad - ", "").Trim();
                }
                else
                {
                    CurrentState = "Nothing is playing";
                }
            }
            else
            {
                IsSoundpadRunning = false;
                CurrentState = "Soundpad is not running";
            }
        }

        private static string GetSoundpadLocation()
        {
            var soundpadProc = Process.GetProcessesByName("Soundpad").FirstOrDefault();
            if (soundpadProc != null)
            {
                return soundpadProc.MainModule.FileName;
            }
            return string.Empty;
        }

        private static void ExecuteSoundpadCommand(string arguments)
        {
            if (IsSoundpadRunning)
            {
                Process.Start(SoundpadLocation, arguments);
            }
            else
            {
                // Optionally handle the case when Soundpad is not running
                // e.g., show a message to the user or write to a log
            }
        }

        public static void PlayNextSound()
        {
            ExecuteSoundpadCommand("-rc DoPlayNextSound()");
        }

        public static void PlayPreviousSound()
        {
            ExecuteSoundpadCommand("-rc DoPlayPreviousSound()");
        }

        public static void TogglePause()
        {
            ExecuteSoundpadCommand("-rc DoTogglePause()");
        }

        public static void StopSound()
        {
            ExecuteSoundpadCommand("-rc DoStopSound()");
        }

        public static void PlaySound(int index, bool speakers, bool mic)
        {
            ExecuteSoundpadCommand($"-rc DoPlaySound({index},{speakers.ToString().ToLower()},{mic.ToString().ToLower()})");
        }

        public static void PlaySoundFromCategory(int categoryIndex, int soundIndex)
        {
            ExecuteSoundpadCommand($"-rc DoPlaySoundFromCategory({categoryIndex},{soundIndex})");
        }

        public static void PlaySoundByIndex(int index)
        {
            ExecuteSoundpadCommand($"-rc DoPlaySound({index})");
        }

        // Additional functions can be added here based on the Soundpad Remote Control API
    }
}
