using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Module that interfaces with the Soundpad application over its remote-control named pipe,
/// providing playback control and now-playing monitoring. Status comes from GetPlayStatus()
/// and GetTitleText() pipe queries, which keep working while Soundpad is minimized to the
/// system tray; window-title scraping remains only as a fallback for old Soundpad versions
/// whose free edition exposes no pipe.
/// </summary>
public partial class SoundpadModule : ObservableObject, IModule
{
    private const string SoundpadProcessName = "Soundpad";
    private const int PipeConnectTimeoutMs = 500;
    private const int PipeRequestTimeoutMs = 2000;
    // After this many consecutive pipe failures, only retry connecting every PipeBackoffTicks polls.
    private const int PipeBackoffThreshold = 3;
    private const int PipeBackoffTicks = 10;
    // After this many consecutive poll exceptions, auto-disable the integration.
    private const int MaxConsecutivePollFailures = 5;

    private readonly SoundpadPipeClient _client = new();
    private readonly System.Timers.Timer _stateTimer;
    private int _pollGate;
    // Bumped whenever monitoring stops; in-flight polls compare it before applying state so a
    // late dispatcher callback can't repopulate data that a stop/consent-revoke just cleared.
    private int _pollEpoch;
    private int _pipeFailureStreak;
    private int _ticksUntilPipeRetry;
    private int _consecutivePollFailures;
    private bool _pipeEverConnected;
    private string _soundpadLocation = string.Empty;
    private bool _disposed;
    private readonly IToastService? _toast;
    private volatile bool _soundpadErrorShown;
    private readonly IAppState _appState;
    private readonly IUiDispatcher _dispatcher;
    private readonly IPrivacyConsentService _consentService;

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

    public SoundpadModule(int time, IAppState appState, IUiDispatcher dispatcher, IntegrationSettings integrationSettings, IPrivacyConsentService consentService, IToastService? toast = null)
    {
        _appState = appState;
        _dispatcher = dispatcher;
        _integrationSettings = integrationSettings;
        _consentService = consentService;
        _toast = toast;
        _stateTimer = new System.Timers.Timer(time)
        {
            AutoReset = true,
            Enabled = false
        };
        _stateTimer.Elapsed += (sender, e) => _ = PollAsync();

        _consentService.ConsentChanged += OnConsentChanged;

        if (ShouldStartMonitoring())
            StartModule();
    }

    private void OnConsentChanged(object? sender, ConsentChangedEventArgs e)
    {
        if (e.Hook != PrivacyHook.SoundpadBridge)
            return;

        if (e.NewState == ConsentState.Denied)
        {
            _dispatcher.BeginInvoke(() =>
            {
                StopModule();
                PlayingSong = string.Empty;
                CurrentSoundpadState = soundpadState.NotRunning;
                PlayingNow = false;
                Stopped = true;
                IsSoundpadRunning = false;
                EnablePanel = false;
            });
            _toast?.Show("🔒 Soundpad", "Soundpad bridge paused — privacy consent revoked.", ToastType.Privacy, key: "soundpad-privacy-denied");
        }
        else if (e.NewState == ConsentState.Approved && ShouldStartMonitoring())
        {
            StartModule();
        }
    }

    public string Name => "Soundpad";
    public bool IsEnabled { get; set; } = true;
    public bool IsRunning => _stateTimer?.Enabled ?? false;
    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task StartAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task StopAsync(CancellationToken ct = default)
    {
        Interlocked.Increment(ref _pollEpoch);
        _stateTimer?.Stop();
        _client.Disconnect();
        return Task.CompletedTask;
    }

    public void SaveSettings() { }

