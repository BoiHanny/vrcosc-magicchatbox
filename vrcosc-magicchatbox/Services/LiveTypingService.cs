using System;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Streams the line being typed straight into the chatbox.
/// </summary>
/// <remarks>
/// Keystrokes arrive far faster than the chatbox will accept them, so this is a rate limiter with a
/// send attached rather than a send with a rate limit attached: the first keystroke goes out at once
/// and everything after it collapses into one trailing push per interval. The trailing push is the
/// part that matters - without it the last few characters someone types before they stop would never
/// reach the chatbox, which is precisely the state they are left looking at.
/// </remarks>
public sealed class LiveTypingService : ILiveTypingService, IDisposable
{
    private readonly ChatSettings _chatSettings;
    private readonly IAppState _appState;
    private readonly Lazy<IOscSender> _oscSender;
    private readonly OscDisplayState _oscDisplay;

    private readonly object _gate = new();
    private System.Timers.Timer? _trailing;
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

    /// <remarks>
    /// The master switch is part of the answer. Nothing reaches VRChat while it is off, so a line
    /// held from before it was thrown is holding the chatbox against no one - and since the hold is
    /// only ever refreshed by a keystroke, it would keep the integrations parked indefinitely for
    /// someone who simply stopped typing and turned sending off.
    /// </remarks>
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

        // An empty box is not a line being typed, it is a line abandoned. Hand the chatbox back
        // rather than parking an empty string in it.
        string line = (text ?? string.Empty).TrimEnd();
        if (line.Length == 0)
        {
            Release(clearChatbox: true);
            return;
        }

        // No chat icon prefix here on purpose. The icon rotation advances every time it is asked for
        // a glyph, so prefixing each push would spin through the whole collection mid-sentence, and
        // the icon reads as a marker of a message that has been sent rather than one being written.
        if (line.Length > Core.Constants.OscMaxMessageLength)
            line = line[..Core.Constants.OscMaxMessageLength];

        lock (_gate)
        {
            _pending = line;
            _holding = true;

            var wait = _lastPushUtc.AddMilliseconds(_chatSettings.ChatLiveTypingRateMs) - DateTime.UtcNow;
            if (wait <= TimeSpan.Zero)
                PushLocked();
            else
                ArmTrailingLocked(wait);
        }
    }

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
        }
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

        // No notification sound. That chime is for a message someone chose to send, and firing it
        // once a second while a sentence is being written is the fastest way to make everyone
        // nearby mute the app.
        _ = _oscSender.Value.SendOSCMessage(fx: false, delay: 0, force: true, explicitText: line);
    }
}
