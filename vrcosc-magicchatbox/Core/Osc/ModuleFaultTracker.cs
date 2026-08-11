using System;
using System.Collections.Concurrent;
using System.Threading;
using vrcosc_magicchatbox.Classes.DataAndSecurity;

namespace vrcosc_magicchatbox.Core.Osc;

public sealed class ModuleFaultTracker
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<string, FaultState> _states = new(StringComparer.OrdinalIgnoreCase);

    public bool IsFaulted(string sortKey)
    {
        if (!_states.TryGetValue(sortKey, out var state))
            return false;

        if (Volatile.Read(ref state.ConsecutiveFailures) < Constants.ModuleMaxConsecutiveFailures)
            return false;

        long lastFailTicks = Volatile.Read(ref state.LastFailureTicksUtc);
        long nowTicks = DateTime.UtcNow.Ticks;
        if (nowTicks - lastFailTicks > Constants.ModuleFaultCooldown.Ticks)
        {
            if (Interlocked.CompareExchange(ref state.IsProbing, 1, 0) == 0)
            {
                Volatile.Write(ref state.ProbeStartedTicksUtc, nowTicks);
                return false;            }

            long probeStarted = Volatile.Read(ref state.ProbeStartedTicksUtc);
            if (nowTicks - probeStarted > ProbeTimeout.Ticks
                && Interlocked.CompareExchange(ref state.ProbeStartedTicksUtc, nowTicks, probeStarted) == probeStarted)
            {
                return false;            }
        }

        return true;
    }

    public void RecordSuccess(string sortKey)
    {
        if (_states.TryGetValue(sortKey, out var state))
        {
            bool wasFaulted = Volatile.Read(ref state.ConsecutiveFailures) >= Constants.ModuleMaxConsecutiveFailures;
            Volatile.Write(ref state.ConsecutiveFailures, 0);
            Volatile.Write(ref state.IsProbing, 0);

            if (wasFaulted)
            {
                Logging.WriteInfo(
                    $"[ModuleFaultTracker] Provider '{sortKey}' recovered after successful probe.");
            }
        }
    }

    public void RecordFailure(string sortKey, Exception? ex = null)
    {
        var state = _states.GetOrAdd(sortKey, _ => new FaultState());
        int count = Interlocked.Increment(ref state.ConsecutiveFailures);
        Volatile.Write(ref state.LastFailureTicksUtc, DateTime.UtcNow.Ticks);
        Volatile.Write(ref state.IsProbing, 0);
        if (count == Constants.ModuleMaxConsecutiveFailures)
        {
            Logging.WriteInfo(
                $"[ModuleFaultTracker] Provider '{sortKey}' auto-disabled after " +
                $"{Constants.ModuleMaxConsecutiveFailures} consecutive failures. " +
                $"Will probe after {Constants.ModuleFaultCooldown.TotalSeconds}s cooldown. " +
                $"Last error: {ex?.Message ?? "unknown"}");
        }
    }

    public int GetFailureCount(string sortKey)
    {
        return _states.TryGetValue(sortKey, out var state)
            ? Volatile.Read(ref state.ConsecutiveFailures)
            : 0;
    }

    public void ResetFault(string sortKey)
    {
        if (_states.TryGetValue(sortKey, out var state))
        {
            Volatile.Write(ref state.ConsecutiveFailures, 0);
            Volatile.Write(ref state.IsProbing, 0);
        }
    }

    private sealed class FaultState
    {
        public int ConsecutiveFailures;
        public int IsProbing;        public long LastFailureTicksUtc;
        public long ProbeStartedTicksUtc;    }
}