    /// <summary>
    /// One poll cycle: process check → pipe status queries → legacy window-title fallback.
    /// Overlapping ticks are skipped instead of queued.
    /// </summary>
    private async Task PollAsync()
    {
        if (_disposed || !_stateTimer.Enabled)
            return;
        if (Interlocked.Exchange(ref _pollGate, 1) == 1)
            return;

        int epoch = Volatile.Read(ref _pollEpoch);
        Process[] soundpadProcs = Array.Empty<Process>();
        try
        {
            soundpadProcs = Process.GetProcessesByName(SoundpadProcessName);
            var soundpadProc = soundpadProcs.FirstOrDefault();
            if (soundpadProc == null)
            {
                _client.Disconnect();
                _pipeFailureStreak = 0;
                _ticksUntilPipeRetry = 0;
                ApplyState(epoch, soundpadState.NotRunning, playingNow: false, stopped: true, song: string.Empty,
                    isRunning: false, error: true, errorMessage: "😞 Soundpad is not running.");
                return;
            }

            var status = SoundpadPlayStatus.Unknown;
            string? titleResponse = null;

            if (await TryEnsurePipeAsync().ConfigureAwait(false))
            {
                string? statusResponse = (await _client.SendRequestAsync("GetPlayStatus()", PipeRequestTimeoutMs).ConfigureAwait(false)).Response;
                status = SoundpadStatusParser.ParsePlayStatus(statusResponse);
                if (statusResponse == null)
                {
                    RegisterPipeFailure();
                }
                else
                {
                    _pipeFailureStreak = 0;
                    _pipeEverConnected = true;
                    if (status is SoundpadPlayStatus.Playing or SoundpadPlayStatus.Paused or SoundpadPlayStatus.Seeking)
                        titleResponse = (await _client.SendRequestAsync("GetTitleText()", PipeRequestTimeoutMs).ConfigureAwait(false)).Response;
                }
            }

            if (status == SoundpadPlayStatus.Unknown)
            {
                PollFromWindowTitle(epoch, soundpadProc);
                return;
            }

            string song = SoundpadStatusParser.ParseNowPlayingTitle(titleResponse);
            // The frame title lags behind playback start (and the pipe can break between the
            // two queries) — keep the last known song rather than blanking it for a tick.
            if (song.Length == 0 && status != SoundpadPlayStatus.Stopped)
                song = PlayingSong;

            switch (status)
            {
                case SoundpadPlayStatus.Playing:
                case SoundpadPlayStatus.Seeking:
                    ApplyState(epoch, soundpadState.Playing, playingNow: true, stopped: false, song: song);
                    break;
                case SoundpadPlayStatus.Paused:
                    ApplyState(epoch, soundpadState.Paused, playingNow: false, stopped: false, song: song);
                    break;
                default:
                    ApplyState(epoch, soundpadState.Stopped, playingNow: false, stopped: true, song: string.Empty);
                    break;
            }
            _consecutivePollFailures = 0;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            // Leave IsSoundpadRunning/PlayingSong untouched — a transient poll failure must not
            // flip UI state; only surface the error.
            SetError("😞 An error occurred while updating Soundpad state.");
            if (++_consecutivePollFailures >= MaxConsecutivePollFailures)
            {
                _consecutivePollFailures = 0;
                _toast?.Show("🎵 Soundpad", "Soundpad integration disabled after repeated errors — re-enable it to retry.", ToastType.Warning, key: "soundpad-autodisable");
                _dispatcher.BeginInvoke(() => _integrationSettings.IntgrSoundpad = false);
            }
        }
        finally
        {
            foreach (var proc in soundpadProcs)
                proc.Dispose();
            Interlocked.Exchange(ref _pollGate, 0);
        }
    }

    private async Task<bool> TryEnsurePipeAsync()
    {
        if (_client.IsConnected)
            return true;

        if (_ticksUntilPipeRetry > 0)
        {
            _ticksUntilPipeRetry--;
            return false;
        }

        bool connected = await _client.TryConnectAsync(PipeConnectTimeoutMs).ConfigureAwait(false);
        if (!connected)
            RegisterPipeFailure();
        return connected;
    }

    private void RegisterPipeFailure()
    {
        _pipeFailureStreak++;
        if (_pipeFailureStreak >= PipeBackoffThreshold)
            _ticksUntilPipeRetry = PipeBackoffTicks;
    }

