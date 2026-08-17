using MagicChatbox.Tests.TestDoubles;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using vrcosc_magicchatbox.Core.Vrc;
using Xunit;

namespace MagicChatbox.Tests.Core.Vrc;

// The owner's report: "the OSC is very slow and it used to be very quick in the other ones."
//
// It was. Neither V2 nor V3 has anything like this pump - V2's transport wakes on enqueue and drains
// the whole outbox into one bundle, so a write reaches the socket immediately. V1 put a fixed 50 ms
// tick with an 8-message budget in front of the same socket, which is 160 messages a second and up to
// 50 ms of latency on every single write, including the one you get from touching a toggle.
//
// These pin the two properties that matter to somebody using the app: one write goes out promptly,
// and a batch does not take seconds. They are wall-clock tests, so the bounds are generous - they are
// here to catch a regression of seconds, not to measure milliseconds.
public class AvatarParameterPumpLatencyTests
{
    private static bool WaitFor(Func<bool> condition, TimeSpan timeout)
        => SpinWait.SpinUntil(condition, timeout);

    [Fact]
    public void One_write_reaches_the_socket_promptly()
    {
        var egress = new FakeVrcEgress();
        using var pump = new AvatarParameterPump();
        pump.Start(egress);

        var clock = Stopwatch.StartNew();
        pump.Publish("Toggles/Hat", true);

        Assert.True(
            WaitFor(() => egress.Writes.Count > 0, TimeSpan.FromSeconds(2)),
            "a single parameter write never reached the egress");

        clock.Stop();

        Assert.True(
            clock.ElapsedMilliseconds < 250,
            $"one write took {clock.ElapsedMilliseconds} ms to reach the socket");
    }

    [Fact]
    public void A_whole_look_goes_out_in_well_under_a_second()
    {
        // A preset on the owner's machine is a median of 16 values, and the biggest avatar here has
        // 212 saved. Under the 8-per-50ms budget, 212 writes could not go out in less than 1.3
        // seconds however fast the socket was.
        const int count = 212;

        var egress = new FakeVrcEgress();
        using var pump = new AvatarParameterPump();
        pump.Start(egress);

        var clock = Stopwatch.StartNew();

        for (int i = 0; i < count; i++)
            pump.Publish($"Look/Item{i}", true);

        Assert.True(
            WaitFor(() => egress.Writes.Count >= count, TimeSpan.FromSeconds(10)),
            $"only {egress.Writes.Count} of {count} writes arrived");

        clock.Stop();

        Assert.True(
            clock.ElapsedMilliseconds < 1000,
            $"{count} writes took {clock.ElapsedMilliseconds} ms; a saved look should not take a second to put on");
    }

    [Fact]
    public void Repeated_writes_to_one_parameter_still_collapse_to_the_latest()
    {
        // Worth keeping even though the tick is not. A slider drag publishes continuously and VRChat
        // only cares where the thumb ended up; sending every intermediate value is what a rate limit
        // was reaching for, and coalescing gets it without delaying anything.
        var egress = new FakeVrcEgress();
        using var pump = new AvatarParameterPump();
        pump.Start(egress);

        for (int i = 0; i < 200; i++)
            pump.Publish("Face/Blush", i / 200f);

        pump.Publish("Face/Blush", 1f);

        Assert.True(
            WaitFor(() => egress.LastValueOf("Face/Blush") is { } v && Math.Abs(v.AsFloat() - 1f) < 0.001f,
                TimeSpan.FromSeconds(2)),
            "the last value of a rapid series never arrived");

        Assert.True(
            egress.CountOf("Face/Blush") < 200,
            $"every intermediate value was sent ({egress.CountOf("Face/Blush")}); they should collapse");
    }

    [Fact]
    public void Changing_avatar_does_not_replay_the_previous_avatar_s_values()
    {
        // Reset re-staged every slot the app had ever driven, and the slot table was never cleared, so
        // putting on a new avatar sent it the values of the old one. On any name the two avatars share
        // - VRCEmote is on 221 of the avatars on this machine, GoGo Loco's on 180 - that is the new
        // avatar being visibly set to the old one's state.
        var egress = new FakeVrcEgress();
        using var pump = new AvatarParameterPump();
        pump.Start(egress);

        pump.Publish("Toggles/OldAvatarOnly", true);

        Assert.True(
            WaitFor(() => egress.CountOf("Toggles/OldAvatarOnly") > 0, TimeSpan.FromSeconds(2)),
            "the first write never went out");

        int before = egress.Writes.Count;

        pump.ForgetAvatar();

        Thread.Sleep(300);

        Assert.Equal(before, egress.Writes.Count);
    }
}
