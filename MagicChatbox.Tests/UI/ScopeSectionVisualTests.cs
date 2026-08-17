using MagicChatbox.Scope;
using MagicChatbox.Tests.TestDoubles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.Vrc;
using vrcosc_magicchatbox.Services.Scope;
using vrcosc_magicchatbox.UI.Pages.Sections;
using vrcosc_magicchatbox.ViewModels.Sections;
using Xunit;

namespace MagicChatbox.Tests.UI;

/// <summary>
/// The guard editor, built rather than trusted.
/// </summary>
/// <remarks>
/// The Avatar page's own visual tests construct their view model without this section, so its markup was
/// never touched by anything until somebody opened the page. That is how it shipped with a Foreground
/// bound through a DynamicResource key -- a thing WPF does not do -- and a remove button reaching one
/// ancestor too far, which is not an error at all, just a command that silently is not there.
/// </remarks>
public class ScopeSectionVisualTests
{
    [Fact]
    public void The_section_builds_with_a_guard_and_both_kinds_of_group_on_screen()
    {
        Exception? failure = WpfHost.RunInWindow(
            () => new ScopeSection { DataContext = Populated() },
            section => Assert.NotNull(section.DataContext));

        Assert.True(failure == null, "the guard editor did not build: " + failure);
    }

    [Fact]
    public void The_section_binds_only_to_members_that_exist()
    {
        IReadOnlyList<string> errors = [];

        Exception? failure = WpfHost.Run(() =>
        {
            using var scope = new BindingErrorScope();

            WpfHost.BuildInWindow(() => new ScopeSection { DataContext = Populated() }, _ => { });

            errors = scope.RealErrors;
        });

        Assert.True(failure == null, "the guard editor did not build: " + failure);
        Assert.True(errors.Count == 0, "binding failures:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
    }

    [Fact]
    public void The_editor_a_guard_opens_builds_too()
    {
        // The form is behind a disclosure, so nothing renders it until somebody asks. A template with no
        // item is a template with no test.
        IReadOnlyList<string> errors = [];

        Exception? failure = WpfHost.Run(() =>
        {
            using var scope = new BindingErrorScope();

            ScopeSectionViewModel vm = Populated();
            vm.Rules.Single().IsEditing = true;
            vm.ShowGroups = true;

            WpfHost.BuildInWindow(
                () => new ScopeSection { DataContext = vm },
                section =>
                {
                    var rules = (ItemsControl)section.FindName("ScopeRuleList");
                    var card = (FrameworkElement)rules.ItemTemplate.LoadContent();
                    card.DataContext = vm.Rules.Single();
                    Realise(card);

                    // Exact type: ComboBox derives from ItemsControl, and the card is full of them.
                    ItemsControl tests = Descendants(card)
                        .OfType<ItemsControl>()
                        .First(c => c.GetType() == typeof(ItemsControl));
                    var row = (FrameworkElement)tests.ItemTemplate.LoadContent();
                    row.DataContext = vm.Rules.Single().Predicates.First();
                    Realise(row);
                });

            errors = scope.RealErrors;
        });

        Assert.True(failure == null, "the guard editor did not build: " + failure);
        Assert.True(errors.Count == 0, "binding failures:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
    }

    private static void Realise(FrameworkElement element)
    {
        element.Measure(new Size(600, 400));
        element.Arrange(new Rect(0, 0, 600, 400));
        element.UpdateLayout();
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);

        for (int i = 0; i < count; i++)
        {
            DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            yield return child;

            foreach (DependencyObject nested in Descendants(child))
                yield return nested;
        }
    }

    private static ScopeSectionViewModel Populated()
    {
        var settings = new ScopeSettings();
        var provider = new StubSettingsProvider<ScopeSettings>(settings);

        var facts = new ScopeFactSource(
            () => AvatarIdentity.Unknown,
            () => AvatarSchemaSnapshot.Empty,
            () => Array.Empty<AvatarSense>(),
            () => false,
            () => MagicChatbox.Vrc.VrcInstance.None,
            () => string.Empty,
            () => 0,
            () => false,
            () => false);

        var runtime = new ScopeRuntime(provider, facts);
        var vm = new ScopeSectionViewModel(provider, runtime, new Lazy<IModuleHost>(() => new BridgelessModuleHost()));

        vm.NewAvatarGroupName = "Streaming";
        vm.AddAvatarGroupCommand.Execute(null);
        vm.NewWorldGroupName = "Muted";
        vm.AddWorldGroupCommand.Execute(null);

        vm.SelectedReadyMade = vm.ReadyMade.First();
        vm.AddReadyMadeCommand.Execute(null);

        ScopeRuleRowViewModel rule = vm.Rules.Single();
        rule.LiveVerdict = "holding it off";
        rule.Sentence = "the avatar is in Streaming";

        return vm;
    }
}
