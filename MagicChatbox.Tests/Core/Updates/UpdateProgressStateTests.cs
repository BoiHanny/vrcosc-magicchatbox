using System;
using System.Linq;
using vrcosc_magicchatbox.Core.Updates;
using Xunit;

namespace MagicChatbox.Tests.Core.Updates;

public class UpdateProgressStateTests
{
    private static UpdateProgressState Started()
    {
        var state = new UpdateProgressState();
        state.Begin("Updating to 0.9.222");
        return state;
    }

    [Fact]
    public void Begin_activates_the_card_and_lists_the_four_steps()
    {
        var state = Started();

        Assert.True(state.IsActive);
        Assert.Equal("Updating to 0.9.222", state.Headline);
        Assert.True(state.IsIndeterminate);
        Assert.Equal(0, state.Percent);
        Assert.Equal(
            new[] { UpdateStepKind.Download, UpdateStepKind.Verify, UpdateStepKind.Unpack, UpdateStepKind.Install },
            state.Steps.Select(s => s.Kind));
        Assert.All(state.Steps, step => Assert.Equal(UpdateStepStatus.Pending, step.Status));
    }

    [Fact]
    public void Begin_clears_the_results_of_a_previous_attempt()
    {
        var state = Started();
        state.SetStep(UpdateStepKind.Download, UpdateStepStatus.Done, "14.7 MB");
        state.Fail("network died");

        state.Begin("Updating to 0.9.223");

        Assert.False(state.IsFailed);
        Assert.All(state.Steps, step =>
        {
            Assert.Equal(UpdateStepStatus.Pending, step.Status);
            Assert.Equal(string.Empty, step.Detail);
        });
    }

    [Fact]
    public void Report_leaves_indeterminate_mode_and_clamps_out_of_range_values()
    {
        var state = Started();

        state.Report(140, "overshoot");
        Assert.False(state.IsIndeterminate);
        Assert.Equal(100, state.Percent);

        state.Report(-20, "undershoot");
        Assert.Equal(0, state.Percent);
    }

    [Fact]
    public void ReportIndeterminate_goes_back_to_an_unknown_length_operation()
    {
        var state = Started();
        state.Report(50, "halfway");

        state.ReportIndeterminate("hashing");

        Assert.True(state.IsIndeterminate);
        Assert.Equal("hashing", state.Detail);
    }

    [Fact]
    public void Fail_marks_the_running_step_failed_and_leaves_finished_steps_alone()
    {
        var state = Started();
        state.SetStep(UpdateStepKind.Download, UpdateStepStatus.Done, "14.7 MB");
        state.SetStep(UpdateStepKind.Verify, UpdateStepStatus.Running);

        state.Fail("checksum did not match");

        Assert.True(state.IsFailed);
        Assert.Equal(UpdateStepStatus.Done, state.Step(UpdateStepKind.Download).Status);
        Assert.Equal(UpdateStepStatus.Failed, state.Step(UpdateStepKind.Verify).Status);
        Assert.Equal(UpdateStepStatus.Pending, state.Step(UpdateStepKind.Unpack).Status);
    }

    [Fact]
    public void The_card_can_only_be_dismissed_once_it_has_stopped_working()
    {
        var state = Started();
        Assert.False(state.CanDismiss);

        state.Fail("boom");
        Assert.True(state.CanDismiss);

        state.Reset();
        Assert.False(state.IsActive);
        Assert.False(state.CanDismiss);
    }

    [Fact]
    public void Complete_finishes_at_a_hundred_percent()
    {
        var state = Started();

        state.Complete("done");

        Assert.True(state.IsCompleted);
        Assert.Equal(100, state.Percent);
        Assert.False(state.IsIndeterminate);
    }

    [Fact]
    public void CompletedSteps_counts_a_warning_as_finished_because_the_step_still_ran()
    {
        var state = Started();
        state.SetStep(UpdateStepKind.Download, UpdateStepStatus.Done);
        state.SetStep(UpdateStepKind.Verify, UpdateStepStatus.Warning, "no checksum published");
        state.SetStep(UpdateStepKind.Unpack, UpdateStepStatus.Running);

        Assert.Equal(
            new[] { UpdateStepKind.Download, UpdateStepKind.Verify },
            state.CompletedSteps());
    }

    [Fact]
    public void A_step_exposes_a_glyph_that_tracks_its_status()
    {
        var step = new UpdateStepViewModel(UpdateStepKind.Verify, "Verify integrity");
        Assert.Equal("○", step.Glyph);

        step.Status = UpdateStepStatus.Running;
        Assert.Equal("●", step.Glyph);

        step.Status = UpdateStepStatus.Done;
        Assert.Equal("✔", step.Glyph);

        step.Status = UpdateStepStatus.Warning;
        Assert.Equal("!", step.Glyph);

        step.Status = UpdateStepStatus.Failed;
        Assert.Equal("✕", step.Glyph);
    }

    [Fact]
    public void A_step_only_reserves_room_for_a_detail_line_when_it_has_one()
    {
        var step = new UpdateStepViewModel(UpdateStepKind.Download, "Download");
        Assert.False(step.HasDetail);

        step.Detail = "14.7 MB";
        Assert.True(step.HasDetail);

        step.Detail = "   ";
        Assert.False(step.HasDetail);
    }

    [Theory]
    [InlineData(512L, "1 KB")]
    [InlineData(1024L * 400, "400 KB")]
    [InlineData(1024L * 1024, "1.0 MB")]
    [InlineData(15382313L, "14.7 MB")]
    public void DescribeBytes_switches_to_megabytes_once_there_are_any(long bytes, string expected)
    {
        Assert.Equal(expected, UpdateProgressState.DescribeBytes(bytes));
    }

    [Fact]
    public void DescribeTransfer_names_both_ends_and_the_rate()
    {
        string text = UpdateProgressState.DescribeTransfer(
            5L * 1024 * 1024,
            15L * 1024 * 1024,
            TimeSpan.FromSeconds(5));

        Assert.Equal("5.0 MB of 15.0 MB · 1.0 MB/s", text);
    }

    [Fact]
    public void DescribeTransfer_omits_the_rate_until_there_is_enough_time_to_measure_one()
    {
        string text = UpdateProgressState.DescribeTransfer(
            1024L * 1024,
            10L * 1024 * 1024,
            TimeSpan.FromMilliseconds(100));

        Assert.Equal("1.0 MB of 10.0 MB", text);
    }

    [Fact]
    public void DescribeTransfer_copes_with_a_server_that_never_said_how_big_the_file_is()
    {
        string text = UpdateProgressState.DescribeTransfer(1024L * 1024, null, TimeSpan.FromMilliseconds(10));

        Assert.Equal("1.0 MB", text);
    }

    [Theory]
    [InlineData(0L, 100L, 0d)]
    [InlineData(50L, 100L, 50d)]
    [InlineData(100L, 100L, 100d)]
    [InlineData(150L, 100L, 100d)]
    public void PercentOf_stays_inside_the_bar(long done, long total, double expected)
    {
        Assert.Equal(expected, UpdateProgressState.PercentOf(done, total));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    public void PercentOf_reports_zero_when_the_total_is_unknown(long? total)
    {
        Assert.Equal(0d, UpdateProgressState.PercentOf(42, total));
    }
}
