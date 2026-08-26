using vrcosc_magicchatbox.Core.Updates;
using Xunit;

namespace MagicChatbox.Tests.Core.Updates;

public class UpdateDecisionTests
{
    private static readonly UpdateOffer NoStable = UpdateOffer.Absent(UpdateChannel.Stable);
    private static readonly UpdateOffer NoPreRelease = UpdateOffer.Absent(UpdateChannel.PreRelease);

    private static UpdateOffer StableOffer(string version) => OfferOf(UpdateChannel.Stable, version);

    private static UpdateOffer PreReleaseOffer(string version) => OfferOf(UpdateChannel.PreRelease, version);

    private static UpdateOffer OfferOf(UpdateChannel channel, string version)
        => new(channel, version, $"https://example.test/{version}/MagicChatbox.zip", "sha256:" + version);

    [Fact]
    public void An_armed_stable_channel_installs_a_newer_stable_build()
    {
        UpdateVerdict verdict = UpdateDecision.Decide(
            "0.9.220",
            StableOffer("0.9.224"),
            NoPreRelease,
            stableMode: UpdateChannelMode.Auto,
            preReleaseMode: UpdateChannelMode.Off);

        Assert.Equal(UpdateStanding.UpdateAvailable, verdict.Standing);
        Assert.Equal(UpdateAction.AutoInstall, verdict.Action);
        Assert.Equal(UpdateChannel.Stable, verdict.Channel);
        Assert.Equal("0.9.224", verdict.Version);
        Assert.Equal("https://example.test/0.9.224/MagicChatbox.zip", verdict.Url);
        Assert.Equal("sha256:0.9.224", verdict.Digest);
    }

    [Fact]
    public void A_watched_stable_channel_only_notifies()
    {
        UpdateVerdict verdict = UpdateDecision.Decide(
            "0.9.220",
            StableOffer("0.9.224"),
            NoPreRelease,
            stableMode: UpdateChannelMode.Notify,
            preReleaseMode: UpdateChannelMode.Off);

        Assert.Equal(UpdateStanding.UpdateAvailable, verdict.Standing);
        Assert.Equal(UpdateAction.Notify, verdict.Action);
        Assert.Equal(UpdateChannel.Stable, verdict.Channel);
        Assert.Equal("0.9.224", verdict.Version);
    }

    [Fact]
    public void A_disabled_stable_channel_offers_nothing()
    {
        UpdateVerdict verdict = UpdateDecision.Decide(
            "0.9.220",
            StableOffer("0.9.224"),
            NoPreRelease,
            stableMode: UpdateChannelMode.Off,
            preReleaseMode: UpdateChannelMode.Off);

        Assert.Equal(UpdateAction.None, verdict.Action);
        Assert.NotEqual(UpdateStanding.UpdateAvailable, verdict.Standing);
        Assert.Equal("0.9.220", verdict.Version);
    }

    [Fact]
    public void A_disabled_pre_release_channel_is_never_offered_even_when_it_is_newest()
    {
        UpdateVerdict verdict = UpdateDecision.Decide(
            "0.9.224",
            StableOffer("0.9.224"),
            PreReleaseOffer("0.9.230"),
            stableMode: UpdateChannelMode.Auto,
            preReleaseMode: UpdateChannelMode.Off);

        Assert.Equal(UpdateStanding.UpToDate, verdict.Standing);
        Assert.Equal(UpdateAction.None, verdict.Action);
        Assert.Equal(UpdateChannel.Stable, verdict.Channel);
        Assert.NotEqual("0.9.230", verdict.Version);
    }

    [Fact]
    public void An_armed_pre_release_channel_installs_a_newer_pre_release()
    {
        UpdateVerdict verdict = UpdateDecision.Decide(
            "0.9.224",
            StableOffer("0.9.224"),
            PreReleaseOffer("0.9.225"),
            stableMode: UpdateChannelMode.Auto,
            preReleaseMode: UpdateChannelMode.Auto);

        Assert.Equal(UpdateStanding.UpdateAvailable, verdict.Standing);
        Assert.Equal(UpdateAction.AutoInstall, verdict.Action);
        Assert.Equal(UpdateChannel.PreRelease, verdict.Channel);
        Assert.Equal("0.9.225", verdict.Version);
    }

