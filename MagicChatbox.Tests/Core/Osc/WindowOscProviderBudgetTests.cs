using System;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Osc;
using vrcosc_magicchatbox.Core.Osc.Providers;
using vrcosc_magicchatbox.Core.Osc.Text;
using vrcosc_magicchatbox.ViewModels.State;
using Xunit;

namespace MagicChatbox.Tests.Core.Osc;

/// <summary>
/// Window activity is the integration most able to swamp the line - the app name and the title are
/// both arbitrary text - and it used to hand over whatever it had and let the builder deal with it.
/// </summary>
public class WindowOscProviderBudgetTests
{
    private sealed class StubSettingsProvider<T>(T value) : ISettingsProvider<T> where T : class, new()
    {
        public T Value { get; } = value;
        public void Save() { }
        public void FlushPendingSave() { }
        public void Reload() { }
        public event EventHandler SettingsChanged { add { } remove { } }
    }

    private static (WindowOscProvider Provider, WindowActivitySettings Wa) Build(string focused)
    {
        var intgr = new IntegrationSettings { IntgrScanWindowActivity = true };
        var wa = new WindowActivitySettings { ShowFocusedApp = true };
        var chatStatus = new ChatStatusDisplayState { FocusedWindow = focused };

        return (
            new WindowOscProvider(
                new StubSettingsProvider<IntegrationSettings>(intgr),
                new StubSettingsProvider<WindowActivitySettings>(wa),
                chatStatus),
            wa);
    }

    private static OscBuildContext Context(string prefix = "", string suffix = "", params string[] collected)
        => new()
        {
            Prefix = prefix,
            Suffix = suffix,
            Separator = OscGlyphs.SegmentJoin,
            CurrentSegments = collected,
            IsVRRunning = false,
        };

    [Fact]
    public void A_short_app_reads_exactly_as_it_always_did()
    {
        var (provider, wa) = Build("'Firefox' (Inbox)");

        var segment = provider.TryBuild(Context());

        Assert.NotNull(segment);
        Assert.Equal($"{wa.DesktopTitle} {wa.DesktopFocusTitle} 'Firefox' (Inbox)", segment!.Text);
    }

    [Fact]
    public void A_title_longer_than_the_line_is_cut_to_what_is_left_of_it()
    {
        var (provider, _) = Build($"'Firefox' ({new string('a', 400)})");

        var segment = provider.TryBuild(Context());

        Assert.NotNull(segment);
        Assert.True(segment!.Text.Length <= OscBuildContext.MaxOscLength, $"segment was {segment.Text.Length}");
        Assert.EndsWith(OscGlyphs.Ellipsis, segment.Text);
    }

    [Fact]
    public void What_is_already_on_the_line_and_the_separator_are_both_paid_for()
    {
        string existing = new('e', 100);
        var (provider, _) = Build($"'Firefox' ({new string('a', 400)})");

        var segment = provider.TryBuild(Context(collected: existing));

        Assert.NotNull(segment);
        int wholeLine = existing.Length + OscGlyphs.SegmentJoin.Length + segment!.Text.Length;
        Assert.True(wholeLine <= OscBuildContext.MaxOscLength, $"the line came to {wholeLine}");
    }

    [Fact]
    public void The_prefix_and_suffix_come_out_of_the_budget_too()
    {
        var (provider, _) = Build($"'Firefox' ({new string('a', 400)})");

        var segment = provider.TryBuild(Context(prefix: "[[[[[", suffix: "]]]]]"));

        Assert.NotNull(segment);
        Assert.True(segment!.Text.Length <= OscBuildContext.MaxOscLength - 10, $"segment was {segment.Text.Length}");
    }

    [Fact]
    public void Under_pressure_the_focus_word_goes_before_the_app_name_does()
    {
        // The reader wants to know which app. "focussing in" is the first thing worth losing.
        var (provider, wa) = Build(new string('a', 140));

        var segment = provider.TryBuild(Context());

        Assert.NotNull(segment);
        Assert.DoesNotContain(wa.DesktopFocusTitle, segment!.Text);
        Assert.StartsWith(wa.DesktopTitle, segment.Text);
    }

    [Fact]
    public void With_almost_no_room_left_the_heading_survives_on_its_own()
    {
        var (provider, wa) = Build("'Firefox' (Inbox)");

        var segment = provider.TryBuild(Context(collected: new string('e', 131)));

        Assert.NotNull(segment);
        Assert.Equal(wa.DesktopTitle, segment!.Text);
    }

    [Fact]
    public void With_no_room_at_all_the_segment_stands_down_rather_than_overshooting()
    {
        var (provider, _) = Build("'Firefox' (Inbox)");

        Assert.Null(provider.TryBuild(Context(collected: new string('e', 144))));
    }

    [Fact]
    public void Hiding_the_focused_app_leaves_the_heading_and_nothing_else()
    {
        var (provider, wa) = Build("'Firefox' (Inbox)");
        wa.ShowFocusedApp = false;

        var segment = provider.TryBuild(Context());

        Assert.NotNull(segment);
        Assert.Equal(wa.DesktopTitle, segment!.Text);
    }

    [Fact]
    public void The_segment_never_comes_out_with_an_edge_space()
    {
        var (provider, wa) = Build("'Firefox'");
        wa.DesktopFocusTitle = "  ";

        var segment = provider.TryBuild(Context());

        Assert.NotNull(segment);
        Assert.Equal(segment!.Text.Trim(), segment.Text);
        Assert.DoesNotContain("  ", segment.Text);
    }
}