    /// <summary>
    /// Legacy status source for Soundpad versions without a remote-control pipe. Blind while
    /// Soundpad is minimized to the tray (MainWindowTitle is empty there).
    /// </summary>
    private void PollFromWindowTitle(int epoch, Process soundpadProc)
    {
        string title = string.Empty;
        try
        {
            title = soundpadProc.MainWindowTitle;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            // A pipe that worked before is just transiently broken (Soundpad restart, sleep/wake);
            // only blame the Soundpad version when the pipe never answered at all.
            string message = _pipeEverConnected
                ? "😞 Lost the Soundpad connection — reconnecting…"
                : "😞 Unable to read Soundpad — remote control unavailable and its window is minimized to the tray. Updating Soundpad usually fixes this.";
            ApplyState(epoch, soundpadState.Unknown, playingNow: false, stopped: true, song: string.Empty,
                error: true, errorMessage: message);
            return;
        }

        bool paused = SoundpadStatusParser.IsPausedTitle(title);
        string song = SoundpadStatusParser.ParseNowPlayingTitle(title);
        if (string.IsNullOrEmpty(song))
            ApplyState(epoch, soundpadState.Stopped, playingNow: false, stopped: true, song: string.Empty);
        else if (paused)
            ApplyState(epoch, soundpadState.Paused, playingNow: false, stopped: false, song: song);
        else
            ApplyState(epoch, soundpadState.Playing, playingNow: true, stopped: false, song: song);
    }

    private void ApplyState(int epoch, soundpadState state, bool playingNow, bool stopped, string song,
        bool isRunning = true, bool error = false, string errorMessage = "")
    {
        _dispatcher.BeginInvoke(() =>
        {
            // Stale poll: monitoring was stopped (or consent revoked) after this cycle started.
            if (epoch != Volatile.Read(ref _pollEpoch))
                return;

            IsSoundpadRunning = isRunning;
            CurrentSoundpadState = state;
            PlayingNow = playingNow;
            Stopped = stopped;
            PlayingSong = song;
            Error = error;
            ErrorString = error ? errorMessage : string.Empty;
        });
    }

    private void SetError(string message)
    {
        _dispatcher.BeginInvoke(() =>
        {
            Error = true;
            ErrorString = message;
        });
    }

    /// <summary>
    /// Sends an action command over the pipe, then refreshes the now-playing state shortly
    /// after. The "Soundpad.exe -rc" fallback only runs when the request never reached
    /// Soundpad — a delivered-but-unanswered command may have executed and must not be retried.
    /// </summary>
    private async Task SendCommandAsync(string command)
    {
        if (!IsSoundpadRunning)
        {
            SetError("😞 Soundpad is not running.");
            return;
        }

        try
        {
            var reply = await _client.SendRequestAsync(command, PipeRequestTimeoutMs).ConfigureAwait(false);
            if (reply.Response == null)
            {
                if (reply.RequestDelivered)
                {
                    SetError("😞 Soundpad did not confirm the command.");
                    return;
                }
                if (!TryFallbackProcessStart(command))
                {
                    SetError("😞 Failed to execute Soundpad command.");
                    return;
                }
            }
            else if (!SoundpadStatusParser.IsSuccessResponse(reply.Response))
            {
                SetError($"😞 Soundpad rejected the command ({reply.Response}).");
                return;
            }

            // Accepted is not completed — give Soundpad a moment, then refresh now-playing.
            await Task.Delay(150).ConfigureAwait(false);
            await PollAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            SetError("😞 Failed to execute Soundpad command.");
        }
    }

