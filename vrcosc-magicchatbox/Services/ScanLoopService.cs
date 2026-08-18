using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Services;

public sealed class ScanLoopService : IDisposable
{
    private Timer? _backgroundCheck;
    private TimeSpan _currentInterval;
    private static readonly TimeSpan ComponentStatsMinInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan VrCheckMinInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan WindowActivityMinInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan VrCheckTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan WindowActivityTimeout = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan HardwareStatsTimeout = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan HardwareStatsFirstRunTimeout = TimeSpan.FromSeconds(45);
    private readonly IAppState _appState;
    private readonly ChatStatusDisplayState _chatStatus;
    private readonly IntegrationDisplayState _integrationDisplay;
    private readonly OscDisplayState _oscDisplay;
    private readonly EmojiService _emojis;
    private readonly Lazy<ComponentStatsModule> _statsModule;
    private readonly IUiDispatcher _dispatcher;
    private readonly IWindowActivityService _windowActivity;
    private readonly ITimeFormattingService _timeFormatting;
    private readonly AsyncOperationGuard _faultTracker = new();
    private System.Timers.Timer? _chatUpdateTimer;
    private System.Timers.Timer? _pauseTimer;
    private DateTime _nextRun = DateTime.UtcNow;
    private DateTime _lastOSCMessageTime = DateTime.MinValue;
    private DateTime _lastComponentStatsUpdateUtc = DateTime.MinValue;
    private DateTime _lastVrCheckUtc = DateTime.MinValue;
    private DateTime _lastWindowActivityUtc = DateTime.MinValue;
    private int _windowActivityInFlight;
    private int _componentStatsInFlight;
    private bool _componentStatsPrimed;
    private string? _lastFormattedCurrentTime;
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

    private readonly Lazy<ILiveTypingService> _liveTyping;
    private ILiveTypingService LiveTyping => _liveTyping.Value;

    private bool _started;

