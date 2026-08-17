using System.Linq;
using vrcosc_magicchatbox.Core.Vrc;
using Xunit;

namespace MagicChatbox.Tests.Core.Vrc;

// The readiness card answers "does what I send actually reach my avatar", and it can only answer for
// names it knows it sends. It used to carry a hand-written list of six heart-rate names against a
// contract declaring forty-four, and no row at all for the camera flash - so a user with a working
// VRCOSC-shaped prefab was told their avatar had nothing for it.
//
// The fix is that the contract is the source of truth and features are defined by which gate switches
// them on. These tests exist to keep it that way: add a parameter with a new gate and the last test
// here fails until somebody decides which feature it belongs to.
public class AvatarFeatureCatalogTests
{
    [Fact]
    public void Every_parameter_the_app_sends_belongs_to_a_feature()
    {
        // The ratchet. A new gate means a new capability nobody can see the readiness of.
        Assert.True(
            AvatarFeatureCatalog.UnclaimedGates().Count == 0,
            "these gates are sent to avatars but belong to no readiness row: "
            + string.Join(", ", AvatarFeatureCatalog.UnclaimedGates()));
    }

    [Fact]
    public void The_features_between_them_cover_every_outbound_parameter()
    {
        int claimed = AvatarFeatureCatalog.Features.Sum(f => f.WrittenNames.Count);

        int outbound = AvatarParameterContract.Parameters
            .Count(p => p.Flow == AvatarParameterFlow.AppToAvatar);

        Assert.Equal(outbound, claimed);
    }

    [Fact]
    public void Heart_rate_covers_far_more_than_the_six_names_that_were_hand_written()
    {
        var names = AvatarFeatureCatalog.NamesFor(AvatarFeatureCatalog.HeartRateKey);

        Assert.Contains("HR", names);
        Assert.Contains("VRCOSC/Heartrate/Value", names);
        Assert.Contains("MCB_Heartrate_Avg", names);
        Assert.True(names.Count > 6, $"only {names.Count} heart-rate names were claimed");
    }

    [Fact]
    public void The_camera_flash_is_a_feature_of_its_own()
    {
        // It was missing entirely, so a user who had enabled it had no way to tell whether their
        // avatar could receive it.
        Assert.NotEmpty(AvatarFeatureCatalog.NamesFor(AvatarFeatureCatalog.CameraFlashKey));
    }

    [Fact]
    public void No_two_features_claim_the_same_parameter()
    {
        var all = AvatarFeatureCatalog.Features.SelectMany(f => f.WrittenNames).ToList();

        Assert.Equal(all.Count, all.Distinct().Count());
    }

    [Fact]
    public void An_unknown_feature_asks_for_nothing_rather_than_throwing()
    {
        Assert.Empty(AvatarFeatureCatalog.NamesFor("NoSuchFeature"));
    }
}
