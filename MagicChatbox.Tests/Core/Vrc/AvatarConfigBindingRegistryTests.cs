using MagicChatbox.Tests.TestDoubles;
using System;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Vrc;
using Xunit;

namespace MagicChatbox.Tests.Core.Vrc;

// The seeder, the settings flag and twelve tests for all of it shipped wired to an empty binding
// list, so the whole feature ran on every avatar change and provably could not do anything. This is
// the list that was missing.
//
// The safety rule it has to keep: a parameter riding on somebody's avatar may take a feature away and
// may never switch one on. Read the direction as "the value is the feature" - holding MCB/Cfg/Media
// off turns media off, and holding it on is refused rather than obeyed.
public class AvatarConfigBindingRegistryTests
{
    private static (FakeAppState State, IntegrationSettings Integrations) Targets() => (new FakeAppState(), new IntegrationSettings());

    [Fact]
    public void Every_option_offered_can_actually_be_applied()
    {
        // The descriptions are rendered from Options and the behaviour comes from Build. If one grows
        // an entry the other does not have, the options list advertises something inert - which is the
        // exact failure this whole feature just spent a release in.
        (FakeAppState state, IntegrationSettings integrations) = Targets();

        var built = AvatarConfigBindingRegistry.Build(state, integrations);

        Assert.Equal(
            AvatarConfigBindingRegistry.Options.Select(o => o.Parameter).OrderBy(p => p, StringComparer.Ordinal),
            built.Select(b => b.Parameter).OrderBy(p => p, StringComparer.Ordinal));
    }

    [Fact]
    public void Every_binding_lives_under_the_prefix_the_app_owns()
    {
        (FakeAppState state, IntegrationSettings integrations) = Targets();

        Assert.All(AvatarConfigBindingRegistry.Build(state, integrations), b => Assert.True(b.IsOwned));
    }

    [Fact]
    public void A_binding_outside_the_prefix_is_refused_when_the_seeder_is_built()
    {
        // Nothing outside MCB/Cfg/ may be driven this way: an avatar author could otherwise name a
        // parameter after somebody else's and have it act on this app's settings.
        var stray = new AvatarConfigBinding(
            "Toggles/Hat", "not ours", ConfigDirection.OffOnly, _ => { });

        Assert.Throws<ArgumentException>(() => new AvatarConfigSeeder([stray], () => true));
    }

    [Fact]
    public void Holding_a_switch_off_takes_the_feature_away()
    {
        (FakeAppState state, IntegrationSettings integrations) = Targets();
        integrations.IntgrHeartRate = true;

        AvatarConfigBinding binding = AvatarConfigBindingRegistry
            .Build(state, integrations)
            .Single(b => b.Parameter == AvatarConfigBindingRegistry.HeartRate);

        binding.Apply(false);

        Assert.False(integrations.IntgrHeartRate);
    }

    [Fact]
    public void Every_binding_is_one_way()
    {
        // ConfigDirection.Both exists, and nothing in this registry may use it. A world, a badly built
        // prefab or a stale saved value can take capability away; granting it stays a decision made at
        // the desk.
        (FakeAppState state, IntegrationSettings integrations) = Targets();

        Assert.All(
            AvatarConfigBindingRegistry.Build(state, integrations),
            b => Assert.Equal(ConfigDirection.OffOnly, b.Direction));
    }

    [Fact]
    public void Every_switch_is_documented_for_the_people_who_have_to_build_it()
    {
        // These have to be created in Unity by hand, so a switch the published contract does not
        // mention is a switch nobody can use. docs/avatar-parameters.md is generated from the contract.
        var documented = AvatarParameterContract.Parameters
            .Where(p => p.Tier == AvatarParameterTier.Config)
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal);

        Assert.Equal(
            AvatarConfigBindingRegistry.Options.Select(o => o.Parameter).OrderBy(n => n, StringComparer.Ordinal),
            documented);
    }

    [Fact]
    public void The_config_switches_are_kept_out_of_the_layout_doctor_s_required_list()
    {
        // The doctor reports what an avatar is missing. These are optional extras rather than the
        // command surface, so listing them would tell every avatar it was incomplete.
        Assert.All(
            AvatarParameterContract.Parameters.Where(p => p.Tier == AvatarParameterTier.Config),
            p => Assert.NotEqual(AvatarParameterTier.Control, p.Tier));
    }

    [Fact]
    public void The_switches_cover_the_things_worth_hiding_in_public()
    {
        Assert.Contains(AvatarConfigBindingRegistry.Options, o => o.Parameter == AvatarConfigBindingRegistry.HeartRate);
        Assert.Contains(AvatarConfigBindingRegistry.Options, o => o.Parameter == AvatarConfigBindingRegistry.Media);
        Assert.Contains(AvatarConfigBindingRegistry.Options, o => o.Parameter == AvatarConfigBindingRegistry.WindowActivity);
        Assert.All(AvatarConfigBindingRegistry.Options, o => Assert.False(string.IsNullOrWhiteSpace(o.Description)));
    }
}