    private static TimeSpan ToOscTickInterval(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds))
            seconds = AppSettings.OscTickIntervalDefaultSeconds;

        return TimeSpan.FromMilliseconds(Math.Clamp(
            seconds,
            AppSettings.OscTickIntervalMinSeconds,
            AppSettings.OscTickIntervalMaxSeconds) * 1000);
    }

    public ScanLoopService(
        IAppState appState,
        ChatStatusDisplayState chatStatus,
        IntegrationDisplayState integrationDisplay,
        OscDisplayState oscDisplay,
        EmojiService emojis,
        Lazy<ComponentStatsModule> statsModule,
        IUiDispatcher dispatcher,
        IWindowActivityService windowActivity,
        ITimeFormattingService timeFormatting,
        ISettingsProvider<IntegrationSettings> intSettingsProvider,
        ISettingsProvider<ChatSettings> chatSettingsProvider,
        ISettingsProvider<AppSettings> appSettingsProvider,
        Lazy<OSCController> osc,
        Lazy<IOscSender> oscSender,
        Lazy<ILiveTypingService> liveTyping)
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
        _liveTyping = liveTyping;
    }

    public void Start()
    {
        if (_started) return;
        _started = true;
        _currentInterval = ToOscTickInterval(AS.ScanningInterval);
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

    private void OnBackgroundTick()
    {
        Interlocked.Exchange(ref _tickQueued, 0);
        if (!_started || _disposed)
            return;

        if (LiveTyping.IsHolding)
        {
            StopPauseTimer();
            StopChatUpdateTimer();
            return;
        }

        if (IsChatOverrideActive())
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

    private bool IsChatOverrideActive()
    {
        return LiveTyping.IsHolding
            || IsChatOverrideActive(_chatStatus.ScanPause, _chatStatus.LastMessagesSnapshot);
    }

    public static bool IsChatOverrideActive(bool scanPause, IEnumerable<ChatItem>? lastMessages)
    {
        return scanPause && lastMessages != null && lastMessages.Any(x => x.IsRunning);
    }

    public static bool ShouldPresent(bool started, bool disposed, bool chatOverrideActive, bool liveTypingHolding)
        => started && !disposed && !chatOverrideActive && !liveTypingHolding;

    public async Task Scantick(bool firstRun = false)
    {
        if (!_started || _disposed) return;
        if (IsChatOverrideActive()) return;
        if (_isProcessing) return;
        _isProcessing = true;

        try
        {
            DateTime nowUtc = DateTime.UtcNow;
            const long allowedOverlapMs = 100;
            if (nowUtc.AddMilliseconds(allowedOverlapMs) >= _nextRun || firstRun)
            {
                var desiredInterval = ToOscTickInterval(AS.ScanningInterval);
                if (_currentInterval != desiredInterval)
                {
                    _currentInterval = desiredInterval;
                    _backgroundCheck?.Change(_currentInterval, _currentInterval);
                    _nextRun = nowUtc.Add(_currentInterval);
                    return;
                }

                _nextRun = nowUtc.Add(_currentInterval);

                await ExecuteScantickLogicAsync();
                if (!_started || _disposed) return;
                if (IsChatOverrideActive()) return;

                var osc = Osc;
                var built = await Task.Run(() => osc.Build()).ConfigureAwait(true);

                if (!ShouldPresent(_started, _disposed, IsChatOverrideActive(), LiveTyping.IsHolding))
                    return;

                osc.Present(built);

                long nowMs = nowUtc.Ticks / TimeSpan.TicksPerMillisecond;
                long lastMs = _lastOSCMessageTime.Ticks / TimeSpan.TicksPerMillisecond;

                if ((nowMs - lastMs + allowedOverlapMs) >= desiredInterval.TotalMilliseconds)
                {
                    if (!_started || _disposed) return;
                    bool sent = await OscSend.SendOSCMessage(false);
                    if (sent)
                        _lastOSCMessageTime = nowUtc;
                }
                else
                {
                    var nextAllowed = _lastOSCMessageTime.Add(desiredInterval);
                    Logging.WriteInfo($"OSC message rate-limited, NOW: {DateTime.UtcNow} ALLOWED AFTER: {nextAllowed}");
                }
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
            var tasks = new List<Task>();

            if (IsVrCheckDue())
            {
                tasks.Add(_faultTracker.RunGuardedAsync(
                    "VRCheck",
                    () => Task.Run(() => _statsModule.Value.IsVRRunning()),
                    VrCheckTimeout));
                _lastVrCheckUtc = DateTime.UtcNow;
            }

            if (_integrationSettings.IntgrScanWindowActivity
                && IsWindowActivityDue()
                && Interlocked.CompareExchange(ref _windowActivityInFlight, 1, 0) == 0)
            {
                _lastWindowActivityUtc = DateTime.UtcNow;
                tasks.Add(RunWindowActivityAsync());
            }

            if (_integrationSettings.IntgrComponentStats)
            {
                if (IsComponentStatsDue()
                    && Interlocked.CompareExchange(ref _componentStatsInFlight, 1, 0) == 0)
                {
                    tasks.Add(RunComponentStatsAsync());
                }
            }
            else if (_statsModule.IsValueCreated && _integrationDisplay.ComponentStatsRunning)
            {
                tasks.Add(_faultTracker.RunGuardedAsync(
                    "HardwareStatsStop",
                    () => Task.Run(() => _statsModule.Value.StopAndClear()),
                    HardwareStatsTimeout));
            }

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

    private async Task RunWindowActivityAsync()
    {
        try
        {
            await _faultTracker.RunGuardedAsync(
                "WindowActivity",
                UpdateFocusedWindowAsync,
                WindowActivityTimeout).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _windowActivityInFlight, 0);
        }
    }

    private async Task UpdateFocusedWindowAsync()
    {
        _chatStatus.FocusedWindow = await Task.Run(
            () => _windowActivity.GetForegroundProcessName()).ConfigureAwait(false);
    }

    private async Task UpdateCurrentTimeAsync()
    {
        var formatted = await Task.Run(
            () => _timeFormatting.GetFormattedCurrentTime()).ConfigureAwait(false);

        if (!string.Equals(formatted, _lastFormattedCurrentTime, StringComparison.Ordinal))
        {
            _lastFormattedCurrentTime = formatted;
            _integrationDisplay.CurrentTime = formatted;
        }
    }

    private async Task RunComponentStatsAsync()
    {
        try
        {
            await _faultTracker.RunGuardedAsync(
                "HardwareStats",
                () => Task.Run(() => _statsModule.Value.TickAndUpdate()),
                _componentStatsPrimed ? HardwareStatsTimeout : HardwareStatsFirstRunTimeout)
                .ConfigureAwait(false);

            _componentStatsPrimed = true;
        }
        finally
        {
            _lastComponentStatsUpdateUtc = DateTime.UtcNow;
            Interlocked.Exchange(ref _componentStatsInFlight, 0);
        }
    }

    private bool IsComponentStatsDue()
    {
        return DateTime.UtcNow - _lastComponentStatsUpdateUtc >= ComponentStatsMinInterval;
    }

    private bool IsVrCheckDue()
    {
        return DateTime.UtcNow - _lastVrCheckUtc >= VrCheckMinInterval;
    }

    private bool IsWindowActivityDue()
    {
        return DateTime.UtcNow - _lastWindowActivityUtc >= WindowActivityMinInterval;
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
                    lastSendChat.CanLiveEdit = CS.ChatLiveEdit;

                if (_chatStatus.ScanPauseCountDown <= 0 || !_chatStatus.ScanPause)
                {
                    _chatStatus.ScanPause = false;
                    StopPauseTimer();

                    if (_chatStatus.ScanPauseCountDown != 0)
                        _chatStatus.ScanPauseCountDown = 0;

                    Osc.ClearChat(lastSendChat);
                    _ = OscSend.SendOSCMessage(false, force: true);

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
                        _ = OscSend.SendOSCMessage(false);
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
