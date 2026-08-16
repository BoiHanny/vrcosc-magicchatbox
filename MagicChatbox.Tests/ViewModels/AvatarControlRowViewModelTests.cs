using MagicChatbox.Tests.TestDoubles;
using MagicChatbox.Vocabulary;
using System;
using System.Threading;
using vrcosc_magicchatbox.Core.Vrc;
using vrcosc_magicchatbox.ViewModels.Avatar;
using Xunit;

namespace MagicChatbox.Tests.ViewModels;

// A control has to answer the moment it is touched, because the round trip through VRChat and back
// is not instant and a control that waits for it feels broken. So the row paints the user's value
// straight away and ignores incoming values for a moment afterwards - otherwise the next poll, still
// carrying the old value, drags the toggle back under the user's finger.
public class AvatarControlRowViewModelTests
{
    private static AvatarControlRow Row(
        string name, SignalKind kind, bool writable, double value = 0)
        => new(
            name,
            AvatarControlCatalog.LeafOf(name),
            kind,
            writable,
            AvatarControlCatalog.WidgetFor(kind, writable, name),
            value,
            true,
            false);

    private static AvatarControlRowViewModel Bool(FakeOscSender sender, bool writable = true, double value = 0)
        => new(Row("Toggles/Hat", SignalKind.Bool, writable, value), new AvatarParameterRouter(sender, () => null));

    [Fact]
    public void Flipping_a_toggle_writes_it()
    {
        var sender = new FakeOscSender();
        var row = Bool(sender);

        row.BoolValue = true;

        Assert.Equal(true, sender.LastValueFor("/avatar/parameters/Toggles/Hat"));
    }

    [Fact]
    public void The_control_shows_the_new_value_before_VRChat_confirms_anything()
    {
        var sender = new FakeOscSender();
        var row = Bool(sender);

        row.BoolValue = true;

        Assert.True(row.BoolValue);
        Assert.True(row.IsHeld);
    }

    [Fact]
    public void A_poll_carrying_the_old_value_does_not_snap_the_control_back()
    {
        // The failure this prevents: tap a toggle, and 750ms later the refresh - still reporting what
        // VRChat knew before the write landed - flips it off again under your finger.
        var sender = new FakeOscSender();
        var row = Bool(sender);

        row.BoolValue = true;
        row.ObserveExternal(0, hasValue: true);

        Assert.True(row.BoolValue);
    }

    [Fact]
    public void Once_the_hold_expires_the_avatar_is_believed_again()
    {
        var sender = new FakeOscSender();
        var row = new AvatarControlRowViewModel(
            Row("Toggles/Hat", SignalKind.Bool, true),
            new AvatarParameterRouter(sender, () => null));

        row.BoolValue = true;

        // Reaching in rather than sleeping two seconds: the hold window is a design constant, not
        // something worth spending two seconds of every test run on.
        typeof(AvatarControlRowViewModel)
            .GetField("_heldUntilUtc", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(row, DateTime.UtcNow.AddSeconds(-1));

        row.ObserveExternal(0, hasValue: true);

        Assert.False(row.BoolValue);
        Assert.False(row.IsHeld);
    }

    [Fact]
    public void A_read_only_row_refuses_to_write()
    {
        var sender = new FakeOscSender();
        var row = Bool(sender, writable: false);

        row.BoolValue = true;

        Assert.Empty(sender.Parameters);
    }

    [Fact]
    public void A_read_only_row_reads_as_a_word_not_a_control()
    {
        var sender = new FakeOscSender();
        var row = new AvatarControlRowViewModel(
            Row("Grounded", SignalKind.Bool, false, 1),
            new AvatarParameterRouter(sender, () => null));

        Assert.True(row.IsReadOnly);
        Assert.False(row.IsToggle);
        Assert.Equal("yes", row.StateWord);
    }

    [Fact]
    public void Stepping_an_int_writes_the_new_number_and_never_goes_below_zero()
    {
        var sender = new FakeOscSender();
        var row = new AvatarControlRowViewModel(
            Row("Toggles/Outfit", SignalKind.Int, true, 1),
            new AvatarParameterRouter(sender, () => null));

        row.StepCommand.Execute("up");
        Assert.Equal(2, sender.LastValueFor("/avatar/parameters/Toggles/Outfit"));

        row.StepCommand.Execute("down");
        row.StepCommand.Execute("down");
        row.StepCommand.Execute("down");

        Assert.Equal(0, sender.LastValueFor("/avatar/parameters/Toggles/Outfit"));
    }

    [Fact]
    public void A_float_is_written_once_the_drag_ends_rather_than_on_every_pixel()
    {
        var sender = new FakeOscSender();
        var row = new AvatarControlRowViewModel(
            Row("Toggles/Size", SignalKind.Float, true, 0.2),
            new AvatarParameterRouter(sender, () => null));

        row.Value = 0.65;
        Assert.Empty(sender.Parameters);

        row.CommitFloatCommand.Execute(null);

        Assert.Equal(0.65f, (float)sender.LastValueFor("/avatar/parameters/Toggles/Size")!, 4);
    }

    [Fact]
    public void VRCEmote_is_offered_as_a_stepper_rather_than_a_slider()
    {
        // No OSCQuery RANGE is parsed anywhere, so a slider would invent bounds it cannot know.
        var sender = new FakeOscSender();
        var row = new AvatarControlRowViewModel(
            Row("VRCEmote", SignalKind.Int, true),
            new AvatarParameterRouter(sender, () => null));

        Assert.True(row.IsStepper);
        Assert.False(row.IsSlider);
    }
}
