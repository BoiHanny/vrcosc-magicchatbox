using MagicChatbox.Vocabulary;
using MagicChatbox.Vrc;
using System.Linq;
using vrcosc_magicchatbox.Core.Vrc;
using Xunit;

namespace MagicChatbox.Tests.Core.Vrc;

// "Recently changed" exists so somebody can pull a slider in VRChat's menu and find the control here
// without knowing what it is called. It was showing the app's own internal key - avatar.param.
// prefixed and lower-cased - which is a name the user cannot look up, on a card whose own copy says
// they will not need to know one. Worse, it did not filter to parameters, so avatar.id and the three
// eye-height keys could take half the eight slots.
public class AvatarSenseRecentTests
{
    private static void Observe(AvatarSenseStore store, string key, double value)
    {
        var observation = new VrcObservation(
            SignalKey.Intern(key),
            SignalValue.Float((float)value),
            1);

        store.OnObservation(observation);
    }

    [Fact]
    public void Only_avatar_parameters_take_up_the_slots()
    {
        var store = new AvatarSenseStore();

        Observe(store, "avatar.param.toggles/hat", 1);
        Observe(store, "avatar.id", 1);
        Observe(store, "avatar.eyeheight", 1.3);
        Observe(store, "avatar.eyeheight_min", 0.9);

        var recent = store.MostActiveParameters(8);

        Assert.Single(recent);
        Assert.Equal("toggles/hat", recent[0].Key);
    }

    [Fact]
    public void The_app_s_own_prefix_never_reaches_the_screen()
    {
        var store = new AvatarSenseStore();

        Observe(store, "avatar.param.gestureleft", 3);

        Assert.All(
            store.MostActiveParameters(8),
            s => Assert.DoesNotContain(AvatarSenseStore.ParameterKeyPrefix, s.Key, System.StringComparison.Ordinal));
    }

    [Fact]
    public void The_busiest_parameters_come_first()
    {
        var store = new AvatarSenseStore();

        Observe(store, "avatar.param.quiet", 1);

        for (int i = 0; i < 5; i++)
            Observe(store, "avatar.param.busy", i);

        var recent = store.MostActiveParameters(8);

        Assert.Equal("busy", recent.First().Key);
    }

    [Fact]
    public void Asking_for_nothing_returns_nothing()
    {
        var store = new AvatarSenseStore();
        Observe(store, "avatar.param.toggles/hat", 1);

        Assert.Empty(store.MostActiveParameters(0));
    }
}
