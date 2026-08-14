using System;
using System.ComponentModel;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Osc;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.ViewModels.State;
using Xunit;

namespace MagicChatbox.Tests.Core.Osc;

public class OscOutputBuilderTests
{
    private const int Limit = OscBuildContext.MaxOscLength;

    /// <summary>U+2026, the mark the builder puts on a clipped segment.</summary>
    private const string Clip = "…";

    /// <summary>U+2506, the stock separator between two integrations.</summary>
    private const string Join = " ┆ ";

    [Fact]
    public void One_oversized_segment_no_longer_blanks_the_chatbox()
    {
        var result = Build(new FakeProvider("Window", "Window", 50, new string('a', 300)));

        Assert.Equal(Limit, result.Message.Length);
        Assert.Equal(new string('a', Limit - 1) + Clip, result.Message);
        Assert.Empty(result.TrimmedProviders);
        Assert.Equal(new[] { "Window" }, result.IncludedProviders);
        Assert.True(result.ExceededLimit);
    }

    [Fact]
    public void The_segment_left_standing_is_the_one_with_the_lowest_priority_number()
    {
        var result = Build(
            new FakeProvider("Status", "Status", 10, new string('s', 200)),
            new FakeProvider("Time", "Time", 90, "12:00"));

        Assert.Equal(new[] { "Time" }, result.TrimmedProviders);
        Assert.Equal(new[] { "Status" }, result.IncludedProviders);
        Assert.DoesNotContain("12:00", result.Message);
        Assert.Equal(Limit, result.Message.Length);
    }

    [Fact]
    public void Everything_above_the_survivor_is_still_dropped_by_priority()
    {
        var result = Build(
            new FakeProvider("Status", "Status", 10, new string('s', 100)),
            new FakeProvider("Window", "Window", 50, new string('w', 100)),
            new FakeProvider("Time", "Time", 90, new string('t', 100)));

        Assert.Equal(new[] { "Time", "Window" }, result.TrimmedProviders);
        Assert.Equal(new string('s', 100), result.Message);
    }

    [Fact]
    public void The_clip_leaves_the_prefix_and_suffix_whole()
    {
        var result = Build(
            s => { s.OscMessagePrefix = "[[["; s.OscMessageSuffix = "]]"; },
            new FakeProvider("Window", "Window", 50, new string('w', 300)));

        Assert.Equal(Limit, result.Message.Length);
        Assert.Equal("[[[" + new string('w', Limit - 6) + Clip + "]]", result.Message);
    }

    [Fact]
    public void A_clip_never_cuts_an_emoji_in_half()
    {
        // Astral-plane emoji are two characters each, so the budget lands mid-pair.
        string grinning = string.Concat(Enumerable.Repeat("\U0001F600", 100));

        var result = Build(new FakeProvider("Window", "Window", 50, grinning));

        Assert.Equal(string.Concat(Enumerable.Repeat("\U0001F600", 71)) + Clip, result.Message);
        Assert.False(char.IsHighSurrogate(result.Message[^2]));
    }

    [Fact]
    public void A_prefix_that_eats_the_whole_line_still_reaches_the_chatbox()
    {
        var result = Build(
            s => s.OscMessagePrefix = new string('p', 200),
            new FakeProvider("Window", "Window", 50, "a window title"));

        // Nothing is left for the segment, so it counts as dropped - but the line is not blank, and
        // the truncation that explains why now actually runs.
        Assert.Equal(new string('p', Limit), result.Message);
        Assert.Equal(new[] { "Window" }, result.TrimmedProviders);
        Assert.Empty(result.IncludedProviders);
    }

    [Fact]
    public void With_no_segments_at_all_the_prefix_is_not_sent_on_its_own()
    {
        var result = Build(s => { s.OscMessagePrefix = "hi"; s.OscMessageSuffix = "bye"; });

        Assert.Equal(string.Empty, result.Message);
        Assert.False(result.ExceededLimit);
    }

    [Fact]
    public void A_line_that_fits_is_left_exactly_as_it_was()
    {
        var result = Build(
            new FakeProvider("Status", "Status", 10, "away"),
            new FakeProvider("Time", "Time", 90, "12:00"));

        Assert.Equal("away" + Join + "12:00", result.Message);
        Assert.False(result.ExceededLimit);
        Assert.Empty(result.TrimmedProviders);
    }

    [Fact]
    public void Every_segment_reports_what_it_costs()
    {
        var result = Build(
            new FakeProvider("Status", "Status", 10, "away"),
            new FakeProvider("Time", "Time", 90, "12:00"));

        Assert.Equal(4, result.SegmentLengths["Status"]);
        Assert.Equal(5, result.SegmentLengths["Time"]);
    }

    [Fact]
    public void A_dropped_segment_still_reports_what_it_asked_for()
    {
        var result = Build(
            new FakeProvider("Status", "Status", 10, new string('s', 200)),
            new FakeProvider("Time", "Time", 90, "12:00"));

        // Time never made the line, but a dimmed tile still has to say what it wanted.
        Assert.Equal(5, result.SegmentLengths["Time"]);

        // Status reports the clipped cost, which is what is on screen.
        Assert.Equal(Limit, result.SegmentLengths["Status"]);
    }

    [Fact]
    public void The_reported_costs_add_up_to_the_line_that_was_sent()
    {
        var result = Build(
            new FakeProvider("Status", "Status", 10, "away"),
            new FakeProvider("Window", "Window", 50, "notepad"),
            new FakeProvider("Time", "Time", 90, "12:00"));

        int segments = result.IncludedProviders.Sum(key => result.SegmentLengths[key]);
        int separators = Join.Length * (result.IncludedProviders.Count - 1);

        Assert.Equal(result.Length, segments + separators);
    }

    #region Harness

    private static OscBuildResult Build(params IOscProvider[] providers)
        => Build(_ => { }, providers);

    private static OscBuildResult Build(Action<AppSettings> configure, params IOscProvider[] providers)
    {
        var settings = new AppSettings { SeperateWithENTERS = false };
        configure(settings);

        var builder = new OscOutputBuilder(
            providers,
            new FakeAppState(),
            new IntegrationDisplayState(),
            new FakeSettingsProvider(settings),
            new ModuleFaultTracker());

        return builder.Build(allowExternalRefresh: false);
    }

    private sealed class FakeProvider : IOscProvider
    {
        private readonly string _text;

        public FakeProvider(string sortKey, string uiKey, int priority, string text)
        {
            SortKey = sortKey;
            UiKey = uiKey;
            Priority = priority;
            _text = text;
        }

        public string SortKey { get; }

        public string UiKey { get; }

        public int Priority { get; }

        public bool IsEnabledForCurrentMode(bool isVRRunning) => true;

        public OscSegment? TryBuild(OscBuildContext context) => new() { Text = _text };
    }

    private sealed class FakeSettingsProvider : ISettingsProvider<AppSettings>
    {
        public FakeSettingsProvider(AppSettings value) => Value = value;

        public AppSettings Value { get; }

        public void Save() { }

        public void FlushPendingSave() { }

        public void Reload() { }

        public event EventHandler SettingsChanged { add { } remove { } }
    }

    private sealed class FakeAppState : IAppState
    {
        public bool MasterSwitch { get; set; } = true;

        public bool IsVRRunning { get; set; }

        public bool BussyBoysMode { get; set; }

        public bool Egg_Dev { get; set; }

        public bool PulsoidAuthConnected { get; set; }

        public PulsoidAuthState PulsoidAuthState { get; set; }

        public int MainWindowBlurEffect { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
    }

    #endregion
}