    [Fact]
    public void A_stranded_pre_release_user_still_moves_forward_to_stable()
    {
        // The mode belongs to the channel an update comes FROM, not to the build being run:
        // someone left on an abandoned pre-release with pre-releases switched off must still
        // be carried forward by the stable channel they did arm.
        UpdateVerdict verdict = UpdateDecision.Decide(
            "0.9.225",
            StableOffer("0.9.226"),
            PreReleaseOffer("0.9.225"),
            stableMode: UpdateChannelMode.Auto,
            preReleaseMode: UpdateChannelMode.Off);

        Assert.Equal(UpdateStanding.UpdateAvailable, verdict.Standing);
        Assert.Equal(UpdateAction.AutoInstall, verdict.Action);
        Assert.Equal(UpdateChannel.Stable, verdict.Channel);
        Assert.Equal("0.9.226", verdict.Version);
    }

    [Fact]
    public void An_armed_channel_is_never_starved_by_a_merely_watched_one()
    {
        // The pre-release is the newer build, but it is only being watched. Picking it would
        // downgrade the install the user armed on stable into a notification they must click.
        UpdateVerdict verdict = UpdateDecision.Decide(
            "0.9.220",
            StableOffer("0.9.224"),
            PreReleaseOffer("0.9.225"),
            stableMode: UpdateChannelMode.Auto,
            preReleaseMode: UpdateChannelMode.Notify);

        Assert.Equal(UpdateAction.AutoInstall, verdict.Action);
        Assert.Equal(UpdateChannel.Stable, verdict.Channel);
        Assert.Equal("0.9.224", verdict.Version);
    }

    [Fact]
    public void When_both_channels_are_armed_the_highest_version_wins()
    {
        UpdateVerdict preReleaseAhead = UpdateDecision.Decide(
            "0.9.220",
            StableOffer("0.9.224"),
            PreReleaseOffer("0.9.226"),
            stableMode: UpdateChannelMode.Auto,
            preReleaseMode: UpdateChannelMode.Auto);

        Assert.Equal(UpdateAction.AutoInstall, preReleaseAhead.Action);
        Assert.Equal(UpdateChannel.PreRelease, preReleaseAhead.Channel);
        Assert.Equal("0.9.226", preReleaseAhead.Version);

        UpdateVerdict stableAhead = UpdateDecision.Decide(
            "0.9.220",
            StableOffer("0.9.227"),
            PreReleaseOffer("0.9.226"),
            stableMode: UpdateChannelMode.Auto,
            preReleaseMode: UpdateChannelMode.Auto);

        Assert.Equal(UpdateAction.AutoInstall, stableAhead.Action);
        Assert.Equal(UpdateChannel.Stable, stableAhead.Channel);
        Assert.Equal("0.9.227", stableAhead.Version);
    }

    [Fact]
    public void A_tie_between_channels_resolves_to_stable()
    {
        UpdateVerdict verdict = UpdateDecision.Decide(
            "0.9.220",
            StableOffer("0.9.224"),
            PreReleaseOffer("0.9.224"),
            stableMode: UpdateChannelMode.Auto,
            preReleaseMode: UpdateChannelMode.Auto);

        Assert.Equal(UpdateChannel.Stable, verdict.Channel);
        Assert.Equal("0.9.224", verdict.Version);
    }

    [Fact]
    public void An_offer_equal_to_the_current_version_is_not_an_update()
    {
        UpdateVerdict verdict = UpdateDecision.Decide(
            "0.9.224",
            StableOffer("0.9.224"),
            NoPreRelease,
            stableMode: UpdateChannelMode.Auto,
            preReleaseMode: UpdateChannelMode.Auto);

        Assert.Equal(UpdateStanding.UpToDate, verdict.Standing);
        Assert.Equal(UpdateAction.None, verdict.Action);
        Assert.Equal(UpdateChannel.Stable, verdict.Channel);
    }

    [Fact]
    public void An_older_offer_is_never_installed()
    {
        UpdateVerdict verdict = UpdateDecision.Decide(
            "0.9.230",
            StableOffer("0.9.224"),
            PreReleaseOffer("0.9.229"),
            stableMode: UpdateChannelMode.Auto,
            preReleaseMode: UpdateChannelMode.Auto);

        Assert.Equal(UpdateAction.None, verdict.Action);
        Assert.Equal("0.9.230", verdict.Version);
        Assert.NotEqual("0.9.224", verdict.Version);
        Assert.NotEqual("0.9.229", verdict.Version);
    }

