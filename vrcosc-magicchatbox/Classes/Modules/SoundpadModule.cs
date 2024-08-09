using NLog;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Timers;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.Modules
{
    public class SoundpadModule : INotifyPropertyChanged
    {
        private static string _soundpadLocation;
        private static Timer _stateTimer;
        private static bool _isInitialized = false;
        private static int ErrorCount = 0;
        private static readonly object _updateLock = new object();

        public static bool IsSoundpadRunning { get; private set; }
        public static soundpadState CurrentSoundpadState { get; private set; }
        public static string PlayingSong { get; private set; }

        public string GetPlayingSong()
        {
            if(CurrentSoundpadState == soundpadState.Playing)
            {
                return PlayingSong;
            }
            else
            {
                return string.Empty;
            }
        }

        public SoundpadModule(int time)
        {
            _stateTimer = new Timer(time)
            {
                AutoReset = true,
                Enabled = false // Timer is initially disabled
            };
            _stateTimer.Elapsed += (sender, e) => UpdateSoundpadState();
            ViewModel.Instance.PropertyChanged += PropertyChangedHandler;
            InitializeSoundpadModuleAsync(); // Perform initial check and setup
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private static async void InitializeSoundpadModuleAsync()
        {
            await System.Threading.Tasks.Task.Run(() =>
            {
                UpdateSoundpadState(); // Update the Soundpad running status
                if (IsSoundpadRunning && string.IsNullOrEmpty(_soundpadLocation))
                {
                    _soundpadLocation = GetSoundpadLocation();
                    _isInitialized = true; // Set initialized to true only after location is retrieved
                }
                InitializeAndStartModuleIfNeeded();

            });
        }

        private static void UpdateSoundpadState()
        {
            lock (_updateLock)
            {
                try
                {
                    var soundpadProc = Process.GetProcessesByName("Soundpad").FirstOrDefault();
                    IsSoundpadRunning = soundpadProc != null;
                    UpdateCurrentStateBasedOnRunningStatus(soundpadProc);
                }
                catch (System.Exception)
                {
                    if (ErrorCount < 5)
                    {
                        ErrorCount++;
                    }
                    else
                    {
                        ViewModel.Instance.IntgrSoundpad = false;
                        ErrorCount = 0;
                    }
                }
            }
        }

        private static void UpdateCurrentStateBasedOnRunningStatus(Process soundpadProc)
        {
            if (IsSoundpadRunning)
            {
                string title = soundpadProc.MainWindowTitle;
                UpdateCurrentState(title);
            }
            else
            {
                CurrentSoundpadState =  soundpadState.NotRunning;
            }
        }

        private static void UpdateCurrentState(string title)
        {
            // Removing the content inside brackets and the brackets themselves
            title = Regex.Replace(title, @"\s*\[.*?\]\s*", " ").Trim();

            if (title.Contains("II "))
            {
                CurrentSoundpadState = soundpadState.Paused;
                // Assuming you want to update the PlayingSong even when it's paused
                PlayingSong = title.Replace("Soundpad - ", "").Replace(" II", "").Trim();
            }
            else if (!string.IsNullOrWhiteSpace(title) && !title.Equals("Soundpad"))
            {
                CurrentSoundpadState = soundpadState.Playing;
                PlayingSong = title.Replace("Soundpad - ", "").Trim();
            }
            else
            {
                CurrentSoundpadState = soundpadState.Stopped;
                PlayingSong = string.Empty; // Ensure the playing song is cleared when stopped
            }
        }


        private static string GetSoundpadLocation()
        {
            try
            {
                var soundpadProc = Process.GetProcessesByName("Soundpad").FirstOrDefault();
                if (soundpadProc != null)
                {
                    return soundpadProc.MainModule.FileName;
                }
                return string.Empty;
            }
            catch (System.Exception)
            {
                Logging.WriteException(new System.Exception("Soundpad not found"), MSGBox: false);
                return string.Empty;
            }
        }

        public void PropertyChangedHandler(object sender, PropertyChangedEventArgs e)
        {
            if (IsRelevantPropertyChange(e.PropertyName))
            {
                if (ShouldStartMonitoring())
                {
                    InitializeAndStartModuleIfNeeded();
                }
                else
                {
                    StopModule();
                }
            }
        }

        private static void InitializeAndStartModuleIfNeeded()
        {
            if (ShouldStartMonitoring())
            {

                if (!IsSoundpadRunning)
                {
                    UpdateSoundpadState(); // Check if Soundpad is running
                }
                if (!_isInitialized && IsSoundpadRunning)
                {
                    InitializeSoundpadModuleAsync();
                }
                StartModule();
            }
        }

        public static void StartModule()
        {
            if (_isInitialized)
            {
                _stateTimer.Start();
            }
        }

        public void StopModule()
        {
            if (_isInitialized)
            {
                _stateTimer.Stop();
            }
        }


        public static bool ShouldStartMonitoring()
        {
            return ViewModel.Instance.IntgrSoundpad && ViewModel.Instance.IsVRRunning && ViewModel.Instance.IntgrSoundpad_VR ||
                   ViewModel.Instance.IntgrSoundpad && !ViewModel.Instance.IsVRRunning && ViewModel.Instance.IntgrSoundpad_DESKTOP;
        }

        public bool IsRelevantPropertyChange(string propertyName)
        {
            return propertyName == nameof(ViewModel.Instance.IntgrSoundpad) ||
                   propertyName == nameof(ViewModel.Instance.IsVRRunning) ||
                   propertyName == nameof(ViewModel.Instance.IntgrSoundpad_VR) ||
                   propertyName == nameof(ViewModel.Instance.IntgrSoundpad_DESKTOP);
        }

        private static void ExecuteSoundpadCommand(string arguments)
        {
            if (IsSoundpadRunning)
            {
                Process.Start(_soundpadLocation, arguments);
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
    }

    
}
