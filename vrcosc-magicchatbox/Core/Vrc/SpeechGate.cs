using System;
using System.Diagnostics;

namespace vrcosc_magicchatbox.Core.Vrc;

public sealed class SpeechGate
{
    public const string VoiceParameter = "Voice";
    public const string MuteParameter = "MuteSelf";

    private static readonly double TicksPerMs = Stopwatch.Frequency / 1000d;

    private readonly object _gate = new();
    private readonly double _threshold;
    private readonly TimeSpan _hangover;

    private long _lastAboveTicks;
    private bool _muted;
    private bool _speaking;

    public SpeechGate(double threshold = 0.02, TimeSpan? hangover = null)
    {
        _threshold = threshold;
        _hangover = hangover ?? TimeSpan.FromMilliseconds(700);
    }

    public bool IsSpeaking
    {
        get
        {
            lock (_gate)
            {
                if (!_speaking)
                    return false;

                if (_lastAboveTicks != 0 && Elapsed(_lastAboveTicks) > _hangover)
                    _speaking = false;

                return _speaking;
            }
        }
    }

    public bool IsMuted
    {
        get { lock (_gate) return _muted; }
    }

    public bool IsHotMic
    {
        get { lock (_gate) return !_muted && _speaking; }
    }

    public void ObserveVoice(double level)
    {
        lock (_gate)
        {
            if (level > _threshold)
            {
                _lastAboveTicks = Stopwatch.GetTimestamp();
                _speaking = true;
                return;
            }

            if (_speaking && _lastAboveTicks != 0 && Elapsed(_lastAboveTicks) > _hangover)
                _speaking = false;
        }
    }

    public void ObserveMute(bool muted)
    {
        lock (_gate) _muted = muted;
    }

    public void Reset()
    {
        lock (_gate)
        {
            _speaking = false;
            _lastAboveTicks = 0;
        }
    }

    private static TimeSpan Elapsed(long since)
        => TimeSpan.FromMilliseconds((Stopwatch.GetTimestamp() - since) / TicksPerMs);
}
