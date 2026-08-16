using MagicChatbox.Vocabulary;
using MagicChatbox.Vrc;
using System;
using System.Collections.Generic;
using vrcosc_magicchatbox.Core.Vrc;
using Xunit;

namespace MagicChatbox.Tests.Core.Vrc;

// VRChat only announces the avatar when one is put on, so an app that starts mid-session never sees
// /avatar/change and the epoch stays blank. Measured against a live client: 139,263 parameters
// arrived with 0 avatar changes, while the OSCQuery harvest carried the id all along. This is the
// fallback that turns "unknown avatar" into a name.
public class AvatarIdentityResolverTests
{
    private static AvatarSchemaSnapshot Snapshot(string avatarId)
        => new(avatarId, 1, DateTime.UtcNow, Array.Empty<VrcParameterDeclaration>());

    [Fact]
    public void With_nothing_known_the_identity_is_unknown_rather_than_wrong()
    {
        var resolver = new AvatarIdentityResolver(
            () => string.Empty,
            () => AvatarSchemaSnapshot.Empty);

        AvatarIdentity identity = resolver.Resolve();

        Assert.False(identity.IsKnown);
        Assert.Equal(AvatarIdSource.None, identity.Source);
        Assert.Equal("Unknown avatar", identity.DisplayName);
    }

    [Fact]
    public void The_avatar_change_event_wins_when_it_has_fired()
    {
        var resolver = new AvatarIdentityResolver(
            () => "avtr_from_change",
            () => Snapshot("avtr_from_harvest"));

        AvatarIdentity identity = resolver.Resolve();

        Assert.Equal("avtr_from_change", identity.Id);
        Assert.Equal(AvatarIdSource.AvatarChange, identity.Source);
    }

    [Fact]
    public void The_harvest_recovers_the_id_on_a_mid_session_join()
    {
        // The case that actually happens when the app starts after VRChat.
        var resolver = new AvatarIdentityResolver(
            () => string.Empty,
            () => Snapshot("avtr_from_harvest"));

        AvatarIdentity identity = resolver.Resolve();

        Assert.True(identity.IsKnown);
        Assert.Equal("avtr_from_harvest", identity.Id);
        Assert.Equal(AvatarIdSource.SchemaHarvest, identity.Source);
    }

    [Fact]
    public void A_build_and_test_avatar_is_not_treated_as_an_identity()
    {
        // SDK Build & Test produces local: ids. Accepting them gives a creator a junk profile per
        // upload, and VRChat writes no config for them so they can never gain a name.
        Assert.False(AvatarIdentityResolver.IsUsable("local:12345"));
        Assert.False(AvatarIdentityResolver.IsUsable("LOCAL:12345"));
        Assert.True(AvatarIdentityResolver.IsUsable("avtr_a70460da-3b92-4505-be32-18dae45cb192"));
    }

    [Fact]
    public void A_local_id_from_the_change_event_falls_through_to_the_harvest()
    {
        var resolver = new AvatarIdentityResolver(
            () => "local:99",
            () => Snapshot("avtr_real"));

        Assert.Equal("avtr_real", resolver.Resolve().Id);
    }

    [Fact]
    public void Blank_and_whitespace_ids_are_refused()
    {
        Assert.False(AvatarIdentityResolver.IsUsable(null));
        Assert.False(AvatarIdentityResolver.IsUsable(string.Empty));
        Assert.False(AvatarIdentityResolver.IsUsable("   "));
    }

    [Fact]
    public void A_throwing_source_degrades_instead_of_faulting_the_page()
    {
        var resolver = new AvatarIdentityResolver(
            () => throw new InvalidOperationException("bridge stopped"),
            () => Snapshot("avtr_real"));

        Assert.Equal("avtr_real", resolver.Resolve().Id);
    }

    [Fact]
    public void Both_sources_throwing_yields_unknown_rather_than_an_exception()
    {
        var resolver = new AvatarIdentityResolver(
            () => throw new InvalidOperationException("no epoch"),
            () => throw new InvalidOperationException("no schema"));

        Assert.False(resolver.Resolve().IsKnown);
    }

    [Fact]
    public void The_id_is_shown_when_no_name_can_be_read()
    {
        // There is no OSC address and no OSCQuery node carrying the avatar's name, so a machine
        // without the config file has an id and nothing else. Showing the id beats showing nothing.
        var resolver = new AvatarIdentityResolver(
            () => "avtr_nameless",
            () => AvatarSchemaSnapshot.Empty,
            new AvatarConfigReader(root: System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mcb-no-such-osc-root")));

        AvatarIdentity identity = resolver.Resolve();

        Assert.Equal("avtr_nameless", identity.DisplayName);
    }
}
