using MagicChatbox.Vocabulary;
using MagicChatbox.Vrc;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Core.Vrc;
using Xunit;

namespace MagicChatbox.Tests.Core.Vrc;

// Inbound is the direction where a mistake costs the user something: a world, an animator glitch or
// an avatar load can push values at this app as fast as the network allows. Every test here is a
// guard, and the ones about avatar loading matter most - VRChat blasts every parameter's default the
// moment an avatar comes up, and that must not read as somebody pressing every button at once.
public class AvatarCommandReceiverTests
{
    private const long Epoch = 7;

    private static VrcObservation Param(string name, SignalValue value, long epoch = Epoch)
        => new(SignalKey.Intern(AvatarCommandReceiver.KeyPrefix + name), value, epoch);

    private sealed class Harness
    {
        public readonly List<string> Fired = [];
        public bool Enabled = true;
        public AvatarCommandReceiver Receiver = null!;

        public Harness(params InboundCommand[] commands)
        {
            Receiver = new AvatarCommandReceiver(commands, () => Enabled, action => action());
        }

        public InboundCommand Impulse(string name, TimeSpan? minInterval = null)
            => new(name, InboundTrigger.RisingEdge, InboundRisk.Safe, "test", _ => Fired.Add(name))
            {
                MinInterval = minInterval ?? TimeSpan.Zero,
            };
    }

    private static Harness WithImpulse(string name, TimeSpan? minInterval = null)
    {
        var harness = new Harness();
        var command = new InboundCommand(
            name, InboundTrigger.RisingEdge, InboundRisk.Safe, "test", _ => harness.Fired.Add(name))
        {
            MinInterval = minInterval ?? TimeSpan.Zero,
        };

        harness.Receiver = new AvatarCommandReceiver(
            new[] { command }, () => harness.Enabled, action => action());

        return harness;
    }

    private static Harness WithLevel(string name, Action<double>? onFire = null)
    {
        var harness = new Harness();
        var command = new InboundCommand(
            name, InboundTrigger.Level, InboundRisk.Safe, "test",
            v => { harness.Fired.Add($"{name}={v != 0}"); onFire?.Invoke(v); })
        {
            MinInterval = TimeSpan.Zero,
        };

        harness.Receiver = new AvatarCommandReceiver(
            new[] { command }, () => harness.Enabled, action => action());

        return harness;
    }

    private static void Settle(AvatarCommandReceiver receiver)
    {
        // The first observation of an epoch starts a one second settling window. Push a value to
        // establish the epoch, then wait it out so the tests exercise steady-state behaviour.
        receiver.OnObservation(Param("MCB/Ctrl/Warmup", SignalValue.Bool(false)));
        Thread.Sleep(1100);
    }

    [Fact]
    public void An_int_payload_survives_the_dispatch()
    {
        // A command bound to an Int parameter — "restore preset 3", "switch to profile 2" — is only
        // expressible if the value reaches the handler. Collapsing it to a bool at the dispatch
        // boundary makes every non-zero selection indistinguishable.
        var seen = new List<double>();
        var command = new InboundCommand(
            "MCB/Ctrl/Select", InboundTrigger.Level, InboundRisk.Safe, "test", v => seen.Add(v))
        {
            MinInterval = TimeSpan.Zero,
        };

        var receiver = new AvatarCommandReceiver(new[] { command }, () => true, action => action());
        receiver.OnObservation(Param("MCB/Ctrl/Warmup", SignalValue.Bool(false)));
        Thread.Sleep(1100);

        receiver.OnObservation(Param("MCB/Ctrl/Select", SignalValue.Int(3)));
        receiver.OnObservation(Param("MCB/Ctrl/Select", SignalValue.Int(7)));

        Assert.Equal(new[] { 3d, 7d }, seen);
    }

    [Fact]
    public void A_float_payload_survives_the_dispatch()
    {
        var seen = new List<double>();
        var command = new InboundCommand(
            "MCB/Ctrl/Dial", InboundTrigger.Level, InboundRisk.Safe, "test", v => seen.Add(v))
        {
            MinInterval = TimeSpan.Zero,
        };

        var receiver = new AvatarCommandReceiver(new[] { command }, () => true, action => action());
        receiver.OnObservation(Param("MCB/Ctrl/Warmup", SignalValue.Bool(false)));
        Thread.Sleep(1100);

        receiver.OnObservation(Param("MCB/Ctrl/Dial", SignalValue.Float(0.25)));

        Assert.Single(seen);
        Assert.Equal(0.25d, seen[0], 5);
    }

