using MagicChatbox.Tests.TestDoubles;
using MagicChatbox.Vrc;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Core.Vrc;
using Xunit;

namespace MagicChatbox.Tests.Core.Vrc;

// The pump is the seam between this app's synchronous, dispatcher-bound modules and the ValueTask
// egress. Everything that matters about it is a threading or timing property: Publish must never
// block a module thread, the same value must not be resent, and a slow or broken wire must not lose
// the newest value.
public class AvatarParameterPumpTests
{
    private static readonly TimeSpan Fast = TimeSpan.FromMilliseconds(10);

    private static AvatarParameterPump Pump(TimeSpan? minInterval = null, int budget = 64, TimeSpan? keepAlive = null)
        => new(new AvatarParameterPumpOptions
        {
            TickInterval = Fast,
            DefaultMinInterval = minInterval ?? TimeSpan.Zero,
            KeepAlive = keepAlive ?? TimeSpan.FromHours(1),
            MaxSendsPerTick = budget,
        });

    private static async Task<bool> Eventually(Func<bool> condition, int timeoutMs = 3000)
    {
        var clock = Stopwatch.StartNew();

        while (clock.ElapsedMilliseconds < timeoutMs)
        {
            if (condition())
                return true;

            await Task.Delay(10);
        }

        return condition();
    }

    [Fact]
    public void Publishing_before_start_does_not_throw_and_does_not_block()
    {
        using var pump = Pump();

        pump.Publish("MCB/Test", true);
        pump.Publish("MCB/Other", 42);

        Assert.False(pump.IsRunning);
        Assert.Equal(2, pump.Stats.PendingKeys);
    }

