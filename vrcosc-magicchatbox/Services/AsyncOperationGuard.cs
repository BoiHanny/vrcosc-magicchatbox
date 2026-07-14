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
    public async Task RunGuardedAsync(string operationName, Func<Task> action, TimeSpan? timeout = null)
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
            Task operationTask = action();
            if (timeout.HasValue)
                await WaitForOperationAsync(operationName, operationTask, timeout.Value).ConfigureAwait(false);
            else
                await operationTask.ConfigureAwait(false);

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

    private static async Task WaitForOperationAsync(string operationName, Task operationTask, TimeSpan timeout)
    {
        Task timeoutTask = Task.Delay(timeout);
        Task completedTask = await Task.WhenAny(operationTask, timeoutTask).ConfigureAwait(false);
        if (completedTask == operationTask)
        {
            await operationTask.ConfigureAwait(false);
            return;
        }

        _ = operationTask.ContinueWith(
            completed =>
            {
                if (completed.Exception != null)
                {
                    Logging.WriteException(
                        new Exception($"Guarded operation '{operationName}' failed after timing out.", completed.Exception),
                        MSGBox: false);
                }
            },
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);

        throw new TimeoutException(
            $"Guarded operation '{operationName}' timed out after {timeout.TotalSeconds:0.#}s.");
    }

    /// <summary>
    /// Runs a synchronous action with the same fault-tracking semantics.
    /// </summary>
    public async Task RunGuardedAsync(string operationName, Action action, TimeSpan? timeout = null)
    {
        await RunGuardedAsync(operationName, () =>
        {
            action();
            return Task.CompletedTask;
        }, timeout).ConfigureAwait(false);
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
