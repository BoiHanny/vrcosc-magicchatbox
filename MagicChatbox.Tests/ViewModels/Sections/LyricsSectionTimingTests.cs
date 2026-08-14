using System;
using System.Collections.Generic;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.Modules.Lyrics;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.ViewModels.Sections;
using vrcosc_magicchatbox.ViewModels.State;
using Xunit;

namespace MagicChatbox.Tests.ViewModels.Sections;

/// <summary>
/// The Integrations ribbon and the Options page bind to the same singleton view model, so these
/// cover what both surfaces do when a timing button is pressed.
/// </summary>
public class LyricsSectionTimingTests
{
    private sealed class StubSettingsProvider<T> : ISettingsProvider<T> where T : class, new()
    {
        public T Value { get; } = new T();
        public int SaveCount { get; private set; }
        public void Save() => SaveCount++;
        public void FlushPendingSave() { }
        public void Reload() { }
        public event EventHandler SettingsChanged { add { } remove { } }
    }

    private static (LyricsSectionViewModel Vm, StubSettingsProvider<LyricsSettings> Lyrics) Build()
    {
        var lyrics = new StubSettingsProvider<LyricsSettings>();
        var vm = new LyricsSectionViewModel(
            lyrics,
            new StubSettingsProvider<AppSettings>(),
            new StubSettingsProvider<IntegrationSettings>(),
            new LyricsDisplayState());

        return (vm, lyrics);
    }

    [Fact]
    public void Nudging_the_offset_writes_the_setting_and_asks_for_a_save()
    {
        var (vm, lyrics) = Build();

        vm.NudgeOffsetCommand.Execute("-100");

        Assert.Equal(-100, vm.Settings.OffsetMs);
        Assert.Equal(1, lyrics.SaveCount);
    }

    [Fact]
    public void Holding_a_nudge_button_cannot_push_the_offset_past_ten_seconds()
    {
        var (vm, _) = Build();

        for (int i = 0; i < 50; i++)
            vm.NudgeOffsetCommand.Execute("1000");

        Assert.Equal(LyricsTuning.MaxOffsetMs, vm.Settings.OffsetMs);

        for (int i = 0; i < 100; i++)
            vm.NudgeOffsetCommand.Execute("-1000");

        Assert.Equal(LyricsTuning.MinOffsetMs, vm.Settings.OffsetMs);
    }

    [Fact]
    public void Reset_puts_the_offset_back_in_sync()
    {
        var (vm, _) = Build();
        vm.NudgeOffsetCommand.Execute("1000");

        vm.ResetOffsetCommand.Execute(null);

        Assert.Equal(0, vm.Settings.OffsetMs);
        Assert.Equal("in sync", vm.OffsetChip);
    }

    /// <summary>
    /// The ribbon pill binds to OffsetChip and the flyout to OffsetSummary. Neither is an
    /// [ObservableProperty], so they only refresh because the settings object raises a change - which
    /// is exactly the wiring worth pinning down.
    /// </summary>
    [Fact]
    public void The_readouts_announce_themselves_when_the_offset_moves()
    {
        var (vm, _) = Build();
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.NudgeOffsetCommand.Execute("300");

        Assert.Contains(nameof(vm.OffsetChip), raised);
        Assert.Contains(nameof(vm.OffsetSummary), raised);
        Assert.Equal("+300 ms", vm.OffsetChip);
        Assert.Equal("Lyrics run 300 ms early", vm.OffsetSummary);
    }

    [Fact]
    public void Gap_and_hold_steppers_move_their_own_setting_and_nothing_else()
    {
        var (vm, lyrics) = Build();
        int hold = vm.Settings.LineHoldSeconds;

        vm.NudgeGapThresholdCommand.Execute("1");

        Assert.Equal(9, vm.Settings.GapThresholdSeconds);
        Assert.Equal(hold, vm.Settings.LineHoldSeconds);
        Assert.Equal(1, lyrics.SaveCount);

        vm.NudgeLineHoldCommand.Execute("-1");

        Assert.Equal(9, vm.Settings.GapThresholdSeconds);
        Assert.Equal(hold - 1, vm.Settings.LineHoldSeconds);
    }

    [Fact]
    public void The_warning_appears_only_once_the_hold_has_caught_up_with_the_gap()
    {
        var (vm, _) = Build();
        Assert.False(vm.HasTimingWarning);

        // Default is gap 8 / hold 6, so two taps on hold is enough to disable the break marker.
        vm.NudgeLineHoldCommand.Execute("1");
        Assert.False(vm.HasTimingWarning);
        vm.NudgeLineHoldCommand.Execute("1");

        Assert.True(vm.HasTimingWarning);
        Assert.NotNull(vm.TimingWarning);

        vm.NudgeGapThresholdCommand.Execute("1");
        Assert.False(vm.HasTimingWarning);
    }

    /// <summary>
    /// Nothing clamps these on load, so a hand-edited settings file can arrive with a gap of 900.
    /// One tap has to bring it back into a range the steppers can work with.
    /// </summary>
    [Fact]
    public void A_wild_stored_value_is_recovered_by_a_single_tap()
    {
        var (vm, _) = Build();
        vm.Settings.GapThresholdSeconds = 900;
        vm.Settings.LineHoldSeconds = -4;

        vm.NudgeGapThresholdCommand.Execute("-1");
        vm.NudgeLineHoldCommand.Execute("1");

        Assert.Equal(29, vm.Settings.GapThresholdSeconds);
        Assert.Equal(2, vm.Settings.LineHoldSeconds);
    }

    [Fact]
    public void A_command_parameter_that_is_not_a_number_leaves_everything_alone()
    {
        var (vm, lyrics) = Build();

        vm.NudgeOffsetCommand.Execute("half a second");
        vm.NudgeGapThresholdCommand.Execute("");

        Assert.Equal(0, vm.Settings.OffsetMs);
        Assert.Equal(8, vm.Settings.GapThresholdSeconds);
        Assert.Equal(0, lyrics.SaveCount);
    }
}