    [Fact]
    public async Task A_published_value_reaches_the_wire()
    {
        var egress = new FakeVrcEgress();
        using var pump = Pump();
        pump.Start(egress);

        pump.Publish("MCB/HeartRate/Bpm", 72);

        Assert.True(await Eventually(() => egress.CountOf("MCB/HeartRate/Bpm") == 1));
        Assert.Equal(72, egress.LastValueOf("MCB/HeartRate/Bpm")!.Value.AsInt());

        await pump.StopAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task An_unchanged_value_is_not_sent_twice()
    {
        // Without this the pump would emit every parameter on every tick, which at 50 ms and forty
        // parameters is 800 datagrams a second for no new information.
        var egress = new FakeVrcEgress();
        using var pump = Pump();
        pump.Start(egress);

        pump.Publish("MCB/Afk", false);
        Assert.True(await Eventually(() => egress.CountOf("MCB/Afk") == 1));

        for (int i = 0; i < 20; i++)
            pump.Publish("MCB/Afk", false);

        await Task.Delay(200);

        Assert.Equal(1, egress.CountOf("MCB/Afk"));
        Assert.True(pump.Stats.Suppressed > 0);

        await pump.StopAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task A_changed_value_is_sent_again()
    {
        var egress = new FakeVrcEgress();
        using var pump = Pump();
        pump.Start(egress);

        pump.Publish("MCB/Afk", false);
        Assert.True(await Eventually(() => egress.CountOf("MCB/Afk") == 1));

        pump.Publish("MCB/Afk", true);
        Assert.True(await Eventually(() => egress.CountOf("MCB/Afk") == 2));

        Assert.True(egress.LastValueOf("MCB/Afk")!.Value.AsBool());

        await pump.StopAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Rapid_publishes_coalesce_to_the_newest_value()
    {
        // A websocket thread can publish far faster than the wire allows. The pump must drop the
        // intermediate values, never the newest one.
        var egress = new FakeVrcEgress();
        using var pump = Pump(minInterval: TimeSpan.FromMilliseconds(500));
        pump.Start(egress);

        for (int i = 1; i <= 50; i++)
            pump.Publish("MCB/HeartRate/Bpm", i);

        Assert.True(await Eventually(() => egress.CountOf("MCB/HeartRate/Bpm") >= 1));
        await Task.Delay(150);

        Assert.True(egress.CountOf("MCB/HeartRate/Bpm") <= 2);

        await pump.StopAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task The_minimum_interval_is_respected()
    {
        var egress = new FakeVrcEgress();
        using var pump = Pump(minInterval: TimeSpan.FromMilliseconds(400));
        pump.Start(egress);

        pump.Publish("MCB/Fast", 1);
        Assert.True(await Eventually(() => egress.CountOf("MCB/Fast") == 1));

        pump.Publish("MCB/Fast", 2);
        await Task.Delay(120);

        Assert.Equal(1, egress.CountOf("MCB/Fast"));

        Assert.True(await Eventually(() => egress.CountOf("MCB/Fast") == 2));

        await pump.StopAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task A_keep_alive_resends_an_unchanged_value()
    {
        // VRChat resets avatar parameters to their defaults on every avatar load, so a value that
        // never changes would be silently wrong for the rest of the session without this.
        var egress = new FakeVrcEgress();
        using var pump = Pump(keepAlive: TimeSpan.FromMilliseconds(120));
        pump.Start(egress);

        pump.Publish("MCB/Online", true);

        Assert.True(await Eventually(() => egress.CountOf("MCB/Online") >= 2));
        Assert.True(pump.Stats.KeepAlives > 0);

        await pump.StopAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task A_failed_send_is_retried_rather_than_lost()
    {
        var egress = new FakeVrcEgress { Dispatches = false };
        using var pump = Pump();
        pump.Start(egress);

        pump.Publish("MCB/Online", true);

        Assert.True(await Eventually(() => pump.Stats.Failed >= 2));

        egress.Dispatches = true;

        Assert.True(await Eventually(() => pump.Stats.Sent >= 1));

        await pump.StopAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task A_throwing_wire_does_not_kill_the_pump()
    {
        var egress = new FakeVrcEgress { ThrowOnWrite = new InvalidOperationException("socket gone") };
        using var pump = Pump();
        pump.Start(egress);

        pump.Publish("MCB/Online", true);
        Assert.True(await Eventually(() => pump.Stats.Failed >= 1));

        egress.ThrowOnWrite = null;
        Assert.True(await Eventually(() => egress.CountOf("MCB/Online") >= 1));
        Assert.True(pump.IsRunning);

        await pump.StopAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task The_per_tick_budget_bounds_a_burst()
    {
        // Forty parameters times three mirror targets is a hundred and twenty datagrams in one tick
        // if nothing bounds it.
        var egress = new FakeVrcEgress();
        using var pump = Pump(budget: 2);
        pump.Start(egress);

        for (int i = 0; i < 20; i++)
            pump.Publish($"MCB/Bulk/{i}", i);

        await Task.Delay(25);

        Assert.True(egress.Writes.Count <= 8, $"sent {egress.Writes.Count} in the first tick window");

        Assert.True(await Eventually(() => egress.Writes.Count == 20, 5000));

        await pump.StopAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Every_parameter_gets_a_turn_even_when_the_budget_is_tight()
    {
        // A fixed scan order plus a budget starves whatever sorts last, so the cursor has to move.
        var egress = new FakeVrcEgress();
        using var pump = Pump(budget: 1);
        pump.Start(egress);

        for (int i = 0; i < 6; i++)
            pump.Publish($"MCB/Round/{i}", i);

        Assert.True(await Eventually(() => egress.Writes.Count == 6, 5000));

        await pump.StopAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Values_that_cannot_be_sent_are_refused_at_publish()
    {
        // NaN and infinity must never reach the wire.
        var egress = new FakeVrcEgress();
        using var pump = Pump();
        pump.Start(egress);

        pump.Publish("MCB/Bad", float.NaN);
        pump.Publish("MCB/Bad", float.PositiveInfinity);

        await Task.Delay(120);

        Assert.Empty(egress.Writes);

        await pump.StopAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Each_kind_arrives_as_that_kind()
    {
        var egress = new FakeVrcEgress();
        using var pump = Pump();
        pump.Start(egress);

        pump.Publish("MCB/B", true);
        pump.Publish("MCB/I", 7);
        pump.Publish("MCB/F", 0.25f);

        Assert.True(await Eventually(() => egress.Writes.Count == 3));

        Assert.Equal(VrcParameterKind.Bool, egress.LastValueOf("MCB/B")!.Value.Kind);
        Assert.Equal(VrcParameterKind.Int, egress.LastValueOf("MCB/I")!.Value.Kind);
        Assert.Equal(VrcParameterKind.Float, egress.LastValueOf("MCB/F")!.Value.Kind);
        Assert.Equal(0.25f, egress.LastValueOf("MCB/F")!.Value.AsFloat());

        await pump.StopAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Reset_makes_the_next_drain_re_assert_everything()
    {
        // This is what runs on /avatar/change: the avatar came back at its defaults, so every value
        // the app owns has to be stated again even though none of them changed.
        var egress = new FakeVrcEgress();
        using var pump = Pump();
        pump.Start(egress);

        pump.Publish("MCB/Online", true);
        Assert.True(await Eventually(() => egress.CountOf("MCB/Online") == 1));

        pump.Reset();
        pump.Publish("MCB/Online", true);

        Assert.True(await Eventually(() => egress.CountOf("MCB/Online") == 2));

        await pump.StopAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Reset_re_asserts_an_idle_value_that_nobody_publishes_again()
    {
        // The case that actually happens. Most values are published once and then sit there: the
        // module has nothing new to say, so change detection suppresses everything. If Reset only
        // clears the sent-marker, the avatar keeps VRChat's default until the keep-alive fires up to
        // eleven seconds later - or forever, if the value never changes again.
        var egress = new FakeVrcEgress();
        using var pump = Pump();
        pump.Start(egress);

        pump.Publish("MCB/Online", true);
        Assert.True(await Eventually(() => egress.CountOf("MCB/Online") == 1));

        // Steady state: republishing the same value is correctly suppressed.
        pump.Publish("MCB/Online", true);
        await Task.Delay(120);
        Assert.Equal(1, egress.CountOf("MCB/Online"));

        // The avatar changed. Nobody publishes anything; the value must still be restated.
        pump.Reset();

        Assert.True(
            await Eventually(() => egress.CountOf("MCB/Online") == 2),
            "Reset did not re-assert an idle value, so the avatar keeps its default");

        Assert.True(egress.LastValueOf("MCB/Online")!.Value.AsBool());

        await pump.StopAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Reset_does_not_invent_a_value_for_a_parameter_never_sent()
    {
        var egress = new FakeVrcEgress();
        using var pump = Pump();
        pump.Start(egress);

        pump.SetMinInterval("MCB/Never", TimeSpan.Zero);
        pump.Reset();

        await Task.Delay(150);

        Assert.Empty(egress.Writes);

        await pump.StopAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Stopping_is_prompt_and_idempotent()
    {
        var egress = new FakeVrcEgress();
        using var pump = Pump();
        pump.Start(egress);

        pump.Publish("MCB/Online", true);
        Assert.True(await Eventually(() => egress.Writes.Count == 1));

        var clock = Stopwatch.StartNew();
        await pump.StopAsync(TimeSpan.FromSeconds(2));
        clock.Stop();

        Assert.False(pump.IsRunning);
        Assert.True(clock.ElapsedMilliseconds < 1500, $"stop took {clock.ElapsedMilliseconds} ms");

        await pump.StopAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Nothing_is_sent_after_stop()
    {
        var egress = new FakeVrcEgress();
        using var pump = Pump();
        pump.Start(egress);

        pump.Publish("MCB/Online", true);
        Assert.True(await Eventually(() => egress.Writes.Count == 1));

        await pump.StopAsync(TimeSpan.FromSeconds(2));
        egress.ClearWrites();

        pump.Publish("MCB/Online", false);
        await Task.Delay(150);

        Assert.Empty(egress.Writes);
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        var pump = Pump();
        pump.Start(new FakeVrcEgress());

        pump.Dispose();
        pump.Dispose();

        Assert.False(pump.IsRunning);
    }

    [Fact]
    public async Task Publish_never_blocks_on_a_slow_wire()
    {
        // The whole reason the pump exists: a module publishing from the UI thread must not wait on
        // a socket. A 200 ms wire and a hundred publishes must still return immediately.
        var egress = new FakeVrcEgress { WriteDelay = TimeSpan.FromMilliseconds(200) };
        using var pump = Pump();
        pump.Start(egress);

        var clock = Stopwatch.StartNew();

        for (int i = 0; i < 100; i++)
            pump.Publish("MCB/HeartRate/Bpm", i);

        clock.Stop();

        Assert.True(clock.ElapsedMilliseconds < 100, $"Publish blocked for {clock.ElapsedMilliseconds} ms");

        await pump.StopAsync(TimeSpan.FromSeconds(3));
    }
}
