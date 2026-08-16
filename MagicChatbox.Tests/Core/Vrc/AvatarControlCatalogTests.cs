using MagicChatbox.Vocabulary;
using MagicChatbox.Vrc;
using System;
using System.Collections.Generic;
using System.Linq;
using vrcosc_magicchatbox.Core.Vrc;
using Xunit;

namespace MagicChatbox.Tests.Core.Vrc;

// Turning 666 declarations into something a person can operate. The rules come from the avatar
// control page design: the widget follows kind plus writability, read-only is a fact rather than a
// broken control, and the largest bucket on a real avatar is the one with no slash in the name at
// all.
public class AvatarControlCatalogTests
{
    private static AvatarSchemaSnapshot Schema(params (string Name, SignalKind Kind, bool Writable)[] parameters)
        => new(
            "avtr_test",
            1,
            DateTime.UtcNow,
            parameters
                .Select(p => new VrcParameterDeclaration(p.Name, p.Kind, SignalValue.Bool(false), p.Writable))
                .ToList());

    [Theory]
    [InlineData(SignalKind.Bool, true, "Toggles/Hat", AvatarWidget.Toggle)]
    [InlineData(SignalKind.Bool, false, "Grounded", AvatarWidget.StateWord)]
    [InlineData(SignalKind.Int, true, "Toggles/Outfit", AvatarWidget.Stepper)]
    [InlineData(SignalKind.Int, true, "VRCEmote", AvatarWidget.Emote)]
    [InlineData(SignalKind.Float, true, "Toggles/Size", AvatarWidget.Slider)]
    [InlineData(SignalKind.Float, false, "VelocityMagnitude", AvatarWidget.Meter)]
    public void The_widget_follows_the_kind_and_the_write_bit(
        SignalKind kind, bool writable, string name, AvatarWidget expected)
    {
        Assert.Equal(expected, AvatarControlCatalog.WidgetFor(kind, writable, name));
    }

    [Fact]
    public void A_read_only_bool_becomes_a_word_and_never_a_dead_toggle()
    {
        // A greyed-out toggle reads as broken. Grounded is a fact about you, not a control.
        Assert.Equal(AvatarWidget.StateWord, AvatarControlCatalog.WidgetFor(SignalKind.Bool, false, "Grounded"));
        Assert.NotEqual(AvatarWidget.Toggle, AvatarControlCatalog.WidgetFor(SignalKind.Bool, false, "Grounded"));
    }

    [Fact]
    public void Names_without_a_slash_land_in_one_ungrouped_bucket_that_sorts_last()
    {
        // Measured on real avatars: 44% of parameters contain no slash, so this is the biggest group
        // and it must not masquerade as a folder.
        var view = AvatarControlCatalog.Build(Schema(
            ("Toggles/Hat", SignalKind.Bool, true),
            ("Flat", SignalKind.Bool, true),
            ("AlsoFlat", SignalKind.Bool, true)));

        Assert.Equal(2, view.Groups.Count);
        Assert.Equal("Toggles", view.Groups[0].Name);
        Assert.True(view.Groups[1].IsUngrouped);
        Assert.Equal("Ungrouped", view.Groups[1].DisplayName);
        Assert.Equal(2, view.Groups[1].Rows.Count);
    }

    [Fact]
    public void Built_ins_are_counted_apart_from_the_author_s_own_parameters()
    {
        var view = AvatarControlCatalog.Build(Schema(
            ("Toggles/Hat", SignalKind.Bool, true),
            ("Grounded", SignalKind.Bool, false),
            ("VelocityMagnitude", SignalKind.Float, false)));

        Assert.Equal(1, view.CustomCount);
        Assert.Equal(2, view.BuiltInCount);
    }

