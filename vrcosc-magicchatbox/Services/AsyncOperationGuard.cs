using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;

namespace vrcosc_magicchatbox.Services;

public sealed class AsyncOperationGuard
{
    public int MaxConsecutiveFailures { get; set; } = 3;
    public TimeSpan CooldownAfterDisable { get; set; } = TimeSpan.FromMinutes(2);

    private readonly ConcurrentDictionary<string, FaultState> _states = new();

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

        // An attempt that ran out of time may still be going. A blocked native call cannot be
        // cancelled from here — the thread stays in it until it returns — so the most that can be
        // done is refuse to start another. Without this, every cycle adds one more stuck thread to
        // the last, until enough of the pool is parked that unrelated work stops running too.
        Task? previous = Volatile.Read(ref state.InFlight);
        if (previous is { IsCompleted: false })
        {
            if (!state.LoggedStillRunning)
            {
                state.LoggedStillRunning = true;
                Logging.WriteInfo(
                    $"[AsyncOperationGuard] '{operationName}' is still running from an earlier attempt; not starting another.");
            }

            return;
        }

        state.LoggedStillRunning = false;

        try
        {
            Task operationTask = action();
            Volatile.Write(ref state.InFlight, operationTask);

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

    public async Task RunGuardedAsync(string operationName, Action action, TimeSpan? timeout = null)
    {
        await RunGuardedAsync(operationName, () =>
        {
            action();
            return Task.CompletedTask;
        }, timeout).ConfigureAwait(false);
    }

    public bool IsDisabled(string operationName)
    {
        return _states.TryGetValue(operationName, out var s) && s.IsDisabled;
    }

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

        /// <summary>The last attempt, kept even after we stopped waiting for it.</summary>
        public Task? InFlight;

        /// <summary>So a long stall is reported once rather than on every cycle.</summary>
        public bool LoggedStillRunning;
    }
}
