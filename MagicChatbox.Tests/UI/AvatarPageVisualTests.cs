using MagicChatbox.Tests.TestDoubles;
using MagicChatbox.Vocabulary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.Vrc;
using vrcosc_magicchatbox.UI.Pages;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.Avatar;
using Xunit;

namespace MagicChatbox.Tests.UI;

/// <summary>
/// The Avatar page, built rather than trusted.
/// </summary>
/// <remarks>
/// This page shipped with a StaticResource pointing at "FontSizeHeader" when the token is
/// "FontSizeHeading". It compiled, 1749 tests passed, and it threw XamlParseException the first time a
/// person clicked the tab -- because a StaticResource is resolved when something is built from it and
/// not a moment sooner.
///
/// The widget templates are the part most likely to rot: they live inside a DataTemplate that WPF never
/// touches until a row of that shape exists. A template with no item is a template with no test.
/// </remarks>
[Collection(WpfCollection.Name)]
public class AvatarPageVisualTests
{
    [Fact]
    public void The_page_builds_with_every_widget_on_screen()
    {
        Exception? failure = WpfHost.RunInWindow(
            () => new AvatarPage { DataContext = PopulatedViewModel() },
            page => Assert.NotNull(page.FindName("AvatarParameterSearch")));

        Assert.True(failure == null, "the avatar page did not build: " + failure);
    }

