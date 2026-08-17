using System;
using System.Collections.Generic;
using System.Linq;
using vrcosc_magicchatbox.Core.Vrc;
using Xunit;

namespace MagicChatbox.Tests.Core.Vrc;

// The one question the avatar-carried settings feature rests on, unanswered since it shipped: when
// this app writes a saved parameter over OSC, is that what VRChat keeps? The documented way to find
// out needed a purpose-built test avatar. It does not: VRChat writes every avatar's saved state to
// LocalAvatarData when you switch avatar or quit, so the answer is on disk after ordinary use.
//
// This compares what the pump last sent against what VRChat wrote down. It reports what it saw and
// refuses to conclude anything it did not see.
public class SavedWriteAuditTests
{
    private static LocalAvatarState Saved(params (string Name, double Value)[] values)
        => new(
            "avtr_test",
            1.3,
            false,
            values.Select(v => new LocalAvatarValue(v.Name, v.Value)).ToList(),
            DateTime.UtcNow);

    [Fact]
    public void Nothing_sent_means_nothing_claimed()
    {
        SavedWriteReport report = SavedWriteAudit.Compare(new Dictionary<string, double>(), Saved(("A", 1)));

        Assert.Empty(report.Rows);
        Assert.Contains("Nothing to compare", report.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void A_value_VRChat_kept_is_evidence_that_writes_persist()
    {
        SavedWriteReport report = SavedWriteAudit.Compare(
            new Dictionary<string, double> { ["Toggles/Hat"] = 1 },
            Saved(("Toggles/Hat", 1)));

        Assert.Equal(1, report.Kept);
        Assert.Equal(0, report.Replaced);
        Assert.Contains("do persist", report.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void A_value_VRChat_overwrote_is_evidence_that_they_do_not()
    {
        SavedWriteReport report = SavedWriteAudit.Compare(
            new Dictionary<string, double> { ["Toggles/Hat"] = 1 },
            Saved(("Toggles/Hat", 0)));

        Assert.Equal(0, report.Kept);
        Assert.Equal(1, report.Replaced);
        Assert.Contains("do not persist", report.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void A_mixed_result_is_reported_as_mixed_rather_than_rounded_to_a_yes_or_a_no()
    {
        SavedWriteReport report = SavedWriteAudit.Compare(
            new Dictionary<string, double> { ["Kept"] = 1, ["Lost"] = 1 },
            Saved(("Kept", 1), ("Lost", 0)));

        Assert.Contains("not reliable", report.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void A_parameter_VRChat_does_not_save_is_not_counted_as_evidence_either_way()
    {
        // Only parameters marked saved on the avatar appear in the file at all. An unsaved one going
        // missing says nothing about whether OSC writes persist, so it must not be read as a failure.
        SavedWriteReport report = SavedWriteAudit.Compare(
            new Dictionary<string, double> { ["Toggles/Hat"] = 1, ["Momentary"] = 1 },
            Saved(("Toggles/Hat", 1)));

        Assert.Equal(1, report.Kept);
        Assert.Equal(1, report.NotSaved);
        Assert.Equal(1, report.Compared);
        Assert.Contains("do persist", report.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Floats_are_compared_with_room_for_the_wire()
    {
        // A float goes out as a single and comes back through a JSON round trip, so demanding exact
        // equality would report a difference that is the encoding rather than VRChat's behaviour.
        SavedWriteReport report = SavedWriteAudit.Compare(
            new Dictionary<string, double> { ["Face/Blush"] = 0.4 },
            Saved(("Face/Blush", 0.40000001)));

        Assert.Equal(1, report.Kept);
    }

    [Fact]
    public void A_missing_file_is_reported_rather_than_treated_as_a_refusal()
    {
        SavedWriteReport report = SavedWriteAudit.Compare(
            new Dictionary<string, double> { ["Toggles/Hat"] = 1 },
            saved: null);

        Assert.Equal(0, report.Compared);
        Assert.Equal(1, report.NotSaved);
        Assert.Contains("has not saved any", report.Summary, StringComparison.Ordinal);
    }
}