    private bool TryFallbackProcessStart(string command)
    {
        string location = ResolveSoundpadLocation();
        if (string.IsNullOrEmpty(location))
            return false;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = location,
                Arguments = $"-rc {command}",
                CreateNoWindow = true,
                UseShellExecute = false,
            });
            return true;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return false;
        }
    }

    private string ResolveSoundpadLocation()
    {
        if (!string.IsNullOrEmpty(_soundpadLocation))
            return _soundpadLocation;

        Process[] procs = Array.Empty<Process>();
        try
        {
            procs = Process.GetProcessesByName(SoundpadProcessName);
            _soundpadLocation = procs.FirstOrDefault()?.MainModule?.FileName ?? string.Empty;
        }
        catch (Exception ex)
        {
            // Access denied when Soundpad runs elevated and MagicChatBox does not; the pipe
            // still works in that case, so this is quietly non-fatal.
            Logging.WriteException(new Exception("Unable to resolve Soundpad location (is Soundpad running as administrator?)", ex), MSGBox: false);
            _soundpadLocation = string.Empty;
        }
        finally
        {
            foreach (var proc in procs)
                proc.Dispose();
        }
        return _soundpadLocation;
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

    public void PlayNextSound() => _ = SendCommandAsync("DoPlayNextSound()");

    public void PlayPreviousSound() => _ = SendCommandAsync("DoPlayPreviousSound()");

    public void PlayRandomSound() => _ = SendCommandAsync("DoPlayRandomSound()");

    public void PlaySound(int index, bool speakers, bool mic)
        => _ = SendCommandAsync($"DoPlaySound({index},{speakers.ToString().ToLowerInvariant()},{mic.ToString().ToLowerInvariant()})");

    public void PlaySoundByIndex(int index) => _ = SendCommandAsync($"DoPlaySound({index})");

    public void PlaySoundFromCategory(int categoryIndex, int soundIndex)
        => _ = SendCommandAsync($"DoPlaySoundFromCategory({categoryIndex},{soundIndex})");

    public void PropertyChangedHandler(object sender, PropertyChangedEventArgs e)
    {
        if (IsRelevantPropertyChange(e.PropertyName))
        {
            if (ShouldStartMonitoring())
            {
                StartModule();
            }
            else
            {
                StopModule();
            }
        }
    }

    public bool ShouldStartMonitoring()
    {
        if (!_consentService.IsApproved(PrivacyHook.SoundpadBridge))
            return false;

        return _integrationSettings.IntgrSoundpad && _appState.IsVRRunning && _integrationSettings.IntgrSoundpad_VR ||
               _integrationSettings.IntgrSoundpad && !_appState.IsVRRunning && _integrationSettings.IntgrSoundpad_DESKTOP;
    }

    public void StartModule()
    {
        if (_disposed)
            return;

        _dispatcher.BeginInvoke(() => EnablePanel = true);
        _pipeFailureStreak = 0;
        _ticksUntilPipeRetry = 0;
        _consecutivePollFailures = 0;
        _stateTimer.Start();
        _ = PollAsync();
    }

    public void StopModule()
    {
        Interlocked.Increment(ref _pollEpoch);
        _stateTimer?.Stop();
        _client.Disconnect();
        _dispatcher.BeginInvoke(() =>
        {
            EnablePanel = false;
            Error = false;
            ErrorString = string.Empty;
        });
    }

    public void StopSound()
    {
        _ = SendCommandAsync("DoStopSound()");
        _dispatcher.BeginInvoke(() =>
        {
            Stopped = true;
            PlayingNow = false;
        });
    }

    public void TogglePause()
    {
        if (Stopped)
            _ = SendCommandAsync("DoPlayCurrentSoundAgain()");
        else
            _ = SendCommandAsync("DoTogglePause()");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Interlocked.Increment(ref _pollEpoch);
        _consentService.ConsentChanged -= OnConsentChanged;
        _stateTimer?.Stop();
        _stateTimer?.Dispose();
        _client.Dispose();
    }

    partial void OnErrorChanged(bool value)
    {
        if (!value)
            _soundpadErrorShown = false;
    }

    partial void OnErrorStringChanged(string value)
    {
        if (!ShouldStartMonitoring()) return;
        if (error && !string.IsNullOrEmpty(value) && !_soundpadErrorShown)
        {
            _soundpadErrorShown = true;
            _toast?.Show("🎵 Soundpad", value, ToastType.Warning, key: "soundpad-error");
        }
    }
}
