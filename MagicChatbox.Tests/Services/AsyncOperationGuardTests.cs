using System;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Services;
using Xunit;

namespace MagicChatbox.Tests.Services;

public class AsyncOperationGuardTests
{
    [Fact]
    public async Task AnOperationStillRunningFromLastTimeIsNotStartedAgain()
    {
        // Giving up on a call does not stop it — a blocked native call keeps its thread until it
        // returns. Starting another on the next tick would add a second stuck thread to the first,
        // and so on every tick, until enough of the pool is parked that unrelated work stops too.
        var guard = new AsyncOperationGuard();
        var stuck = new TaskCompletionSource();
        int starts = 0;

        await guard.RunGuardedAsync("stuck", () => { starts++; return stuck.Task; }, TimeSpan.FromMilliseconds(50));
        Assert.Equal(1, starts);

        for (int tick = 0; tick < 5; tick++)
            await guard.RunGuardedAsync("stuck", () => { starts++; return stuck.Task; }, TimeSpan.FromMilliseconds(50));

        Assert.Equal(1, starts);

        // Once it finally lets go, the next tick is free to try again.
        stuck.SetResult();
        await guard.RunGuardedAsync("stuck", () => { starts++; return Task.CompletedTask; }, TimeSpan.FromMilliseconds(50));

        Assert.Equal(2, starts);
    }

    [Fact]
    public async Task AnOperationThatKeepsUpIsRunEveryTime()
    {
        // The guard above must not get in the way of an operation that behaves.
        var guard = new AsyncOperationGuard();
        int starts = 0;

        for (int tick = 0; tick < 5; tick++)
            await guard.RunGuardedAsync("healthy", () => { starts++; return Task.CompletedTask; }, TimeSpan.FromSeconds(1));

        Assert.Equal(5, starts);
    }

    [Fact]
    public async Task RepeatedFailuresStillDisableTheOperation()
    {
        // The existing back-off must survive the change above.
        var guard = new AsyncOperationGuard { MaxConsecutiveFailures = 2 };

        for (int attempt = 0; attempt < 2; attempt++)
            await guard.RunGuardedAsync("failing", () => Task.FromException(new InvalidOperationException("no")));

        Assert.True(guard.IsDisabled("failing"));
    }
}
