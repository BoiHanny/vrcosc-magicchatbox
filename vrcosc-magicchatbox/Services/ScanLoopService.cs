using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Manages the main OSC scanning loop and pause/chat timers.
/// TTS and persistence are handled by dedicated services.
/// </summary>
public sealed class ScanLoopService : IDisposable
{
    private Timer? _backgroundCheck;
    private TimeSpan _currentInterval;
    private readonly IAppState _appState;
    private readonly ChatStatusDisplayState _chatStatus;
    private readonly IntegrationDisplayState _integrationDisplay;
    private readonly OscDisplayState _oscDisplay;
    private readonly EmojiService _emojis;
    private readonly ComponentStatsModule _statsModule;
    private readonly IUiDispatcher _dispatcher;
    private readonly IWindowActivityService _windowActivity;
    private readonly ITimeFormattingService _timeFormatting;
    private readonly AsyncOperationGuard _faultTracker = new();
    private System.Timers.Timer? _chatUpdateTimer;
    private System.Timers.Timer? _pauseTimer;
    private DateTime _nextRun = DateTime.Now;
    private DateTime _lastOSCMessageTime = DateTime.MinValue;
    private bool _isProcessing;
    private bool _disposed;
    private int _tickQueued;

    private readonly ChatSettings CS;
    private readonly AppSettings AS;
    private readonly IntegrationSettings _integrationSettings;

    private readonly Lazy<OSCController> _osc;
    private OSCController Osc => _osc.Value;

    private readonly Lazy<IOscSender> _oscSender;
    private IOscSender OscSend => _oscSender.Value;

    private bool _started;

    public ScanLoopService(
        IAppState appState,
        ChatStatusDisplayState chatStatus,
        IntegrationDisplayState integrationDisplay,
        OscDisplayState oscDisplay,
        EmojiService emojis,
        ComponentStatsModule statsModule,
        IUiDispatcher dispatcher,
        IWindowActivityService windowActivity,
        ITimeFormattingService timeFormatting,
        ISettingsProvider<IntegrationSettings> intSettingsProvider,
        ISettingsProvider<ChatSettings> chatSettingsProvider,
        ISettingsProvider<AppSettings> appSettingsProvider,
        Lazy<OSCController> osc,
        Lazy<IOscSender> oscSender)
    {
        _appState = appState;
        _chatStatus = chatStatus;
        _integrationDisplay = integrationDisplay;
        _oscDisplay = oscDisplay;
        _emojis = emojis;
        _statsModule = statsModule;
        _dispatcher = dispatcher;
        _windowActivity = windowActivity;
        _timeFormatting = timeFormatting;

        _integrationSettings = intSettingsProvider.Value;
        CS = chatSettingsProvider.Value;
        AS = appSettingsProvider.Value;

        _osc = osc;
        _oscSender = oscSender;
    }

    public void Start()
    {
        if (_started) return;
        _started = true;
        _currentInterval = TimeSpan.FromMilliseconds(AS.ScanningInterval * 1000);
        _backgroundCheck = new Timer(_ =>
        {
            if (Interlocked.CompareExchange(ref _tickQueued, 1, 0) == 0)
                _dispatcher.InvokeAsync(OnBackgroundTick);
        }, null, _currentInterval, _currentInterval);
    }

    public void Stop()
    {
        if (!_started) return;
        _started = false;
        _backgroundCheck?.Dispose();
        _backgroundCheck = null;
        StopPauseTimer();
        StopChatUpdateTimer();
    }

    /// <summary>
    /// Main timer tick handler — manages pause state and triggers scan loop.
    /// </summary>
    private void OnBackgroundTick()
    {
        Interlocked.Exchange(ref _tickQueued, 0);
        if (!_started || _disposed)
            return;

        bool chatItemActive = _chatStatus.LastMessages != null
            && _chatStatus.LastMessages.Any(x => x.IsRunning);

        if (_chatStatus.ScanPause && chatItemActive)
        {
            StartPauseTimerIfNeeded();
        }
        else
        {
            StopPauseTimer();
            StopChatUpdateTimer();
            _chatStatus.CountDownUI = true;
            _ = Scantick();
        }
    }

