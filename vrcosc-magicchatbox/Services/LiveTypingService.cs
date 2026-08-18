using System;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Services;

public sealed class LiveTypingService : ILiveTypingService, IDisposable
{
    private readonly ChatSettings _chatSettings;
    private readonly IAppState _appState;
    private readonly Lazy<IOscSender> _oscSender;
    private readonly OscDisplayState _oscDisplay;

    private readonly object _gate = new();
    private System.Timers.Timer? _trailing;
    private System.Timers.Timer? _idle;
    private string _pending = string.Empty;
    private string _pushed = string.Empty;
    private DateTime _lastPushUtc = DateTime.MinValue;
    private volatile bool _holding;
    private bool _disposed;

    public LiveTypingService(
        ISettingsProvider<ChatSettings> chatSettings,
        IAppState appState,
        Lazy<IOscSender> oscSender,
        OscDisplayState oscDisplay)
    {
        _chatSettings = chatSettings.Value;
        _appState = appState;
        _oscSender = oscSender;
        _oscDisplay = oscDisplay;

        _chatSettings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ChatSettings.ChatLiveTyping) && !_chatSettings.ChatLiveTyping)
                Release(clearChatbox: true);
        };
    }

    public bool IsHolding => _holding && _appState.MasterSwitch;

    public void Show(string text)
    {
        if (_disposed)
            return;

        if (!_chatSettings.ChatLiveTyping || !_appState.MasterSwitch)
        {
            Release(clearChatbox: true);
            return;
        }

        string line = (text ?? string.Empty).TrimEnd();
        if (line.Length == 0)
        {
            Release(clearChatbox: true);
            return;
        }

        if (line.Length > Core.Constants.OscMaxMessageLength)
            line = line[..Core.Constants.OscMaxMessageLength];

        lock (_gate)
        {
            _pending = line;
            _holding = true;

            ArmIdleLocked();

            var wait = _lastPushUtc.AddMilliseconds(_chatSettings.ChatLiveTypingRateMs) - DateTime.UtcNow;
            if (wait <= TimeSpan.Zero)
                PushLocked();
            else
                ArmTrailingLocked(wait);
        }
    }

    public event Action? FinalizeRequested;

    public void Release(bool clearChatbox)
    {
        bool wasHolding;
        lock (_gate)
        {
            wasHolding = _holding;
            _holding = false;
            _pending = string.Empty;
            _pushed = string.Empty;
            _trailing?.Stop();
            _idle?.Stop();
        }

        if (!wasHolding || !clearChatbox)
            return;

        _oscDisplay.OscToSent = string.Empty;
        _oscDisplay.OscMsgCount = 0;
        _oscDisplay.OscMsgCountUI = $"0/{Core.Constants.OscMaxMessageLength}";

        if (_appState.MasterSwitch)
            _ = _oscSender.Value.SentClearMessage(0);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        lock (_gate)
        {
            _trailing?.Stop();
            _trailing?.Dispose();
            _trailing = null;

            _idle?.Stop();
            _idle?.Dispose();
            _idle = null;
        }
    }

    private void ArmIdleLocked()
    {
        if (!_chatSettings.ChatLiveTypingAutoFinalize)
        {
            _idle?.Stop();
            return;
        }

        _idle ??= BuildIdleTimer();
        _idle.Stop();
        _idle.Interval = _chatSettings.ChatLiveTypingFinalizeMs;
        _idle.Start();
    }

    private System.Timers.Timer BuildIdleTimer()
    {
        var timer = new System.Timers.Timer { AutoReset = false };
        timer.Elapsed += (_, _) =>
        {
            lock (_gate)
            {
                if (_disposed || !_holding || !_chatSettings.ChatLiveTypingAutoFinalize)
                    return;
            }

            FinalizeRequested?.Invoke();
        };

        return timer;
    }

    private void ArmTrailingLocked(TimeSpan due)
    {
        _trailing ??= BuildTrailingTimer();
        _trailing.Stop();
        _trailing.Interval = Math.Max(1, due.TotalMilliseconds);
        _trailing.Start();
    }

    private System.Timers.Timer BuildTrailingTimer()
    {
        var timer = new System.Timers.Timer { AutoReset = false };
        timer.Elapsed += (_, _) =>
        {
            lock (_gate)
            {
                if (_disposed || !_holding)
                    return;

                PushLocked();
            }
        };
        return timer;
    }

    private void PushLocked()
    {
        if (_pending.Length == 0 || string.Equals(_pending, _pushed, StringComparison.Ordinal))
            return;

        string line = _pending;
        _pushed = line;
        _lastPushUtc = DateTime.UtcNow;

        _oscDisplay.OscToSent = line;
        _oscDisplay.OscMsgCount = line.Length;
        _oscDisplay.OscMsgCountUI = $"{line.Length}/{Core.Constants.OscMaxMessageLength}";

        _ = _oscSender.Value.SendOSCMessage(fx: false, delay: 0, force: true, explicitText: line);
    }
}