    [Fact]
    public void Adult_parameters_are_hidden_by_default_and_counted_never_named()
    {
        // This page opens by default. Measured: roughly 30 of 197 avatars carry explicit groups, and
        // one real avatar's first screenful is entirely contact parameters.
        var view = AvatarControlCatalog.Build(Schema(
            ("Toggles/Hat", SignalKind.Bool, true),
            ("OGB/Orf/Anal/PenSelfNewTip", SignalKind.Float, true),
            ("OGB/Orf/Handjob/PenSelfNewRoot", SignalKind.Float, true)));

        Assert.Single(view.Groups);
        Assert.Equal("Toggles", view.Groups[0].Name);
        Assert.Equal(2, view.HiddenRowCount);
        Assert.True(view.HiddenGroupCount >= 1);

        Assert.DoesNotContain(
            view.Groups.SelectMany(g => g.Rows),
            r => r.Name.Contains("Anal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Turning_the_filter_off_shows_them_again()
    {
        var view = AvatarControlCatalog.Build(
            Schema(("OGB/Orf/Anal/PenSelfNewTip", SignalKind.Float, true)),
            hideAdult: false);

        Assert.Single(view.Groups);
        Assert.Equal(0, view.HiddenRowCount);
    }

    [Fact]
    public void An_ordinary_parameter_is_not_caught_by_the_adult_filter()
    {
        // False positives here are their own failure: hiding somebody's clothing toggle is worse
        // than useless because they cannot find it and are not told why.
        Assert.False(AvatarControlCatalog.IsAdultName("Toggles/Hat"));
        Assert.False(AvatarControlCatalog.IsAdultName("Clothing/Jacket"));
        Assert.False(AvatarControlCatalog.IsAdultName("Go/Locomotion"));
        Assert.False(AvatarControlCatalog.IsAdultName("FT/v2/JawOpen"));
        Assert.False(AvatarControlCatalog.IsAdultName("Hair/Length"));
    }

    [Fact]
    public void Search_matches_the_author_s_own_spelling_anywhere_in_the_name()
    {
        var view = AvatarControlCatalog.Build(
            Schema(
                ("Toggles/Ring", SignalKind.Bool, true),
                ("Toggles/Hat", SignalKind.Bool, true),
                ("Clothing/Ringlet", SignalKind.Bool, true)),
            search: "ring");

        var names = view.Groups.SelectMany(g => g.Rows).Select(r => r.Name).ToList();

        Assert.Contains("Toggles/Ring", names);
        Assert.Contains("Clothing/Ringlet", names);
        Assert.DoesNotContain("Toggles/Hat", names);
    }

    [Fact]
    public void Only_drivable_parameters_render_when_asked_for_controls()
    {
        // Offering to write a read-only parameter is a lie, and 22% of them are read-only.
        var view = AvatarControlCatalog.Build(
            Schema(
                ("Toggles/Hat", SignalKind.Bool, true),
                ("Grounded", SignalKind.Bool, false)),
            writableOnly: true);

        var rows = view.Groups.SelectMany(g => g.Rows).ToList();

        Assert.Single(rows);
        Assert.Equal("Toggles/Hat", rows[0].Name);
    }

    [Fact]
    public void A_live_value_overrides_the_one_the_schema_was_harvested_with()
    {
        var senses = new AvatarSenseStore();
        var observation = new VrcObservation(
            SignalKey.Intern(AvatarSenseStore.ParameterKeyPrefix + "Toggles/Size"),
            SignalValue.Float(0.75),
            1);
        senses.OnObservation(in observation);

        var view = AvatarControlCatalog.Build(
            Schema(("Toggles/Size", SignalKind.Float, true)),
            senses);

        AvatarControlRow row = view.Groups.SelectMany(g => g.Rows).Single();

        Assert.True(row.HasValue);
        Assert.Equal(0.75, row.Value, 5);
    }

    [Fact]
    public void The_leaf_is_what_a_person_reads_in_a_deep_name()
    {
        // 36% of parameters sit two or more levels deep; showing the whole path in every row is noise.
        Assert.Equal("PenSelfNewTip", AvatarControlCatalog.LeafOf("OGB/Orf/Anal/PenSelfNewTip"));
        Assert.Equal("Hat", AvatarControlCatalog.LeafOf("Toggles/Hat"));
        Assert.Equal("Flat", AvatarControlCatalog.LeafOf("Flat"));
    }

    [Fact]
    public void An_empty_schema_produces_an_empty_view_rather_than_throwing()
    {
        var view = AvatarControlCatalog.Build(AvatarSchemaSnapshot.Empty);

        Assert.Empty(view.Groups);
        Assert.Equal(0, view.CustomCount);
        Assert.Equal(0, view.BuiltInCount);
    }
}
