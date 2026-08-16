using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Vrc;
using Xunit;

namespace MagicChatbox.Tests.Core.Vrc;

// These two policies are consulted before every chatbox send, so "fails open" is the required
// behaviour: a discovery failure must never silently mute the product, and an empty block list must
// never block anything.
public class AppSafetyPoliciesTests
{
    [Fact]
    public void A_fresh_world_policy_mutes_nothing()
    {
        var policy = new AppWorldPolicy(new VrcBridgeSettings(), () => "Some World", () => false);

        Assert.False(policy.IsCurrentWorldMuted);
    }

    [Fact]
    public void A_world_on_the_list_is_muted()
    {
        var settings = new VrcBridgeSettings();
        settings.MutedWorlds.Add("Quiet Place");

        var policy = new AppWorldPolicy(settings, () => "The Quiet Place Lobby", () => false);

        Assert.True(policy.IsCurrentWorldMuted);
    }

    [Fact]
    public void World_matching_ignores_case()
    {
        var settings = new VrcBridgeSettings();
        settings.MutedWorlds.Add("quiet place");

        var policy = new AppWorldPolicy(settings, () => "The QUIET PLACE Lobby", () => false);

        Assert.True(policy.IsCurrentWorldMuted);
    }

    [Fact]
    public void An_unknown_world_is_not_muted()
    {
        // "We could not work out where you are" must not become "stop sending".
        var settings = new VrcBridgeSettings();
        settings.MutedWorlds.Add("Quiet Place");

        var policy = new AppWorldPolicy(settings, () => null, () => false);

        Assert.False(policy.IsCurrentWorldMuted);
    }

    [Fact]
    public void A_throwing_world_source_does_not_mute()
    {
        var policy = new AppWorldPolicy(
            new VrcBridgeSettings(),
            () => throw new System.InvalidOperationException("radar not ready"),
            () => false);

        Assert.False(policy.IsCurrentWorldMuted);
    }

    [Fact]
    public void Public_instances_are_muted_only_when_the_user_asked_for_that()
    {
        var settings = new VrcBridgeSettings();
        var policy = new AppWorldPolicy(settings, () => "Somewhere", () => true);

        Assert.False(policy.IsCurrentWorldMuted);

        settings.MuteInPublicInstances = true;

        Assert.True(policy.IsCurrentWorldMuted);
    }

    [Fact]
    public void A_fresh_profanity_policy_blocks_nothing()
    {
        var policy = new AppProfanityPolicy(new VrcBridgeSettings());

        Assert.False(policy.Blocks("anything at all", out string? term));
        Assert.Null(term);
    }

    [Fact]
    public void A_blocked_term_is_reported_back_so_the_user_can_see_why()
    {
        var settings = new VrcBridgeSettings();
        settings.BlockedTerms.Add("hunter2");

        var policy = new AppProfanityPolicy(settings);

        Assert.True(policy.Blocks("my password is Hunter2", out string? term));
        Assert.Equal("hunter2", term);
    }

    [Fact]
    public void Blank_entries_in_the_block_list_are_ignored()
    {
        // An empty string is contained in every string, so a stray blank row would block everything.
        var settings = new VrcBridgeSettings();
        settings.BlockedTerms.Add("   ");
        settings.BlockedTerms.Add("");

        var policy = new AppProfanityPolicy(settings);

        Assert.False(policy.Blocks("perfectly ordinary text", out _));
    }

    [Fact]
    public void Empty_text_is_never_blocked()
    {
        var settings = new VrcBridgeSettings();
        settings.BlockedTerms.Add("nope");

        var policy = new AppProfanityPolicy(settings);

        Assert.False(policy.Blocks(string.Empty, out _));
    }
}