    [Fact]
    public void The_page_binds_only_to_members_that_exist()
    {
        // A binding to a property nobody has does not throw; it renders nothing, which looks exactly
        // like a value that happens to be empty. WPF reports it, but only to a trace source, and this
        // is the thing listening.
        IReadOnlyList<string> errors = [];

        Exception? failure = WpfHost.Run(() =>
        {
            using var scope = new BindingErrorScope();

            WpfHost.BuildInWindow(() => new AvatarPage { DataContext = PopulatedViewModel() }, _ => { });

            errors = scope.RealErrors;
        });

        Assert.True(failure == null, "the avatar page did not build: " + failure);
        Assert.True(errors.Count == 0, "binding failures:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
    }

    [Theory]
    [InlineData("AvatarTabOverview")]
    [InlineData("AvatarTabControls")]
    [InlineData("AvatarTabLooks")]
    [InlineData("AvatarTabRules")]
    [InlineData("AvatarTabDiagnostics")]
    public void Every_tab_has_content_and_builds(string tab)
    {
        // A TabControl shows one tab and leaves the rest unmeasured, so four fifths of this page is
        // markup nothing renders until somebody clicks. Inline TabItem content is still constructed at
        // parse time, which is what makes it reachable here at all.
        Exception? failure = WpfHost.RunInWindow(
            () => new AvatarPage { DataContext = PopulatedViewModel() },
            page =>
            {
                var scroller = page.FindName(tab) as ScrollViewer;

                Assert.True(scroller != null, tab + " is not on the page");

                var panel = scroller.Content as Panel;

                Assert.True(panel != null, tab + " holds no panel");
                Assert.True(panel.Children.Count > 0, tab + " is empty");
            });

        Assert.True(failure == null, tab + " did not build: " + failure);
    }

    [Theory]
    [InlineData(AvatarWidget.Toggle, SignalKind.Bool, true)]
    [InlineData(AvatarWidget.Stepper, SignalKind.Int, true)]
    [InlineData(AvatarWidget.Slider, SignalKind.Float, true)]
    [InlineData(AvatarWidget.Meter, SignalKind.Float, false)]
    public void Every_widget_template_builds(AvatarWidget widget, SignalKind kind, bool writable)
    {
        // The rows sit two ItemsControls deep, and container generation is queued on the dispatcher
        // rather than driven by layout - so simply showing the page renders the groups and leaves the
        // rows for a message loop this host deliberately does not run. Reading the templates and
        // building them by hand covers the same markup and settles, which walking the tree did not.
        Exception? failure = WpfHost.RunInWindow(
            () => new AvatarPage { DataContext = PopulatedViewModel() },
            page =>
            {
                var groupList = (ItemsControl)page.FindName("AvatarGroupList");
                var group = (FrameworkElement)groupList.ItemTemplate.LoadContent();
                group.DataContext = new AvatarControlGroupViewModel("Group", "Group", []);
                Realise(group);

                ItemsControl rowList = Descendants(group).OfType<ItemsControl>().First();
                var row = (FrameworkElement)rowList.ItemTemplate.LoadContent();
                row.DataContext = Row("Group/Thing", "Thing", kind, widget, writable);
                Realise(row);
            });

        Assert.True(failure == null, $"the {widget} template did not build: " + failure);
    }

    private static void Realise(FrameworkElement element)
    {
        element.Measure(new Size(400, 200));
        element.Arrange(new Rect(0, 0, 400, 200));
        element.UpdateLayout();
    }

    private static AvatarPageViewModel PopulatedViewModel()
    {
        // Showing the window raises IsVisibleChanged, which calls Activate() and starts the refresh
        // timer. A host with no bridge is what makes that refresh a no-op, so the rows staged below
        // survive to be rendered instead of being cleared by a live rebuild against nothing.
        var vm = new AvatarPageViewModel(
            new StubSettingsProvider<VrcBridgeSettings>(),
            new StubSettingsProvider<IntegrationSettings>(),
            new StubSettingsProvider<AvatarPresetSettings>(),
            new Lazy<IModuleHost>(() => new BridgelessModuleHost()),
            new RecordingParameterSink(),
            StubConsentService.ApprovingAll());

        vm.AvatarName = "Test avatar";
        vm.ParameterSummary = "4 custom · 0 built-in · 3 you can drive";
        vm.RungMessage = "Reading your avatar";
        vm.SpeechText = "Not speaking";

        vm.Readiness.Add(new ReadinessRow("Heart rate", ReadinessState.Driving, "Driving", "6 of 6 found", 6, 6));
        vm.Readiness.Add(new ReadinessRow("Discord", ReadinessState.Waiting, "Waiting", "not in a voice channel", 5, 5));
        vm.Readiness.Add(new ReadinessRow("Camera flash", ReadinessState.Faulted, "Problem", "something went wrong", 0, 1));

        vm.Ecosystems = [.. EcosystemSignature.Markers.Take(3)];

        vm.Presets.Add(new AvatarPreset(
            "Club", "avtr_test", "Test avatar", DateTime.UtcNow,
            [new AvatarPresetValue("Toggles/Hat", SignalKind.Bool, 1)]));
        vm.PresetRefusals.Add(new PresetApplyRow(
            "Toggles/Gone", PresetOutcome.NotOnThisAvatar, SignalKind.Bool, 1, "Toggles/Gone"));
        vm.PresetStatus = "\"Club\": 1 to restore, 1 not on this avatar.";

        vm.SharedAcrossAvatars.Add(new SharedParameter("Go/Locomotion", 180, 0));
        vm.Globals.Add(new AvatarPresetValue("EyeTrackingActive", SignalKind.Bool, 1));
        vm.GlobalsStatus = "Set 1 of your 1 defaults on this avatar.";

        vm.ConfigChanges.Add(new ScopeStatusRow(
            "Heart rate while streaming",
            "Heart rate is held off — avatar.group is streaming",
            "✕"));
        vm.HasConfigChanges = true;

        vm.Layout = new LayoutReport(
            LayoutState.RenamedByVrcFury,
            0,
            "Renamed on install",
            "VRCFury renamed the controls, so VRChat sees VF12_MCB/Ctrl/Panic instead.",
            ["MCB/Ctrl/Panic", "MCB/Ctrl/Tts/Stop"]);

        vm.RecentlyChanged.Add(new AvatarSense("Sensors/Contact", SignalKind.Float, 0.9, "", DateTime.UtcNow));
        vm.HasUndrivableRecent = true;

        vm.PinnedRows.Add(Row("Toggles/Hat", "Hat", SignalKind.Bool, AvatarWidget.Toggle, writable: true));
        vm.PinnedRows[0].IsPinned = true;
        vm.HasPinnedRows = true;

        vm.RecentRows.Add(Row("Modes/Outfit", "Outfit", SignalKind.Int, AvatarWidget.Stepper, writable: true, value: 2));
        vm.HasRecentRows = true;

        vm.Groups.Add(new AvatarControlGroupViewModel(
            "Toggles",
            "Toggles",
            [Row("Toggles/Hat", "Hat", SignalKind.Bool, AvatarWidget.Toggle, writable: true)]));

        vm.Groups.Add(new AvatarControlGroupViewModel(
            "Modes",
            "Modes",
            [
                Row("Modes/Outfit", "Outfit", SignalKind.Int, AvatarWidget.Stepper, writable: true, value: 2),
                Row("Face/Blush", "Blush", SignalKind.Float, AvatarWidget.Slider, writable: true, value: 0.4),
                Row("Sensors/Contact", "Contact", SignalKind.Float, AvatarWidget.Meter, writable: false, value: 0.9),
            ]));

        return vm;
    }

    private static AvatarControlRowViewModel Row(
        string name, string leaf, SignalKind kind, AvatarWidget widget, bool writable, double value = 1)
        => new(
            new AvatarControlRow(name, leaf, kind, writable, widget, value, HasValue: true, IsBuiltIn: false),
            new RecordingParameterSink());

    private static IEnumerable<FrameworkElement> Descendants(DependencyObject root)
    {
        int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);

        for (int i = 0; i < count; i++)
        {
            DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);

            if (child is FrameworkElement element)
                yield return element;

            foreach (FrameworkElement nested in Descendants(child))
                yield return nested;
        }
    }


}
