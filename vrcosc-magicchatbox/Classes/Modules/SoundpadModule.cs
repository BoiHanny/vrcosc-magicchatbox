using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Module that interfaces with the Soundpad application over a named pipe, providing playback
/// control and status monitoring.
/// </summary>
public partial class SoundpadModule : ObservableObject, IModule
{
    private bool _isInitialized = false;
    private string _soundpadLocation;
    private System.Timers.Timer _stateTimer;
    private readonly object _updateLock = new object();
    private int ErrorCount = 0;
    private bool _disposed;
    private readonly IAppState _appState;
    private readonly IUiDispatcher _dispatcher;

    private readonly IntegrationSettings _integrationSettings;

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

    public SoundpadModule(int time, IAppState appState, IUiDispatcher dispatcher, IntegrationSettings integrationSettings)
    {
        _appState = appState;
        _dispatcher = dispatcher;
        _integrationSettings = integrationSettings;
        _stateTimer = new System.Timers.Timer(time)
        {
            AutoReset = true,
            Enabled = false
        };
        _stateTimer.Elapsed += (sender, e) => UpdateSoundpadState(false);
        InitializeSoundpadModuleAsync();
    }

    public string Name => "Soundpad";
    public bool IsEnabled { get; set; } = true;
    public bool IsRunning => _stateTimer?.Enabled ?? false;
    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task StartAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken ct = default) { _stateTimer?.Stop(); return Task.CompletedTask; }
    public void SaveSettings() { }

    private const string PipeName = "sp_remote_control";

    private void ExecuteSoundpadCommand(string arguments)
    {
        if (!IsSoundpadRunning)
        {
            Error = true;
            ErrorString = "😞 Soundpad is not running.";
            return;
        }

        // Strip the "-rc " prefix — pipe protocol takes raw commands.
        string command = arguments.StartsWith("-rc ", StringComparison.OrdinalIgnoreCase)
            ? arguments[4..]
            : arguments;

        _ = SendPipeCommandAsync(command);
    }

    /// <summary>
    /// Sends a command to Soundpad via named pipe (microsecond latency, zero process overhead).
    /// Falls back to Process.Start if the pipe is unavailable.
    /// </summary>
    private async Task SendPipeCommandAsync(string command)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(1000).ConfigureAwait(false);

            byte[] buffer = Encoding.UTF8.GetBytes(command);
            await pipe.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            await pipe.FlushAsync().ConfigureAwait(false);

            // Read response (Soundpad always sends one)
            byte[] responseBuffer = new byte[4096];
            int bytesRead = await pipe.ReadAsync(responseBuffer, 0, responseBuffer.Length).ConfigureAwait(false);

            Error = false;
            ErrorString = string.Empty;
        }
        catch (TimeoutException)
        {
            // Pipe not available — fall back to Process.Start
            FallbackProcessStart(command);
        }
        catch (IOException)
        {
            FallbackProcessStart(command);
        }
        catch (Exception ex)
        {
            Error = true;
            ErrorString = "😞 Failed to execute Soundpad command.";
            Logging.WriteException(ex, MSGBox: false);
        }
    }

    private void FallbackProcessStart(string command)
    {
        if (string.IsNullOrEmpty(_soundpadLocation)) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _soundpadLocation,
                Arguments = $"-rc {command}",
                CreateNoWindow = true,
                UseShellExecute = false,
            });
            Error = false;
            ErrorString = string.Empty;
        }
        catch (Exception ex)
        {
            Error = true;
            ErrorString = "😞 Failed to execute Soundpad command.";
            Logging.WriteException(ex, MSGBox: false);
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
                UpdateSoundpadState(false);
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

        _dispatcher.Invoke(() =>
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
                PlayingSong = string.Empty;
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
                Error = true;
                ErrorString = "😞 Unable to read Soundpad, Ensure it is not minimized system tray.";
                Logging.WriteException(ex, MSGBox: false);
                UpdateCurrentState(string.Empty);
            }
        }
        else
        {
            Error = true;
            ErrorString = "😞 Soundpad is not running.";
            _dispatcher.Invoke(() =>
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

                _dispatcher.Invoke(() =>
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
                    _dispatcher.Invoke(() =>
                    {
                        _integrationSettings.IntgrSoundpad = false;
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
        return propertyName == nameof(_integrationSettings.IntgrSoundpad) ||
               propertyName == nameof(_appState.IsVRRunning) ||
               propertyName == nameof(_integrationSettings.IntgrSoundpad_VR) ||
               propertyName == nameof(_integrationSettings.IntgrSoundpad_DESKTOP);
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
        return _integrationSettings.IntgrSoundpad && _appState.IsVRRunning && _integrationSettings.IntgrSoundpad_VR ||
               _integrationSettings.IntgrSoundpad && !_appState.IsVRRunning && _integrationSettings.IntgrSoundpad_DESKTOP;
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _stateTimer?.Stop();
        _stateTimer?.Dispose();
    }
}
