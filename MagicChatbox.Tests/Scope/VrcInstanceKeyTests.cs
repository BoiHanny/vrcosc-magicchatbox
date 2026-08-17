using MagicChatbox.Vrc;
using Xunit;

namespace MagicChatbox.Tests.Scope;

// The instance key is the only place world context comes from, and the old pattern only accepted tokens
// shaped ~word(args). A bare ~ageGate stopped the match there, so everything after it -- the region, and
// worse, the access token -- was silently dropped. Absence of an access token reads as Public, so the
// failure direction was toward the most open answer.
public class VrcInstanceKeyTests
{
    [Fact]
    public void A_plain_public_instance_parses()
    {
        VrcInstance instance = VrcInstanceKey.Parse("wrld_abc123:12345");

        Assert.Equal("wrld_abc123", instance.WorldId);
        Assert.Equal("12345", instance.InstanceId);
        Assert.Equal(VrcInstanceAccess.Public, instance.Access);
    }

    [Theory]
    [InlineData("wrld_a:1~hidden(usr_x)~region(eu)", VrcInstanceAccess.FriendsPlus, "eu")]
    [InlineData("wrld_a:1~friends(usr_x)~region(use)", VrcInstanceAccess.Friends, "use")]
    [InlineData("wrld_a:1~private(usr_x)~region(jp)", VrcInstanceAccess.Invite, "jp")]
    [InlineData("wrld_a:1~private(usr_x)~canRequestInvite~region(usw)", VrcInstanceAccess.InvitePlus, "usw")]
    [InlineData("wrld_a:1~group(grp_x)~region(eu)", VrcInstanceAccess.Group, "eu")]
    public void Every_access_token_is_recognised(string key, VrcInstanceAccess access, string region)
    {
        VrcInstance instance = VrcInstanceKey.Parse(key);

        Assert.Equal(access, instance.Access);
        Assert.Equal(region, instance.Region);
    }

    [Fact]
    public void A_valueless_token_does_not_hide_everything_after_it()
    {
        // This is the bug. ~ageGate carries no parentheses, and the old pattern stopped there -- losing
        // ~private and reporting an invite-only instance as Public.
        VrcInstance instance = VrcInstanceKey.Parse("wrld_a:1~ageGate~private(usr_x)~region(eu)");

        Assert.Equal(VrcInstanceAccess.Invite, instance.Access);
        Assert.Equal("eu", instance.Region);
    }

    [Fact]
    public void The_joining_line_is_read_past_a_valueless_token()
    {
        const string line =
            "2026.08.17 21:04:11 Log        -  [Behaviour] Joining wrld_abc:4711~ageGate~hidden(usr_1)~region(eu)~nonce(deadbeef)";

        string key = VrcInstanceKey.ReadFromJoiningLine(line);
        VrcInstance instance = VrcInstanceKey.Parse(key);

        Assert.Equal("wrld_abc:4711~ageGate~hidden(usr_1)~region(eu)~nonce(deadbeef)", key);
        Assert.Equal(VrcInstanceAccess.FriendsPlus, instance.Access);
        Assert.Equal("eu", instance.Region);
    }

    [Fact]
    public void A_line_that_is_not_a_join_yields_nothing()
    {
        Assert.Equal(string.Empty, VrcInstanceKey.ReadFromJoiningLine("Joining or Creating Room: The Black Cat"));
        Assert.Equal(string.Empty, VrcInstanceKey.ReadFromJoiningLine(null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("wrld_nocolon")]
    [InlineData("wrld_a:")]
    [InlineData(":1234")]
    public void Anything_unparseable_is_None_rather_than_a_half_built_instance(string key)
    {
        Assert.False(VrcInstanceKey.Parse(key).IsKnown);
    }

    [Fact]
    public void The_world_id_folds_the_same_way_whether_it_came_from_a_key_or_a_saved_entry()
    {
        // Applied to both sides, or a world added to a group on one day stops matching itself on another.
        Assert.Equal("wrld_abc", VrcInstanceKey.BaseWorldId("wrld_abc:1234~private(x)"));
        Assert.Equal("wrld_abc", VrcInstanceKey.BaseWorldId("  WRLD_ABC  "));
        Assert.Equal("wrld_abc", VrcInstanceKey.BaseWorldId("wrld_abc"));
        Assert.Equal(string.Empty, VrcInstanceKey.BaseWorldId(null));
    }

    [Fact]
    public void An_unknown_token_is_ignored_rather_than_ending_the_parse()
    {
        VrcInstance instance = VrcInstanceKey.Parse("wrld_a:1~somethingNew(42)~friends(usr_x)~region(eu)");

        Assert.Equal(VrcInstanceAccess.Friends, instance.Access);
        Assert.Equal("eu", instance.Region);
    }
}

public class VrcCrowdBucketTests
{
    [Fact]
    public void A_fresh_reading_buckets_on_the_entry_thresholds()
    {
        Assert.Equal(VrcCrowd.Quiet, VrcCrowdBuckets.Classify(VrcCrowd.Unknown, 5));
        Assert.Equal(VrcCrowd.Busy, VrcCrowdBuckets.Classify(VrcCrowd.Unknown, 6));
        Assert.Equal(VrcCrowd.Packed, VrcCrowdBuckets.Classify(VrcCrowd.Unknown, 16));
    }

    [Fact]
    public void One_person_in_a_doorway_does_not_flip_the_bucket_back_and_forth()
    {
        // The whole reason the thresholds are asymmetric. Entering Busy takes six; leaving takes four.
        VrcCrowd crowd = VrcCrowdBuckets.Classify(VrcCrowd.Unknown, 6);
        Assert.Equal(VrcCrowd.Busy, crowd);

        foreach (int headcount in new[] { 5, 6, 5, 6, 5 })
        {
            crowd = VrcCrowdBuckets.Classify(crowd, headcount);
            Assert.Equal(VrcCrowd.Busy, crowd);
        }
    }

    [Fact]
    public void Leaving_a_bucket_takes_crossing_the_lower_threshold()
    {
        VrcCrowd crowd = VrcCrowdBuckets.Classify(VrcCrowd.Busy, 4);
        Assert.Equal(VrcCrowd.Busy, crowd);

        Assert.Equal(VrcCrowd.Quiet, VrcCrowdBuckets.Classify(VrcCrowd.Busy, 3));
    }

    [Fact]
    public void A_room_emptying_all_at_once_skips_straight_down()
    {
        Assert.Equal(VrcCrowd.Quiet, VrcCrowdBuckets.Classify(VrcCrowd.Packed, 1));
        Assert.Equal(VrcCrowd.Busy, VrcCrowdBuckets.Classify(VrcCrowd.Packed, 8));
    }

    [Fact]
    public void Packed_holds_until_it_drops_below_its_own_lower_threshold()
    {
        Assert.Equal(VrcCrowd.Packed, VrcCrowdBuckets.Classify(VrcCrowd.Packed, 12));
        Assert.Equal(VrcCrowd.Busy, VrcCrowdBuckets.Classify(VrcCrowd.Packed, 11));
    }

    [Fact]
    public void A_headcount_nobody_could_read_is_unknown_rather_than_quiet()
    {
        Assert.Equal(VrcCrowd.Unknown, VrcCrowdBuckets.Classify(VrcCrowd.Busy, -1));
    }
}