    [Fact]
    public void An_unknown_parameter_is_ignored()
    {
        var h = WithImpulse("MCB/Ctrl/Tts/Stop");
        Settle(h.Receiver);

        h.Receiver.OnObservation(Param("SomeAvatarToggle", SignalValue.Bool(true)));

        Assert.Empty(h.Fired);
    }

    [Fact]
    public void A_rising_edge_fires_once()
    {
        var h = WithImpulse("MCB/Ctrl/Tts/Stop");
        Settle(h.Receiver);

        h.Receiver.OnObservation(Param("MCB/Ctrl/Tts/Stop", SignalValue.Bool(true)));

        Assert.Single(h.Fired);
    }

    [Fact]
    public void Holding_a_button_down_does_not_fire_repeatedly()
    {
        // An animator can resend the same value every frame. Without edge detection that is a
        // command a hundred times a second.
        var h = WithImpulse("MCB/Ctrl/Tts/Stop");
        Settle(h.Receiver);

        for (int i = 0; i < 20; i++)
            h.Receiver.OnObservation(Param("MCB/Ctrl/Tts/Stop", SignalValue.Bool(true)));

        Assert.Single(h.Fired);
        Assert.True(h.Receiver.Stats.SuppressedByEdge >= 19);
    }

    [Fact]
    public void Releasing_and_pressing_again_fires_again()
    {
        var h = WithImpulse("MCB/Ctrl/Tts/Stop");
        Settle(h.Receiver);

        h.Receiver.OnObservation(Param("MCB/Ctrl/Tts/Stop", SignalValue.Bool(true)));
        h.Receiver.OnObservation(Param("MCB/Ctrl/Tts/Stop", SignalValue.Bool(false)));
        h.Receiver.OnObservation(Param("MCB/Ctrl/Tts/Stop", SignalValue.Bool(true)));

        Assert.Equal(2, h.Fired.Count);
    }

    [Fact]
    public void A_release_on_its_own_never_fires_an_impulse()
    {
        var h = WithImpulse("MCB/Ctrl/Panic");
        Settle(h.Receiver);

        h.Receiver.OnObservation(Param("MCB/Ctrl/Panic", SignalValue.Bool(false)));

        Assert.Empty(h.Fired);
    }

    [Fact]
    public void The_avatar_load_default_storm_is_swallowed()
    {
        // This is the one that matters. A fresh avatar reports every parameter at its default, and
        // some of those defaults are true. None of it is a button press.
        var h = WithImpulse("MCB/Ctrl/Panic");

        for (int i = 0; i < 30; i++)
            h.Receiver.OnObservation(Param("MCB/Ctrl/Panic", SignalValue.Bool(true), epoch: 99));

        Assert.Empty(h.Fired);
        Assert.True(h.Receiver.Stats.SuppressedByEpoch > 0);
    }

    [Fact]
    public void Switching_avatars_clears_the_remembered_state()
    {
        var h = WithImpulse("MCB/Ctrl/Tts/Stop");
        Settle(h.Receiver);

        h.Receiver.OnObservation(Param("MCB/Ctrl/Tts/Stop", SignalValue.Bool(true)));
        Assert.Single(h.Fired);

        // New avatar, its own settling window, and the held value must not fire on arrival.
        h.Receiver.OnObservation(Param("MCB/Ctrl/Tts/Stop", SignalValue.Bool(true), epoch: Epoch + 1));
        Assert.Single(h.Fired);
    }

    [Fact]
    public void A_rate_limit_bounds_an_oscillating_parameter()
    {
        var h = WithImpulse("MCB/Ctrl/Tts/Stop", minInterval: TimeSpan.FromSeconds(30));
        Settle(h.Receiver);

        for (int i = 0; i < 50; i++)
        {
            h.Receiver.OnObservation(Param("MCB/Ctrl/Tts/Stop", SignalValue.Bool(true)));
            h.Receiver.OnObservation(Param("MCB/Ctrl/Tts/Stop", SignalValue.Bool(false)));
        }

        Assert.Single(h.Fired);
        Assert.True(h.Receiver.Stats.SuppressedByRate > 0);
    }

