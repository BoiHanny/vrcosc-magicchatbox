using CommunityToolkit.Mvvm.ComponentModel;
using NLog;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.Modules
{
    public partial class SoundpadModule : ObservableObject
    {
        private static bool _isInitialized = false;
        private static string _soundpadLocation;
        private static Timer _stateTimer;
        private static readonly object _updateLock = new object();
        private static int ErrorCount = 0;

        [ObservableProperty]
        soundpadState currentSoundpadState = soundpadState.NotRunning;

        [ObservableProperty]
        bool enablePanel = false;

        [ObservableProperty]
        bool error = false;

        [ObservableProperty]
        string errorString = string.Empty;

        [ObservableProperty]
        bool isSoundpadRunning = false;

        [ObservableProperty]
        bool playingNow = false;

        [ObservableProperty]
        bool stopped = true;

        [ObservableProperty]
        public string playingSong = string.Empty;

        public SoundpadModule(int time)
        {
            _stateTimer = new Timer(time)
            {
                AutoReset = true,
                Enabled = false
            };
            _stateTimer.Elapsed += (sender, e) => UpdateSoundpadState(false);
            InitializeSoundpadModuleAsync();
        }

        private void ExecuteSoundpadCommand(string arguments)
        {
            if (IsSoundpadRunning)
            {
                try
                {
                    Process.Start(_soundpadLocation, arguments);
                    Error = false;
                    ErrorString = string.Empty;
                }
                catch (System.Exception ex)
                {
                    Error = true;
                    ErrorString = "😞 Failed to execute Soundpad command.";
                    Logging.WriteException(ex, MSGBox: false);
                }
            }
            else
            {
                Error = true;
                ErrorString = "😞 Soundpad is not running.";
                // Optionally handle the case when Soundpad is not running
            }
        }

        private string GetSoundpadLocation()
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
            catch (System.Exception ex)
            {
                Logging.WriteException(new System.Exception("Soundpad not found"), MSGBox: false);
                Error = true;
                ErrorString = "😞 Unable to find Soundpad location.";
                return string.Empty;
            }
        }

        private void InitializeAndStartModuleIfNeeded()
        {
            if (ShouldStartMonitoring())
            {
                if (!IsSoundpadRunning)
                {
                    UpdateSoundpadState(false); // Check if Soundpad is running
                }
                if (!_isInitialized && IsSoundpadRunning)
                {
                    InitializeSoundpadModuleAsync();
                }
                StartModule();
            }
        }

        private async Task InitializeSoundpadModuleAsync()
        {
            await System.Threading.Tasks.Task.Run(() =>
            {
                UpdateSoundpadState(true);
                if (IsSoundpadRunning && string.IsNullOrEmpty(_soundpadLocation))
                {
                    _soundpadLocation = GetSoundpadLocation();
                    EnablePanel = true;
                    _isInitialized = true;
                }
                InitializeAndStartModuleIfNeeded();
            });
        }

        private void UpdateCurrentState(string title)
        {
            // Removing the content inside brackets and the brackets themselves
            title = Regex.Replace(title, @"\s*\[.*?\]\s*", " ").Trim();

            // Dispatch property updates to the UI thread
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (string.IsNullOrEmpty(title))
                {
                    // Unable to get the title, possibly minimized to tray
                    CurrentSoundpadState = soundpadState.Unknown;
                    PlayingNow = false;
                    Stopped = true;
                    PlayingSong = string.Empty;
                }
                else if (title.Contains("II "))
                {
                    CurrentSoundpadState = soundpadState.Paused;
                    PlayingNow = false;
                    Stopped = false;
                    PlayingSong = title.Replace("Soundpad - ", "").Replace(" II", "").Trim();
                    Error = false;
                    ErrorString = string.Empty;
                }
                else if (!string.IsNullOrWhiteSpace(title) && !title.Equals("Soundpad"))
                {
                    CurrentSoundpadState = soundpadState.Playing;
                    PlayingNow = true;
                    Stopped = false;
                    PlayingSong = title.Replace("Soundpad - ", "").Trim();
                    Error = false;
                    ErrorString = string.Empty;
                }
                else
                {
                    CurrentSoundpadState = soundpadState.Stopped;
                    PlayingNow = false;
                    Stopped = true;
                    PlayingSong = string.Empty; // Ensure the playing song is cleared when stopped
                    Error = false;
                    ErrorString = string.Empty;
                }
            });
        }

        private void UpdateCurrentStateBasedOnRunningStatus(Process soundpadProc)
        {
            if (IsSoundpadRunning && soundpadProc != null)
            {
                string title = string.Empty;
                try
                {
                    title = soundpadProc.MainWindowTitle;

                    if (string.IsNullOrEmpty(title))
                    {
                        // Soundpad is minimized to system tray or title is inaccessible
                        Error = true;
                        ErrorString = "😞 Unable to read Soundpad, Ensure it is not minimized system tray.";
                        UpdateCurrentState(string.Empty);
                    }
                    else
                    {
                        Error = false;
                        ErrorString = string.Empty;
                        UpdateCurrentState(title);
                    }
                }
                catch (System.Exception ex)
                {
                    // Exception occurred while accessing MainWindowTitle
                    Error = true;
                    ErrorString = "😞 Unable to read Soundpad, Ensure it is not minimized system tray.";
                    Logging.WriteException(ex, MSGBox: false);
                    UpdateCurrentState(string.Empty);
                }
            }
            else
            {
                // Soundpad is not running
                Error = true;
                ErrorString = "😞 Soundpad is not running.";
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    CurrentSoundpadState = soundpadState.NotRunning;
                    PlayingNow = false;
                    PlayingSong = string.Empty;
                });
            }
        }


        private void UpdateSoundpadState(bool fromStart)
        {
            lock (_updateLock)
            {
                try
                {

                    var soundpadProc = Process.GetProcessesByName("Soundpad").FirstOrDefault();

                    // Dispatch the updates to the UI thread
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        IsSoundpadRunning = soundpadProc != null;
                        UpdateCurrentStateBasedOnRunningStatus(soundpadProc);
                    });

                    if (!IsSoundpadRunning && !fromStart)
                    {
                        EnablePanel = true;
                        ErrorString = "😞 Soundpad is not running.";
                        Error = true;
                    }
                }
                catch (System.Exception ex)
                {
                    Error = true;
                    ErrorString = "😞 An error occurred while updating Soundpad state.";
                    Logging.WriteException(ex, MSGBox: false);

                    if (ErrorCount < 5)
                    {
                        ErrorCount++;
                    }
                    else
                    {
                        // Also dispatch this update to the UI thread
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            ViewModel.Instance.IntgrSoundpad = false;
                        });
                        ErrorCount = 0;
                    }
                }
            }
        }

        public string GetPlayingSong()
        {
            if (CurrentSoundpadState == soundpadState.Playing)
            {
                return PlayingSong;
            }
            else
            {
                return string.Empty;
            }
        }

        public bool IsRelevantPropertyChange(string propertyName)
        {
            return propertyName == nameof(ViewModel.Instance.IntgrSoundpad) ||
                   propertyName == nameof(ViewModel.Instance.IsVRRunning) ||
                   propertyName == nameof(ViewModel.Instance.IntgrSoundpad_VR) ||
                   propertyName == nameof(ViewModel.Instance.IntgrSoundpad_DESKTOP);
        }

        public void PlayNextSound()
        {
            ExecuteSoundpadCommand("-rc DoPlayNextSound()");
        }

        public void PlayPreviousSound()
        {
            ExecuteSoundpadCommand("-rc DoPlayPreviousSound()");
        }

        public void PlayRandomSound()
        {
            ExecuteSoundpadCommand($"-rc DoPlayRandomSound()");
        }

        public void PlaySound(int index, bool speakers, bool mic)
        {
            ExecuteSoundpadCommand($"-rc DoPlaySound({index},{speakers.ToString().ToLower()},{mic.ToString().ToLower()})");
        }

        public void PlaySoundByIndex(int index)
        {
            ExecuteSoundpadCommand($"-rc DoPlaySound({index})");
        }

        public void PlaySoundFromCategory(int categoryIndex, int soundIndex)
        {
            ExecuteSoundpadCommand($"-rc DoPlaySoundFromCategory({categoryIndex},{soundIndex})");
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

        public bool ShouldStartMonitoring()
        {
            return ViewModel.Instance.IntgrSoundpad && ViewModel.Instance.IsVRRunning && ViewModel.Instance.IntgrSoundpad_VR ||
                   ViewModel.Instance.IntgrSoundpad && !ViewModel.Instance.IsVRRunning && ViewModel.Instance.IntgrSoundpad_DESKTOP;
        }

        public void StartModule()
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
            if (!IsSoundpadRunning)
            {
                EnablePanel = false;
            }
        }

        public void StopSound()
        {
            ExecuteSoundpadCommand("-rc DoStopSound()");
            Stopped = true;
            PlayingNow = false;
        }

        public void TogglePause()
        {
            if (Stopped)
                ExecuteSoundpadCommand("-rc DoPlayCurrentSoundAgain()");
            else
                ExecuteSoundpadCommand("-rc DoTogglePause()");
        }
    }
}