    /// <summary>
    /// Core scan/update loop — updates modules then sends OSC message.
    /// </summary>
    public async Task Scantick(bool firstRun = false)
    {
        if (!_started || _disposed) return;
        if (_isProcessing) return;
        _isProcessing = true;

        try
        {
            if (DateTime.Now >= _nextRun || firstRun)
            {
                var desiredInterval = TimeSpan.FromMilliseconds(AS.ScanningInterval * 1000);
                if (_currentInterval != desiredInterval)
                {
                    _currentInterval = desiredInterval;
                    _backgroundCheck?.Change(_currentInterval, _currentInterval);
                    _nextRun = DateTime.Now.Add(_currentInterval);
                    return;
                }

                await ExecuteScantickLogicAsync();
                if (!_started || _disposed) return;

                Osc.BuildOSC();

                long nowMs = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                long lastMs = _lastOSCMessageTime.Ticks / TimeSpan.TicksPerMillisecond;
                const long allowedOverlapMs = 100;

                if ((nowMs - lastMs + allowedOverlapMs) >= AS.ScanningInterval * 1000)
                {
                    if (!_started || _disposed) return;
                    OscSend.SendOSCMessage(false);
                    _lastOSCMessageTime = DateTime.Now;
                }
                else
                {
                    var nextAllowed = _lastOSCMessageTime.Add(desiredInterval);
                    Logging.WriteInfo($"OSC message rate-limited, NOW: {DateTime.Now} ALLOWED AFTER: {nextAllowed}");
                }

                _nextRun = DateTime.Now.Add(_currentInterval);
            }
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private async Task ExecuteScantickLogicAsync()
    {
        try
        {
            var tasks = new List<Task>
            {
                _faultTracker.RunGuardedAsync("VRCheck", () => _statsModule.IsVRRunning())
            };

            if (_integrationSettings.IntgrScanWindowActivity)
                tasks.Add(_faultTracker.RunGuardedAsync("WindowActivity", UpdateFocusedWindowAsync));

            if (_integrationSettings.IntgrComponentStats)
                tasks.Add(_faultTracker.RunGuardedAsync("HardwareStats", () => Task.Run(() => _statsModule.TickAndUpdate())));

            if (_integrationSettings.IntgrScanWindowTime)
                tasks.Add(_faultTracker.RunGuardedAsync("TimeFormat", UpdateCurrentTimeAsync));

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
    }

    #region Module update helpers

    private async Task UpdateFocusedWindowAsync()
    {
        _chatStatus.FocusedWindow = await Task.Run(
            () => _windowActivity.GetForegroundProcessName()).ConfigureAwait(false);
    }

    private async Task UpdateCurrentTimeAsync()
    {
        _integrationDisplay.CurrentTime = await Task.Run(
            () => _timeFormatting.GetFormattedCurrentTime()).ConfigureAwait(false);
    }

    #endregion

    #region Pause / Chat Update timers

    private void StartPauseTimerIfNeeded()
    {
        if (_pauseTimer != null) return;

        _chatStatus.CountDownUI = false;
        _pauseTimer = new System.Timers.Timer(Core.Constants.BackgroundCheckInterval.TotalMilliseconds);
        _pauseTimer.Elapsed += OnPauseTimerTick;
        _pauseTimer.Start();

        if (CS.KeepUpdatingChat)
            StartChatUpdateTimerIfNeeded();
    }

    private void StartChatUpdateTimerIfNeeded()
    {
        if (_chatUpdateTimer != null) return;
        if (_chatStatus.LastMessages == null) return;

        var lastSend = _chatStatus.LastMessages.FirstOrDefault(x => x.IsRunning);
        if (lastSend != null)
            lastSend.LiveEditButtonTxt = "Sending...";

        _chatUpdateTimer = new System.Timers.Timer((int)(CS.ChattingUpdateRate * 1000));
        _chatUpdateTimer.Elapsed += OnChatUpdateTimerTick;
        _chatUpdateTimer.Start();
    }

    private void StopPauseTimer()
    {
        if (_pauseTimer == null) return;
        _pauseTimer.Stop();
        _pauseTimer.Elapsed -= OnPauseTimerTick;
        _pauseTimer.Dispose();
        _pauseTimer = null;
    }

    private void StopChatUpdateTimer()
    {
        if (_chatUpdateTimer == null) return;
        _chatUpdateTimer.Stop();
        _chatUpdateTimer.Elapsed -= OnChatUpdateTimerTick;
        _chatUpdateTimer.Dispose();
        _chatUpdateTimer = null;
    }

    private void OnPauseTimerTick(object? sender, System.Timers.ElapsedEventArgs e)
    {
        _dispatcher.BeginInvoke(() =>
        {
            try
            {
                var lastSendChat = _chatStatus.LastMessages.FirstOrDefault(x => x.IsRunning);
                _chatStatus.ScanPauseCountDown--;

                if (lastSendChat != null)
                {
                    lastSendChat.CanLiveEdit = CS.ChatLiveEdit;
                    lastSendChat.LiveEditButtonTxt = CS.RealTimeChatEdit
                        ? $"Live Edit ({_chatStatus.ScanPauseCountDown})"
                        : $"Edit ({_chatStatus.ScanPauseCountDown})";
                }

                if (_chatStatus.ScanPauseCountDown <= 0 || !_chatStatus.ScanPause)
                {
                    _chatStatus.ScanPause = false;
                    StopPauseTimer();

                    if (_chatStatus.ScanPauseCountDown != 0)
                        _chatStatus.ScanPauseCountDown = 0;

                    Osc.ClearChat(lastSendChat);
                    OscSend.SendOSCMessage(false);

                    OnBackgroundTick();
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        });
    }

    private void OnChatUpdateTimerTick(object? sender, System.Timers.ElapsedEventArgs e)
    {
        // Marshal to UI thread — ObservableCollection (LastMessages) is not thread-safe
        _dispatcher.BeginInvoke(() =>
        {
            try
            {
                var lastSendChat = _chatStatus.LastMessages.FirstOrDefault(x => x.IsRunning);

                if (CS.KeepUpdatingChat && lastSendChat != null)
                {
                    if (lastSendChat.Msg.Length > 0 && lastSendChat.Msg.Length <= Core.Constants.MaxChatMessageLength && _appState.MasterSwitch)
                    {
                        string completeMsg;
                        if (CS.PrefixChat)
                        {
                            string icon = _emojis.GetNextEmoji(true);
                            completeMsg = icon + " " + lastSendChat.Msg;
                        }
                        else
                        {
                            completeMsg = lastSendChat.Msg;
                        }

                        _oscDisplay.OscToSent = completeMsg;
                        OscSend.SendOSCMessage(false);
                    }
                }
                else
                {
                    foreach (var item in _chatStatus.LastMessages)
                    {
                        item.CanLiveEdit = false;
                        item.CanLiveEditRun = false;
                        item.MsgReplace = string.Empty;
                        item.IsRunning = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        });
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
