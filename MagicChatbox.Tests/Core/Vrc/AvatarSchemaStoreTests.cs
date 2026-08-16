using MagicChatbox.Vocabulary;
using MagicChatbox.Vrc;
using System;
using System.Collections.Generic;
using System.Linq;
using vrcosc_magicchatbox.Core.Vrc;
using Xunit;

namespace MagicChatbox.Tests.Core.Vrc;

// The schema is the only thing that tells the app what an avatar can actually do. It arrives from an
// HTTP round trip that can finish after the avatar it describes has already been taken off, so the
// epoch check is the whole point rather than a detail.
public class AvatarSchemaStoreTests
{
    private static VrcAvatarSchemaHarvest Harvest(
        string avatarId, long epoch, params (string Name, SignalKind Kind, bool Writable)[] parameters)
        => new(
            avatarId,
            epoch,
            parameters
                .Select(p => new VrcParameterDeclaration(p.Name, p.Kind, SignalValue.Bool(false), p.Writable))
                .ToList(),
            Array.Empty<VrcFixedReading>());

    [Fact]
    public void A_fresh_store_knows_nothing_and_says_so()
    {
        var store = new AvatarSchemaStore();

        Assert.True(store.Current.IsEmpty);
        Assert.Equal(0, store.Current.WritableCount);
        Assert.False(store.TryGet("anything", out _));
    }

    [Fact]
    public void A_harvest_becomes_the_current_schema()
    {
        var store = new AvatarSchemaStore(() => 7);

        store.OnSchemaHarvested(Harvest(
            "avtr_test", 7,
            ("MCB/Ctrl/Panic", SignalKind.Bool, true),
            ("VRCEmote", SignalKind.Int, true),
            ("ScaleFactor", SignalKind.Float, false)));

        Assert.Equal("avtr_test", store.Current.AvatarId);
        Assert.Equal(3, store.Current.Parameters.Count);
        Assert.Equal(2, store.Current.WritableCount);
        Assert.Equal(1, store.Current.ReadOnlyCount);
    }

    [Fact]
    public void A_harvest_describing_an_avatar_nobody_is_wearing_is_dropped()
    {
        // The request was issued for epoch 7 and came back after the user swapped to epoch 8. Storing
        // it would make the whole page describe the previous avatar.
        long epoch = 7;
        var store = new AvatarSchemaStore(() => epoch);

        store.OnSchemaHarvested(Harvest("avtr_first", 7, ("A", SignalKind.Bool, true)));
        Assert.Equal("avtr_first", store.Current.AvatarId);

        epoch = 8;
        store.OnSchemaHarvested(Harvest("avtr_stale", 7, ("B", SignalKind.Bool, true)));

        Assert.Equal("avtr_first", store.Current.AvatarId);
        Assert.Equal(1, store.StaleDropped);
    }

    [Fact]
    public void Writability_is_taken_from_the_declaration_and_not_guessed_from_the_name()
    {
        // VRCEmote is writable and ScaleFactor is not, and they are neighbours under the same prefix.
        var store = new AvatarSchemaStore(() => 1);

        store.OnSchemaHarvested(Harvest(
            "avtr_test", 1,
            ("VRCEmote", SignalKind.Int, true),
            ("ScaleFactor", SignalKind.Float, false)));

        Assert.True(store.CanDrive("VRCEmote", SignalKind.Int));
        Assert.False(store.CanDrive("ScaleFactor", SignalKind.Float));
    }

    [Fact]
    public void Driving_requires_the_kind_to_match_as_well_as_the_name()
    {
        var store = new AvatarSchemaStore(() => 1);
        store.OnSchemaHarvested(Harvest("avtr_test", 1, ("HR", SignalKind.Int, true)));

        Assert.True(store.CanDrive("HR", SignalKind.Int));
        Assert.False(store.CanDrive("HR", SignalKind.Float));
    }

    [Fact]
    public void Matching_a_contract_against_an_avatar_returns_only_what_it_can_take()
    {
        var store = new AvatarSchemaStore(() => 1);

        store.OnSchemaHarvested(Harvest(
            "avtr_test", 1,
            ("HR", SignalKind.Int, true),
            ("HRPercent", SignalKind.Float, true),
            ("isHRBeat", SignalKind.Bool, false)));

        var matched = store.MatchDrivable(new[] { "HR", "HRPercent", "isHRBeat", "NotPresent" });

        Assert.Equal(new[] { "HR", "HRPercent" }, matched);
    }

    [Fact]
    public void Clearing_forgets_the_avatar()
    {
        var store = new AvatarSchemaStore(() => 1);
        store.OnSchemaHarvested(Harvest("avtr_test", 1, ("A", SignalKind.Bool, true)));

        store.Clear();

        Assert.True(store.Current.IsEmpty);
        Assert.False(store.TryGet("A", out _));
    }

    [Fact]
    public void Subscribers_hear_about_a_new_schema()
    {
        var store = new AvatarSchemaStore(() => 1);
        var seen = new List<string>();
        store.SchemaChanged += s => seen.Add(s.AvatarId);

        store.OnSchemaHarvested(Harvest("avtr_test", 1, ("A", SignalKind.Bool, true)));

        Assert.Equal(new[] { "avtr_test" }, seen);
    }

    [Fact]
    public void A_throwing_epoch_source_does_not_lose_the_harvest()
    {
        // Failing to read the epoch must not mean discarding a schema we successfully fetched.
        var store = new AvatarSchemaStore(() => throw new InvalidOperationException("no transport"));

        store.OnSchemaHarvested(Harvest("avtr_test", 3, ("A", SignalKind.Bool, true)));

        Assert.Equal("avtr_test", store.Current.AvatarId);
    }
}
