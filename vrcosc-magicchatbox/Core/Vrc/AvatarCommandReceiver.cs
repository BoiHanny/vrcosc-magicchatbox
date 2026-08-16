using MagicChatbox.Vocabulary;
using MagicChatbox.Vrc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using vrcosc_magicchatbox.Classes.DataAndSecurity;

namespace vrcosc_magicchatbox.Core.Vrc;

public readonly record struct AvatarCommandStats(
    long Observed,
    long Matched,
    long Dispatched,
    long SuppressedByEdge,
    long SuppressedByRate,
    long SuppressedByEpoch,
    long SuppressedByGate,
    long Faulted);

public sealed class AvatarCommandReceiver : IVrcObservationSink
{
    public const string KeyPrefix = "avatar.param.";

    private static readonly TimeSpan AvatarSettlingWindow = TimeSpan.FromSeconds(1);
    private static readonly double TicksPerMs = Stopwatch.Frequency / 1000d;

    private sealed class CommandState
    {
        public double LastValue;
        public bool HasLastValue;
        public long LastDispatchTicks;
    }

    private readonly IReadOnlyDictionary<string, InboundCommand> _commands;
    private readonly Func<bool> _isEnabled;
    private readonly Action<Action> _marshal;
    private readonly Dictionary<string, CommandState> _state = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    private long _epoch = long.MinValue;
    private long _epochChangedTicks;

    private long _observed;
    private long _matched;
    private long _dispatched;
    private long _suppressedByEdge;
    private long _suppressedByRate;
    private long _suppressedByEpoch;
    private long _suppressedByGate;
    private long _faulted;

    public AvatarCommandReceiver(
        IEnumerable<InboundCommand> commands,
        Func<bool> isEnabled,
        Action<Action> marshal)
    {
        ArgumentNullException.ThrowIfNull(commands);

        var map = new Dictionary<string, InboundCommand>(StringComparer.OrdinalIgnoreCase);
        foreach (InboundCommand command in commands)
            map[command.Name] = command;

        _commands = map;
        _isEnabled = isEnabled ?? throw new ArgumentNullException(nameof(isEnabled));
        _marshal = marshal ?? throw new ArgumentNullException(nameof(marshal));
    }

    public AvatarCommandStats Stats => new(
        Interlocked.Read(ref _observed),
        Interlocked.Read(ref _matched),
        Interlocked.Read(ref _dispatched),
        Interlocked.Read(ref _suppressedByEdge),
        Interlocked.Read(ref _suppressedByRate),
        Interlocked.Read(ref _suppressedByEpoch),
        Interlocked.Read(ref _suppressedByGate),
        Interlocked.Read(ref _faulted));

    public void OnObservation(in VrcObservation observation)
    {
        Interlocked.Increment(ref _observed);

        long now = Stopwatch.GetTimestamp();

        lock (_gate)
        {
            if (observation.AvatarEpoch != _epoch)
            {
                _epoch = observation.AvatarEpoch;
                _epochChangedTicks = now;
                _state.Clear();
            }
        }

        if (!IsEnabled())
        {
            Interlocked.Increment(ref _suppressedByGate);
            return;
        }

        string key = observation.Key.Value;
        if (!key.StartsWith(KeyPrefix, StringComparison.Ordinal))
            return;

        string name = key[KeyPrefix.Length..];
        if (!_commands.TryGetValue(name, out InboundCommand? command))
            return;

        Interlocked.Increment(ref _matched);

        if (!TryReadValue(observation.Value, out double value))
            return;

        lock (_gate)
        {
            if (_epochChangedTicks != 0 && Elapsed(_epochChangedTicks, now) < AvatarSettlingWindow)
            {
                Remember(name, value, now, dispatched: false);
                Interlocked.Increment(ref _suppressedByEpoch);
                return;
            }

            if (!_state.TryGetValue(name, out CommandState? state))
            {
                state = new CommandState();
                _state[name] = state;
            }

            bool fire = command.Trigger switch
            {
                InboundTrigger.RisingEdge => value != 0 && (!state.HasLastValue || state.LastValue == 0),
                _ => !state.HasLastValue || state.LastValue != value,
            };

            state.LastValue = value;
            state.HasLastValue = true;

            if (!fire)
            {
                Interlocked.Increment(ref _suppressedByEdge);
                return;
            }

            if (state.LastDispatchTicks != 0 && Elapsed(state.LastDispatchTicks, now) < command.MinInterval)
            {
                Interlocked.Increment(ref _suppressedByRate);
                return;
            }

            state.LastDispatchTicks = now;
        }

        Dispatch(command, value != 0);
    }

    public void ResetForNewAvatar()
    {
        lock (_gate)
        {
            _state.Clear();
            _epochChangedTicks = Stopwatch.GetTimestamp();
        }
    }

    private void Remember(string name, double value, long now, bool dispatched)
    {
        if (!_state.TryGetValue(name, out CommandState? state))
        {
            state = new CommandState();
            _state[name] = state;
        }

        state.LastValue = value;
        state.HasLastValue = true;

        if (dispatched)
            state.LastDispatchTicks = now;
    }

    private bool IsEnabled()
    {
        try
        {
            return _isEnabled();
        }
        catch
        {
            return false;
        }
    }

    private void Dispatch(InboundCommand command, bool value)
    {
        Interlocked.Increment(ref _dispatched);

        try
        {
            _marshal(() =>
            {
                try
                {
                    command.Invoke(value);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref _faulted);
                    Logging.WriteException(ex, MSGBox: false);
                }
            });
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _faulted);
            Logging.WriteException(ex, MSGBox: false);
        }
    }

    private static bool TryReadValue(SignalValue value, out double result)
    {
        switch (value.Kind)
        {
            case SignalKind.Bool:
                result = value.AsBool() ? 1d : 0d;
                return true;

            case SignalKind.Int:
                result = value.AsInt();
                return true;

            case SignalKind.Float:
                if (!value.IsFinite())
                {
                    result = 0d;
                    return false;
                }

                result = value.AsFloat();
                return true;

            default:
                result = 0d;
                return false;
        }
    }

    private static TimeSpan Elapsed(long since, long now)
        => TimeSpan.FromMilliseconds((now - since) / TicksPerMs);
}
