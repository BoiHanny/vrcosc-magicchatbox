using System;
using System.Collections.Concurrent;
using System.Threading;
using vrcosc_magicchatbox.Classes.DataAndSecurity;

namespace vrcosc_magicchatbox.Core.Osc;

/// <summary>
/// Tracks consecutive failures per OSC provider and auto-disables providers
/// that fail repeatedly, preventing cascading failures in the scan loop.
/// Uses a half-open/probe pattern: after cooldown, allows ONE probe call.
/// Only clears fault state on a successful probe — permanently broken
/// providers won't flap endlessly.
/// </summary>
public sealed class ModuleFaultTracker
{
    // A granted probe that is never resolved by RecordSuccess/RecordFailure
    // (e.g. the provider was disabled before TryBuild could run) is reclaimed
    // after this long so the provider can't stay latched as faulted forever.
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<string, FaultState> _states = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns true if the provider should be skipped due to repeated failures.
    /// After cooldown, allows exactly one probe attempt before re-faulting.
    /// </summary>
    public bool IsFaulted(string sortKey)
    {
        if (!_states.TryGetValue(sortKey, out var state))
            return false;

        if (Volatile.Read(ref state.ConsecutiveFailures) < Constants.ModuleMaxConsecutiveFailures)
            return false;

        // Faulted — check if cooldown has expired for a probe attempt
        long lastFailTicks = Volatile.Read(ref state.LastFailureTicksUtc);
        long nowTicks = DateTime.UtcNow.Ticks;
        if (nowTicks - lastFailTicks > Constants.ModuleFaultCooldown.Ticks)
        {
            // Allow exactly one probe by temporarily setting probing flag
            if (Interlocked.CompareExchange(ref state.IsProbing, 1, 0) == 0)
            {
                Volatile.Write(ref state.ProbeStartedTicksUtc, nowTicks);
                return false; // This caller gets to probe
            }

            // A probe token is outstanding but stale — reclaim it and grant
            // this caller a fresh probe. The CAS ensures only one caller
            // reclaims any given expired probe.
            long probeStarted = Volatile.Read(ref state.ProbeStartedTicksUtc);
            if (nowTicks - probeStarted > ProbeTimeout.Ticks
                && Interlocked.CompareExchange(ref state.ProbeStartedTicksUtc, nowTicks, probeStarted) == probeStarted)
            {
                return false; // This caller takes over the stale probe
            }
        }

        return true;
    }

    /// <summary>
    /// Record a successful execution — fully resets the failure counter and probe state.
    /// </summary>
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

    /// <summary>
    /// Record a failure. After <see cref="Constants.ModuleMaxConsecutiveFailures"/>
    /// consecutive failures, the provider is temporarily disabled.
    /// If this was a probe attempt, re-faults immediately.
    /// </summary>
    public void RecordFailure(string sortKey, Exception? ex = null)
    {
        var state = _states.GetOrAdd(sortKey, _ => new FaultState());
        int count = Interlocked.Increment(ref state.ConsecutiveFailures);
        Volatile.Write(ref state.LastFailureTicksUtc, DateTime.UtcNow.Ticks);
        Volatile.Write(ref state.IsProbing, 0); // Reset probe flag on failure

        if (count == Constants.ModuleMaxConsecutiveFailures)
        {
            Logging.WriteInfo(
                $"[ModuleFaultTracker] Provider '{sortKey}' auto-disabled after " +
                $"{Constants.ModuleMaxConsecutiveFailures} consecutive failures. " +
                $"Will probe after {Constants.ModuleFaultCooldown.TotalSeconds}s cooldown. " +
                $"Last error: {ex?.Message ?? "unknown"}");
        }
    }

    /// <summary>
    /// Get the number of consecutive failures for a provider (for diagnostics).
    /// </summary>
    public int GetFailureCount(string sortKey)
    {
        return _states.TryGetValue(sortKey, out var state)
            ? Volatile.Read(ref state.ConsecutiveFailures)
            : 0;
    }

    /// <summary>
    /// Manually reset a provider's fault state (e.g., from UI toggle).
    /// </summary>
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
        public int IsProbing; // 0 = not probing, 1 = probe in progress
        public long LastFailureTicksUtc;
        public long ProbeStartedTicksUtc; // when the outstanding probe token was granted
    }
}

