using MagicChatbox.Tests.TestDoubles;
using System;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Vrc;
using Xunit;

namespace MagicChatbox.Tests.Core.Vrc;

// Every module-driven parameter was going out twice: once synchronously down the old sender and once
// through the pump, because MirrorToLegacyOsc defaulted to on. It is a switch-over safety net, and
// leaving it on is double the datagrams to VRChat for no benefit once the bridge is reaching the
// avatar - which is a fair part of "the OSC is very slow".
//
// The switch stays, because somebody whose avatar is not reacting needs a way back. It just does not
// default to sending everything twice any more.
public class AvatarParameterRouterMirrorTests
{
    [Fact]
    public void The_old_sender_is_not_mirrored_by_default()
    {
        Assert.False(new VrcBridgeSettings().MirrorToLegacyOsc);
    }

    [Fact]
    public void With_the_bridge_running_and_mirroring_off_a_value_goes_out_once()
    {
        var legacy = new FakeOscSender();
        var egress = new FakeVrcEgress();
        using var pump = new AvatarParameterPump();
        pump.Start(egress);

        var router = new AvatarParameterRouter(legacy, () => pump, () => false);

        router.Set("Toggles/Hat", true);

        Assert.Empty(legacy.Parameters);
    }

    [Fact]
    public void With_mirroring_on_it_still_goes_both_ways()
    {
        var legacy = new FakeOscSender();
        var egress = new FakeVrcEgress();
        using var pump = new AvatarParameterPump();
        pump.Start(egress);

        var router = new AvatarParameterRouter(legacy, () => pump, () => true);

        router.Set("Toggles/Hat", true);

        Assert.Single(legacy.Parameters);
    }

    [Fact]
    public void Without_a_running_pump_the_old_sender_is_the_only_way_out()
    {
        // The safety net that matters: mirroring off must never mean nothing is sent when the bridge
        // is not running.
        var legacy = new FakeOscSender();
        var router = new AvatarParameterRouter(legacy, () => null, () => false);

        router.Set("Toggles/Hat", true);

        Assert.Single(legacy.Parameters);
    }
}
