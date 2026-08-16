using MagicChatbox.Tests.TestDoubles;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Core.Vrc;
using Xunit;

namespace MagicChatbox.Tests.Core.Vrc;

// The router is the one place that decides where an avatar value goes. While the bridge is still
// opt-in it deliberately does BOTH: the long-standing CoreOSC path keeps working exactly as it
// always has, and the bridge gets a copy so it can be exercised without anybody losing output. That
// duplication is transitional and intentional - when the bridge becomes the only path, this is the
// single file that changes.
public class AvatarParameterRouterTests
{
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
    public void With_no_bridge_running_everything_still_goes_out_the_old_path()
    {
        var sender = new FakeOscSender();
        var router = new AvatarParameterRouter(sender, () => null);

        router.Set("HR", 72);

        Assert.Equal(72, sender.LastValueFor("/avatar/parameters/HR"));
    }

    [Fact]
    public void A_pump_that_is_not_running_is_ignored()
    {
        var sender = new FakeOscSender();
        using var pump = new AvatarParameterPump();
        var router = new AvatarParameterRouter(sender, () => pump);

        router.Set("HR", 72);

        Assert.Equal(72, sender.LastValueFor("/avatar/parameters/HR"));
        Assert.Equal(0, pump.Stats.Published);
    }

    [Fact]
    public async Task A_running_bridge_also_receives_the_value()
    {
        var sender = new FakeOscSender();
        var egress = new FakeVrcEgress();
        using var pump = new AvatarParameterPump(new AvatarParameterPumpOptions
        {
            TickInterval = TimeSpan.FromMilliseconds(10),
            DefaultMinInterval = TimeSpan.Zero,
        });
        pump.Start(egress);

        var router = new AvatarParameterRouter(sender, () => pump);

        router.Set("HR", 72);

        Assert.Equal(72, sender.LastValueFor("/avatar/parameters/HR"));
        Assert.True(await Eventually(() => egress.CountOf("HR") == 1));

        await pump.StopAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task The_bridge_receives_a_bare_name_not_an_address()
    {
        // IVrcEgress takes a parameter name and builds the address itself. Handing it a full address
        // would produce /avatar/parameters//avatar/parameters/HR on the wire.
        var egress = new FakeVrcEgress();
        using var pump = new AvatarParameterPump(new AvatarParameterPumpOptions
        {
            TickInterval = TimeSpan.FromMilliseconds(10),
            DefaultMinInterval = TimeSpan.Zero,
        });
        pump.Start(egress);

        var router = new AvatarParameterRouter(new FakeOscSender(), () => pump);

        router.Set("/avatar/parameters/CameraFlash", true);

        Assert.True(await Eventually(() => egress.CountOf("CameraFlash") == 1));

        await pump.StopAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task A_newer_pulse_cancels_the_older_one_s_trailing_reset()
    {
        // Two screenshots in quick succession must not leave the parameter stuck on, and must not
        // switch it off underneath the newer pulse either.
        var sender = new FakeOscSender();
        var router = new AvatarParameterRouter(sender, () => null);

        router.Pulse("CameraFlash", 200);
        await Task.Delay(30);
        router.Pulse("CameraFlash", 200);

        await Task.Delay(500);

        var values = sender.ValuesFor("/avatar/parameters/CameraFlash");

        Assert.Equal(3, values.Count);
        Assert.Equal(true, values[0]);
        Assert.Equal(true, values[1]);
        Assert.Equal(false, values[2]);
    }

    [Fact]
    public void An_empty_name_is_dropped_rather_than_sent_as_a_bare_prefix()
    {
        var sender = new FakeOscSender();
        var router = new AvatarParameterRouter(sender, () => null);

        router.Set(string.Empty, true);
        router.Set("   ", 1);
        router.Pulse(string.Empty);

        Assert.Empty(sender.Parameters);
    }

    [Fact]
    public void A_throwing_pump_accessor_never_breaks_the_old_path()
    {
        var sender = new FakeOscSender();
        var router = new AvatarParameterRouter(
            sender,
            () => throw new InvalidOperationException("module host not ready"));

        router.Set("HR", 72);

        Assert.Equal(72, sender.LastValueFor("/avatar/parameters/HR"));
    }
}
