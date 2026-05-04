using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Guards async operations against cascading failures.
/// After <see cref="MaxConsecutiveFailures"/> the operation is auto-disabled
/// until <see cref="Reset"/> is called or the cooldown expires.
/// </summary>
public sealed class AsyncOperationGuard
{
    public int MaxConsecutiveFailures { get; set; } = 3;
    public TimeSpan CooldownAfterDisable { get; set; } = TimeSpan.FromMinutes(2);

    private readonly ConcurrentDictionary<string, FaultState> _states = new();

    /// <summary>
    /// Runs <paramref name="action"/> if the operation hasn't been faulted out.
    /// Tracks success/failure and auto-disables after repeated failures.
    /// </summary>
    public async Task RunGuardedAsync(string operationName, Func<Task> action)
    {
        var state = _states.GetOrAdd(operationName, _ => new FaultState());

        if (state.IsDisabled)
        {
            if (DateTime.UtcNow - state.DisabledAtUtc < CooldownAfterDisable)
                return;

            state.ConsecutiveFailures = 0;
            state.IsDisabled = false;
            Logging.WriteInfo($"[AsyncOperationGuard] Re-enabling '{operationName}' after cooldown");
        }

        try
        {
            await action().ConfigureAwait(false);
            state.ConsecutiveFailures = 0;
        }
        catch (Exception ex)
        {
            state.ConsecutiveFailures++;
            Logging.WriteException(ex, MSGBox: false);

            if (state.ConsecutiveFailures >= MaxConsecutiveFailures)
            {
                state.IsDisabled = true;
                state.DisabledAtUtc = DateTime.UtcNow;
                Logging.WriteInfo(
                    $"[AsyncOperationGuard] Auto-disabled '{operationName}' after {state.ConsecutiveFailures} consecutive failures. " +
                    $"Will retry after {CooldownAfterDisable.TotalMinutes:0.#} min.");
            }
        }
    }

    /// <summary>
    /// Runs a synchronous action with the same fault-tracking semantics.
    /// </summary>
    public async Task RunGuardedAsync(string operationName, Action action)
    {
        await RunGuardedAsync(operationName, () =>
        {
            action();
            return Task.CompletedTask;
        }).ConfigureAwait(false);
    }

    /// <summary>Returns true if the named operation is currently auto-disabled.</summary>
    public bool IsDisabled(string operationName)
    {
        return _states.TryGetValue(operationName, out var s) && s.IsDisabled;
    }

    /// <summary>Resets fault state for one or all operations.</summary>
    public void Reset(string? operationName = null)
    {
        if (operationName is not null)
        {
            _states.TryRemove(operationName, out _);
        }
        else
        {
            _states.Clear();
        }
    }

    private sealed class FaultState
    {
        public int ConsecutiveFailures;
        public bool IsDisabled;
        public DateTime DisabledAtUtc;
    }
}
