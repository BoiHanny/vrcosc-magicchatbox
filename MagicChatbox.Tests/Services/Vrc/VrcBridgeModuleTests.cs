using System.Threading.Tasks;
using MagicChatbox.Tests.TestDoubles;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Services.Vrc;
using Xunit;

namespace MagicChatbox.Tests.Services.Vrc;

// The bridge owns a UDP socket, an HTTP listener and an mDNS advertisement, so its lifecycle is the
// part that can hang shutdown or leak a port. These tests cover the contract the module host relies
// on: default-off, cheap construction, idempotent stop and dispose.
public class VrcBridgeModuleTests
{
    private static VrcBridgeModule Bridge(VrcBridgeSettings? settings = null)
        => new(
            new StubSettingsProvider<VrcBridgeSettings>(settings ?? new VrcBridgeSettings()),
            () => "Test World",
            () => false);

    [Fact]
    public void The_bridge_is_off_until_the_user_turns_it_on()
    {
        // It binds a socket and advertises on the network, so it must never start by itself on an
        // upgrade for somebody who never asked for it.
        Assert.False(new VrcBridgeSettings().EnableBridge);
        Assert.False(new VrcBridgeSettings().EnableParameterInput);
    }

    [Fact]
    public void The_receive_port_defaults_to_letting_the_OS_choose()
    {
        // Hard-coding 9001 is how an OSC app becomes silently deaf the moment a second one is running.
        Assert.Equal(0, new VrcBridgeSettings().OscReceivePort);
    }

    [Fact]
    public void Construction_touches_nothing()
    {
        // A module whose constructor overruns the bootstrapper budget is dropped and stays null for
        // the whole session, so construction must not bind, listen or advertise.
        var module = Bridge();

        Assert.False(module.IsRunning);
        Assert.Null(module.Egress);
        Assert.Equal("VrcBridge", module.Name);
    }

    [Fact]
    public async Task Starting_while_turned_off_does_nothing_and_says_so()
    {
        var module = Bridge();

        await module.StartAsync();

        Assert.False(module.IsRunning);
        Assert.Null(module.Egress);
        Assert.Equal("Turned off", module.StatusMessage);
    }

    [Fact]
    public async Task Stopping_a_module_that_never_started_is_harmless()
    {
        var module = Bridge();

        await module.StopAsync();
        await module.StopAsync();

        Assert.False(module.IsRunning);
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        // DI-registered modules are disposed twice: once by the module host and again by the app.
        var module = Bridge();

        module.Dispose();
        module.Dispose();

        Assert.False(module.IsRunning);
    }

    [Fact]
    public async Task Dispose_after_stop_is_harmless()
    {
        var module = Bridge();

        await module.StopAsync();
        module.Dispose();

        Assert.False(module.IsRunning);
    }

    [Fact]
    public async Task A_disposed_module_refuses_to_start()
    {
        var settings = new VrcBridgeSettings { EnableBridge = true };
        var module = Bridge(settings);

        module.Dispose();
        await module.StartAsync();

        Assert.False(module.IsRunning);
    }
}