    [Fact]
    public void Nothing_fires_while_inbound_is_switched_off()
    {
        var h = WithImpulse("MCB/Ctrl/Panic");
        Settle(h.Receiver);
        h.Enabled = false;

        h.Receiver.OnObservation(Param("MCB/Ctrl/Panic", SignalValue.Bool(true)));

        Assert.Empty(h.Fired);
        Assert.True(h.Receiver.Stats.SuppressedByGate > 0);
    }

    [Fact]
    public void A_throwing_gate_is_treated_as_closed()
    {
        // Fail safe: if we cannot tell whether inbound is allowed, it is not.
        var fired = new List<string>();
        var command = new InboundCommand(
            "MCB/Ctrl/Panic", InboundTrigger.RisingEdge, InboundRisk.Safe, "test", _ => fired.Add("x"));

        var receiver = new AvatarCommandReceiver(
            new[] { command },
            () => throw new InvalidOperationException("settings not loaded"),
            action => action());

        receiver.OnObservation(Param("MCB/Ctrl/Panic", SignalValue.Bool(true)));

        Assert.Empty(fired);
    }

    [Fact]
    public void A_level_command_fires_on_every_change_and_not_on_repeats()
    {
        var h = WithLevel("MCB/Ctrl/Master");
        Settle(h.Receiver);

        h.Receiver.OnObservation(Param("MCB/Ctrl/Master", SignalValue.Bool(true)));
        h.Receiver.OnObservation(Param("MCB/Ctrl/Master", SignalValue.Bool(true)));
        h.Receiver.OnObservation(Param("MCB/Ctrl/Master", SignalValue.Bool(false)));

        Assert.Equal(new[] { "MCB/Ctrl/Master=True", "MCB/Ctrl/Master=False" }, h.Fired);
    }

    [Fact]
    public void A_command_that_throws_does_not_stop_the_next_one()
    {
        var fired = new List<string>();
        var bad = new InboundCommand(
            "MCB/Ctrl/Bad", InboundTrigger.RisingEdge, InboundRisk.Safe, "test",
            _ => throw new InvalidOperationException("boom"));
        var good = new InboundCommand(
            "MCB/Ctrl/Good", InboundTrigger.RisingEdge, InboundRisk.Safe, "test", _ => fired.Add("good"));

        var receiver = new AvatarCommandReceiver(new[] { bad, good }, () => true, action => action());
        receiver.OnObservation(Param("MCB/Ctrl/Warmup", SignalValue.Bool(false)));
        Thread.Sleep(1100);

        receiver.OnObservation(Param("MCB/Ctrl/Bad", SignalValue.Bool(true)));
        receiver.OnObservation(Param("MCB/Ctrl/Good", SignalValue.Bool(true)));

        Assert.Single(fired);
        Assert.True(receiver.Stats.Faulted >= 1);
    }

    [Fact]
    public void A_non_finite_float_is_refused()
    {
        var h = WithLevel("MCB/Ctrl/Level");
        Settle(h.Receiver);

        h.Receiver.OnObservation(Param("MCB/Ctrl/Level", SignalValue.Float(double.NaN)));

        Assert.Empty(h.Fired);
    }

    [Fact]
    public void An_int_parameter_drives_an_impulse_the_same_way_a_bool_does()
    {
        var h = WithImpulse("MCB/Ctrl/Tts/Stop");
        Settle(h.Receiver);

        h.Receiver.OnObservation(Param("MCB/Ctrl/Tts/Stop", SignalValue.Int(1)));

        Assert.Single(h.Fired);
    }

    [Fact]
    public async Task Dispatch_does_not_run_on_the_receive_thread_when_a_marshaller_is_given()
    {
        // The real app hands this the WPF dispatcher. The receive path must hand off rather than run
        // a UI-touching action on the socket thread.
        int receiveThread = Environment.CurrentManagedThreadId;
        int dispatchThread = 0;
        var done = new TaskCompletionSource();

        var command = new InboundCommand(
            "MCB/Ctrl/Tts/Stop", InboundTrigger.RisingEdge, InboundRisk.Safe, "test",
            _ => { dispatchThread = Environment.CurrentManagedThreadId; done.TrySetResult(); });

        var receiver = new AvatarCommandReceiver(
            new[] { command }, () => true, action => Task.Run(action));

        receiver.OnObservation(Param("MCB/Ctrl/Warmup", SignalValue.Bool(false)));
        Thread.Sleep(1100);
        receiver.OnObservation(Param("MCB/Ctrl/Tts/Stop", SignalValue.Bool(true)));

        await done.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.NotEqual(receiveThread, dispatchThread);
    }
}
