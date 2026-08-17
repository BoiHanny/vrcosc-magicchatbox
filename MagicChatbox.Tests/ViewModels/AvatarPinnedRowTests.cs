using MagicChatbox.Tests.TestDoubles;
using MagicChatbox.Vocabulary;
using System.Linq;
using vrcosc_magicchatbox.Core.Vrc;
using vrcosc_magicchatbox.ViewModels.Avatar;
using Xunit;

namespace MagicChatbox.Tests.ViewModels;

// The worst avatar on this machine declares 684 parameters. A list that long is not a control panel,
// it is a haystack, so the six a person actually touches have to be reachable without searching for
// them every time.
//
// The discipline this keeps: pinning changes WHERE a row is drawn and never WHAT KIND of row it is.
// One template, one set of widget rules, one place a bug can be.
public class AvatarPinnedRowTests
{
    private static AvatarControlRowViewModel Row(
        string name, SignalKind kind = SignalKind.Bool, bool writable = true)
        => new(
            AvatarControlCatalog.RowFor(
                new MagicChatbox.Vrc.VrcParameterDeclaration(name, kind, SignalValue.Bool(false), writable)),
            new RecordingParameterSink());

    [Fact]
    public void A_row_can_be_pinned_and_unpinned()
    {
        AvatarControlRowViewModel row = Row("Toggles/Hat");

        Assert.False(row.IsPinned);

        row.TogglePinCommand.Execute(null);
        Assert.True(row.IsPinned);

        row.TogglePinCommand.Execute(null);
        Assert.False(row.IsPinned);
    }

    [Fact]
    public void Pinning_tells_whoever_is_keeping_the_list()
    {
        AvatarControlRowViewModel? told = null;

        var row = new AvatarControlRowViewModel(
            AvatarControlCatalog.RowFor(
                new MagicChatbox.Vrc.VrcParameterDeclaration("Toggles/Hat", SignalKind.Bool, SignalValue.Bool(false), true)),
            new RecordingParameterSink(),
            r => told = r);

        row.TogglePinCommand.Execute(null);

        Assert.Same(row, told);
        Assert.True(told!.IsPinned);
    }

    [Fact]
    public void Only_the_parameters_that_can_never_change_are_unpinnable()
    {
        // Narrower than the rule presets use, deliberately. A preset WRITES on its own, so it must
        // refuse VRChat's own parameters or it would make somebody perform an emote. A pin writes
        // nothing - it moves a row to the top, and pressing it afterwards is as deliberate as pressing
        // it in the tree. What is worth refusing is a pin that can never do anything: VRChat documents
        // PreviewMode and IsOnFriendsList as carrying nothing over OSC, and a favourite that never
        // changes is clutter with a star on it.
        Assert.False(Row("PreviewMode").CanPin);
        Assert.False(Row("IsOnFriendsList").CanPin);

        Assert.True(Row("VRCEmote", SignalKind.Int).CanPin);
        Assert.True(Row("Toggles/Hat").CanPin);
    }

    [Fact]
    public void An_unpinnable_row_stays_unpinned_when_the_command_is_run_anyway()
    {
        AvatarControlRowViewModel row = Row("PreviewMode");

        row.TogglePinCommand.Execute(null);

        Assert.False(row.IsPinned);
    }

    [Fact]
    public void A_read_only_row_can_still_be_pinned()
    {
        // Quick access is a dashboard, not only a control panel - keeping Grounded next to your own
        // toggles is a legitimate thing to want.
        AvatarControlRowViewModel row = Row("Grounded", SignalKind.Bool, writable: false);

        Assert.True(row.CanPin);

        row.TogglePinCommand.Execute(null);

        Assert.True(row.IsPinned);
    }

    [Fact]
    public void A_pinned_row_is_the_same_kind_of_row_it_was_in_the_tree()
    {
        // If pinning produced a different widget, there would be two control vocabularies to keep in
        // step and one of them would rot.
        AvatarControlRowViewModel toggle = Row("Toggles/Hat");
        AvatarControlRowViewModel stepper = Row("Modes/Outfit", SignalKind.Int);
        AvatarControlRowViewModel slider = Row("Face/Blush", SignalKind.Float);

        Assert.True(toggle.IsToggle);
        Assert.True(stepper.IsStepper);
        Assert.True(slider.IsSlider);

        foreach (AvatarControlRowViewModel row in new[] { toggle, stepper, slider })
            row.TogglePinCommand.Execute(null);

        Assert.True(toggle.IsToggle);
        Assert.True(stepper.IsStepper);
        Assert.True(slider.IsSlider);
    }

    [Fact]
    public void The_catalog_builds_the_same_row_whether_or_not_it_went_through_a_filter()
    {
        // Pinned rows are built straight from the schema rather than from the filtered view, so that a
        // search does not make somebody's pinned controls disappear. That only holds while both paths
        // agree about what a row is.
        var declaration = new MagicChatbox.Vrc.VrcParameterDeclaration(
            "Modes/Outfit", SignalKind.Int, SignalValue.Int(2), true);

        var schema = new AvatarSchemaSnapshot(
            "avtr_test", 1, System.DateTime.UtcNow, [declaration]);

        AvatarControlRow direct = AvatarControlCatalog.RowFor(declaration);
        AvatarControlRow filtered = AvatarControlCatalog.Build(schema).Groups.Single().Rows.Single();

        Assert.Equal(filtered, direct);
    }
}
