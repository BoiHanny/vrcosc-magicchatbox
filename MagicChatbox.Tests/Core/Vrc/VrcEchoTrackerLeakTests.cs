using MagicChatbox.Vocabulary;
using MagicChatbox.Vrc;
using System;
using System.Threading;
using Xunit;

namespace MagicChatbox.Tests.Core.Vrc;

// Every avatar write registers a wait. Almost nothing awaits them, and 194 of 197 avatars declare
// none of the parameters this app writes - so nothing ever echoes back to clear them. Before this
// fix those registrations lived for the whole session, and TryConfirm walked all of them under a
// lock on the OSC receive loop for every inbound message. At the measured 2,300 messages a second
// that is receive latency that grows the longer the app runs, which is a far worse symptom than the
// memory it also wastes.
public class VrcEchoTrackerLeakTests
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(120);

    private static SignalKey Key(string name) => SignalKey.Intern("avatar.param." + name);

    [Fact]
    public void Writes_that_are_never_echoed_do_not_accumulate_forever()
    {
        var epoch = new VrcAvatarEpoch();
        using var tracker = new VrcEchoTracker(epoch, ShortTimeout);

        for (int i = 0; i < 50; i++)
            tracker.Register(Guid.NewGuid(), Key($"Never/{i}"), SignalValue.Bool(true));

        Assert.Equal(50, tracker.PendingCount);

        Thread.Sleep(250);

        // Any inbound observation sweeps what has gone stale on its way past.
        tracker.TryConfirm(Key("Something/Else"), SignalValue.Bool(false));

        Assert.Equal(0, tracker.PendingCount);
    }

    [Fact]
    public void A_write_still_inside_the_window_is_not_swept()
    {
        var epoch = new VrcAvatarEpoch();
        using var tracker = new VrcEchoTracker(epoch, TimeSpan.FromSeconds(30));

        tracker.Register(Guid.NewGuid(), Key("Toggles/Hat"), SignalValue.Bool(true));

        tracker.TryConfirm(Key("Something/Else"), SignalValue.Bool(false));

        Assert.Equal(1, tracker.PendingCount);
    }

    [Fact]
    public void A_matching_echo_still_confirms()
    {
        // The sweep must not eat the thing it exists to protect.
        var epoch = new VrcAvatarEpoch();
        using var tracker = new VrcEchoTracker(epoch, TimeSpan.FromSeconds(30));

        tracker.Register(Guid.NewGuid(), Key("Toggles/Hat"), SignalValue.Bool(true));

        Assert.True(tracker.TryConfirm(Key("Toggles/Hat"), SignalValue.Bool(true)));
        Assert.Equal(0, tracker.PendingCount);
    }

    [Fact]
    public void A_swept_write_is_counted_as_a_timeout_rather_than_vanishing()
    {
        var epoch = new VrcAvatarEpoch();
        using var tracker = new VrcEchoTracker(epoch, ShortTimeout);

        tracker.Register(Guid.NewGuid(), Key("Never/1"), SignalValue.Bool(true));
        Thread.Sleep(250);
        tracker.TryConfirm(Key("Other"), SignalValue.Bool(false));

        Assert.True(tracker.Timings.TimedOut >= 1);
    }

    [Fact]
    public void A_swept_write_completes_its_waiter_instead_of_hanging_it()
    {
        var epoch = new VrcAvatarEpoch();
        using var tracker = new VrcEchoTracker(epoch, ShortTimeout);

        VrcEchoWait wait = tracker.Register(Guid.NewGuid(), Key("Never/1"), SignalValue.Bool(true));

        Thread.Sleep(250);
        tracker.TryConfirm(Key("Other"), SignalValue.Bool(false));

        Assert.True(wait.IsSettled);
        Assert.Equal(VrcEchoStatus.TimedOut, wait.Completion.Result);
    }

    [Fact]
    public void Writes_made_to_a_previous_avatar_are_dropped_on_the_next_observation()
    {
        // An echo that matches by value on a new avatar must never confirm a write aimed at the old
        // one, and the stale registration should not linger either.
        var epoch = new VrcAvatarEpoch();
        using var tracker = new VrcEchoTracker(epoch, TimeSpan.FromSeconds(30));

        tracker.Register(Guid.NewGuid(), Key("Toggles/Hat"), SignalValue.Bool(true));
        Assert.Equal(1, tracker.PendingCount);

        epoch.AdvanceToAvatar("avtr_something_else");

        Assert.False(tracker.TryConfirm(Key("Toggles/Hat"), SignalValue.Bool(true)));
        Assert.Equal(0, tracker.PendingCount);
    }

    [Fact]
    public void With_nothing_pending_the_receive_path_costs_a_read_and_a_return()
    {
        var epoch = new VrcAvatarEpoch();
        using var tracker = new VrcEchoTracker(epoch, ShortTimeout);

        Assert.False(tracker.TryConfirm(Key("Anything"), SignalValue.Bool(true)));
        Assert.Equal(0, tracker.PendingCount);
    }
}