    [Theory]
    [InlineData(UpdateChannelMode.Notify)]
    [InlineData(UpdateChannelMode.Auto)]
    public void A_blocked_version_is_never_offered_on_either_channel(UpdateChannelMode mode)
    {
        UpdateVerdict verdict = UpdateDecision.Decide(
            "0.9.220",
            StableOffer("0.9.224"),
            PreReleaseOffer("0.9.225"),
            stableMode: mode,
            preReleaseMode: mode,
            blockedVersions: ["0.9.224", "0.9.225"]);

        Assert.Equal(UpdateAction.None, verdict.Action);
        Assert.NotEqual(UpdateStanding.UpdateAvailable, verdict.Standing);
        Assert.Equal("0.9.220", verdict.Version);
    }

    [Fact]
    public void A_blocked_version_still_matches_when_it_was_recorded_with_a_tag_prefix()
    {
        UpdateVerdict verdict = UpdateDecision.Decide(
            "0.9.220",
            StableOffer("0.9.224"),
            NoPreRelease,
            stableMode: UpdateChannelMode.Auto,
            preReleaseMode: UpdateChannelMode.Off,
            blockedVersions: ["v0.9.224"]);

        Assert.Equal(UpdateAction.None, verdict.Action);
    }

    [Fact]
    public void Blocking_one_channel_still_leaves_the_other_free_to_offer()
    {
        UpdateVerdict verdict = UpdateDecision.Decide(
            "0.9.220",
            StableOffer("0.9.224"),
            PreReleaseOffer("0.9.225"),
            stableMode: UpdateChannelMode.Auto,
            preReleaseMode: UpdateChannelMode.Auto,
            blockedVersions: ["0.9.225"]);

        Assert.Equal(UpdateAction.AutoInstall, verdict.Action);
        Assert.Equal(UpdateChannel.Stable, verdict.Channel);
        Assert.Equal("0.9.224", verdict.Version);
    }

    [Fact]
    public void Running_newer_than_every_release_is_ahead_of_releases()
    {
        UpdateVerdict verdict = UpdateDecision.Decide(
            "0.9.300",
            StableOffer("0.9.224"),
            PreReleaseOffer("0.9.226"),
            stableMode: UpdateChannelMode.Notify,
            preReleaseMode: UpdateChannelMode.Notify);

        Assert.Equal(UpdateStanding.AheadOfReleases, verdict.Standing);
        Assert.Equal(UpdateAction.None, verdict.Action);
        Assert.Null(verdict.Channel);
    }

    [Theory]
    [InlineData(UpdateChannelMode.Notify)]
    [InlineData(UpdateChannelMode.Auto)]
    public void Running_the_newest_pre_release_is_up_to_date_on_that_channel(UpdateChannelMode preReleaseMode)
    {
        UpdateVerdict verdict = UpdateDecision.Decide(
            "0.9.225",
            StableOffer("0.9.224"),
            PreReleaseOffer("0.9.225"),
            stableMode: UpdateChannelMode.Auto,
            preReleaseMode: preReleaseMode);

        Assert.Equal(UpdateStanding.UpToDate, verdict.Standing);
        Assert.Equal(UpdateAction.None, verdict.Action);
        Assert.Equal(UpdateChannel.PreRelease, verdict.Channel);
    }

    [Fact]
    public void An_offer_without_a_url_or_a_version_counts_as_absent()
    {
        UpdateVerdict noUrl = UpdateDecision.Decide(
            "0.9.220",
            new UpdateOffer(UpdateChannel.Stable, "0.9.224", string.Empty, string.Empty),
            NoPreRelease,
            stableMode: UpdateChannelMode.Auto,
            preReleaseMode: UpdateChannelMode.Auto);

        Assert.Equal(UpdateAction.None, noUrl.Action);
        Assert.Equal(UpdateStanding.UpToDate, noUrl.Standing);

        UpdateVerdict noVersion = UpdateDecision.Decide(
            "0.9.220",
            NoStable,
            new UpdateOffer(UpdateChannel.PreRelease, string.Empty, "https://example.test/x.zip", string.Empty),
            stableMode: UpdateChannelMode.Auto,
            preReleaseMode: UpdateChannelMode.Auto);

        Assert.Equal(UpdateAction.None, noVersion.Action);
        Assert.Equal(UpdateStanding.UpToDate, noVersion.Standing);
    }
}
