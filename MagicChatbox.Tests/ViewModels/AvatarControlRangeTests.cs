using MagicChatbox.Tests.TestDoubles;
using MagicChatbox.Vocabulary;
using MagicChatbox.Vrc;
using System.Linq;
using vrcosc_magicchatbox.Core.Vrc;
using vrcosc_magicchatbox.ViewModels.Avatar;
using Xunit;

namespace MagicChatbox.Tests.ViewModels;

// Nothing in this app parses OSCQuery's RANGE, so any bound the UI shows has to come from the
// protocol rather than from a guess. VRChat's Int parameter is a byte and its Float runs -1 to 1;
// the published contract records both ("0-255" nine times, "-1.0-1.0" three).
//
// The rule that follows: never invent a bound, and if a value arrives outside the track, widen the
// track rather than clamp it - a slider that disagrees with the number printed beside it is worse
// than a wide one.
public class AvatarControlRangeTests
{
    private static (AvatarControlRowViewModel Row, RecordingParameterSink Sink) Build(
        SignalKind kind, double value, bool writable = true)
    {
        var sink = new RecordingParameterSink();

        var row = new AvatarControlRowViewModel(
            AvatarControlCatalog.RowFor(
                new VrcParameterDeclaration(
                    kind == SignalKind.Int ? "Modes/Outfit" : "Face/Blush",
                    kind,
                    kind == SignalKind.Int ? SignalValue.Int((int)value) : SignalValue.Float((float)value),
                    writable)),
            sink);

        return (row, sink);
    }

    [Fact]
    public void A_stepper_stops_at_the_top_of_the_byte_rather_than_running_past_it()
    {
        // It used to clamp at zero and have no ceiling at all, so holding the plus button walked the
        // value past 255 into numbers VRChat cannot represent.
        (AvatarControlRowViewModel row, RecordingParameterSink sink) = Build(SignalKind.Int, 255);

        row.StepCommand.Execute("up");

        Assert.Equal(255, (int)row.Value);
        Assert.Empty(sink.Writes);
    }

    [Fact]
    public void A_stepper_stops_at_zero()
    {
        (AvatarControlRowViewModel row, RecordingParameterSink sink) = Build(SignalKind.Int, 0);

        row.StepCommand.Execute("down");

        Assert.Equal(0, (int)row.Value);
        Assert.Empty(sink.Writes);
    }

    [Fact]
    public void A_stepper_in_the_middle_still_moves_and_sends()
    {
        (AvatarControlRowViewModel row, RecordingParameterSink sink) = Build(SignalKind.Int, 3);

        row.StepCommand.Execute("up");

        Assert.Equal(4, (int)row.Value);
        Assert.Equal(4, sink.Writes.Single().Value);
    }

    [Fact]
    public void A_float_track_starts_at_the_common_range()
    {
        // Most float parameters are 0 to 1, and starting there keeps the useful precision.
        (AvatarControlRowViewModel row, _) = Build(SignalKind.Float, 0.4);

        Assert.Equal(0d, row.SliderMinimum);
        Assert.Equal(1d, row.SliderMaximum);
    }

    [Fact]
    public void A_negative_value_widens_the_track_instead_of_being_hidden_by_it()
    {
        // The track was hard-coded 0 to 1, so a parameter sitting at -0.5 - which the contract's own
        // FullHRPercent does - had a thumb pinned to the left end showing a number it did not match.
        (AvatarControlRowViewModel row, _) = Build(SignalKind.Float, -0.5);

        Assert.Equal(-1d, row.SliderMinimum);
        Assert.True(row.Value < 0);
    }

    [Fact]
    public void A_value_arriving_later_widens_the_track_too()
    {
        (AvatarControlRowViewModel row, _) = Build(SignalKind.Float, 0.2);

        Assert.Equal(0d, row.SliderMinimum);

        row.ObserveExternal(-0.75, hasValue: true);

        Assert.Equal(-1d, row.SliderMinimum);
    }

    [Fact]
    public void The_track_never_widens_past_what_the_protocol_allows()
    {
        (AvatarControlRowViewModel row, _) = Build(SignalKind.Float, 0.2);

        row.ObserveExternal(-9, hasValue: true);
        row.ObserveExternal(9, hasValue: true);

        Assert.Equal(AvatarControlRowViewModel.LowestFloat, row.SliderMinimum);
        Assert.Equal(AvatarControlRowViewModel.HighestFloat, row.SliderMaximum);
    }

    [Fact]
    public void A_read_only_row_sends_nothing_when_stepped()
    {
        (AvatarControlRowViewModel row, RecordingParameterSink sink) = Build(SignalKind.Int, 3, writable: false);

        row.StepCommand.Execute("up");

        Assert.Empty(sink.Writes);
    }
}
