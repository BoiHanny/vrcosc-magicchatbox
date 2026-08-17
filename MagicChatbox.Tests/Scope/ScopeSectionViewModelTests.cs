using MagicChatbox.Scope;
using MagicChatbox.Vocabulary;
using System;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.ViewModels.Sections;
using Xunit;

namespace MagicChatbox.Tests.Scope;

// The editor is the only way a guard gets written -- nothing parses text into one -- so a guard that
// cannot survive a round trip through these view models is a guard nobody can author.
public class ScopeSectionViewModelTests
{
    private sealed class Provider<T> : ISettingsProvider<T> where T : class, new()
    {
        public Provider(T value) => Value = value;

        public T Value { get; }

        public int Saves { get; private set; }

        public event EventHandler SettingsChanged;

        public void Save()
        {
            Saves++;
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void FlushPendingSave() { }

        public void Reload() { }
    }

    private static (ScopeSectionViewModel Vm, ScopeSettings Settings) Build()
    {
        var settings = new ScopeSettings();
        var provider = new Provider<ScopeSettings>(settings);
        var facts = new vrcosc_magicchatbox.Services.Scope.ScopeFactSource(
            () => vrcosc_magicchatbox.Core.Vrc.AvatarIdentity.Unknown,
            () => vrcosc_magicchatbox.Core.Vrc.AvatarSchemaSnapshot.Empty,
            () => Array.Empty<vrcosc_magicchatbox.Core.Vrc.AvatarSense>(),
            () => false,
            () => MagicChatbox.Vrc.VrcInstance.None,
            () => string.Empty,
            () => 0,
            () => false,
            () => false);

        var runtime = new vrcosc_magicchatbox.Services.Scope.ScopeRuntime(provider, facts);

        return (new ScopeSectionViewModel(provider, runtime, new Lazy<IModuleHost>(() => null)), settings);
    }

    [Fact]
    public void A_ready_made_guard_arrives_switched_off()
    {
        // Nothing the user did not write may fire on its own, and adopting must be the moment they say yes.
        var (vm, settings) = Build();
        vm.SelectedReadyMade = vm.ReadyMade.First();

        vm.AddReadyMadeCommand.Execute(null);

        ScopeRule stored = Assert.Single(settings.Rules);
        Assert.False(stored.Enabled);
        Assert.Equal(vm.ReadyMade.First().Name, stored.Name);
        Assert.Single(vm.Rules);
    }

    [Fact]
    public void Adopting_the_same_ready_made_guard_twice_gives_two_of_them()
    {
        var (vm, settings) = Build();
        vm.SelectedReadyMade = vm.ReadyMade.First();

        vm.AddReadyMadeCommand.Execute(null);
        vm.AddReadyMadeCommand.Execute(null);

        Assert.Equal(2, settings.Rules.Count);
        Assert.Equal(2, settings.Rules.Select(r => r.Id).Distinct().Count());
    }

    [Fact]
    public void Editing_a_guard_writes_it_straight_back_to_settings()
    {
        var (vm, settings) = Build();
        vm.AddBlankRuleCommand.Execute(null);

        ScopeRuleRowViewModel row = vm.Rules.Single();
        row.Name = "Only while streaming";
        row.Enabled = true;
        row.Target = vm.Targets.First(t => t.Label == "Heart rate");
        row.Predicates.Single().Fact = vm.Facts.First(f => f.Key == ScopeFactKey.AvatarGroup);
        row.Predicates.Single().Operator = ScopePredicateRowViewModel.Operators.First(o => o.Op == ScopeOperator.InGroup);
        row.Predicates.Single().Text = "streaming";

        ScopeRule stored = settings.Rules.Single();

        Assert.Equal("Only while streaming", stored.Name);
        Assert.True(stored.Enabled);
        Assert.Equal(ScopeTargetKind.Integration, stored.Target.Kind);
        Assert.Equal("HeartRate", stored.Target.Key);
        Assert.Equal("avatar.group is streaming", ScopeMirror.Canonical(stored.SafeWhen));
    }

    [Fact]
    public void A_guard_that_cannot_be_saved_says_so_instead_of_being_refused()
    {
        // Membership only means something on a group key. The editor reports it rather than blocking the
        // edit, because a half-built guard is a normal state to be in while building one.
        var (vm, _) = Build();
        vm.AddBlankRuleCommand.Execute(null);

        ScopeRuleRowViewModel row = vm.Rules.Single();
        row.Predicates.Single().Operator = ScopePredicateRowViewModel.Operators.First(o => o.Op == ScopeOperator.InGroup);

        Assert.Contains("membership", row.Problems, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("on", SignalKind.Bool)]
    [InlineData("off", SignalKind.Bool)]
    [InlineData("42", SignalKind.Int)]
    [InlineData("0.5", SignalKind.Float)]
    [InlineData("Public", SignalKind.Text)]
    public void A_typed_value_becomes_the_kind_it_looks_like(string typed, SignalKind expected)
    {
        var (vm, settings) = Build();
        vm.AddBlankRuleCommand.Execute(null);

        vm.Rules.Single().Predicates.Single().Text = typed;

        Assert.Equal(expected, settings.Rules.Single().SafeWhen.SafePredicates.Single().Value.Kind);
    }

    [Fact]
    public void The_settling_time_is_kept_in_seconds_for_the_user_and_milliseconds_on_disk()
    {
        var (vm, settings) = Build();
        vm.AddBlankRuleCommand.Execute(null);

        vm.Rules.Single().DwellSeconds = 2.5;

        Assert.Equal(2500, settings.Rules.Single().DwellMs);
    }

    [Fact]
    public void Removing_a_guard_removes_it_from_disk_too()
    {
        var (vm, settings) = Build();
        vm.AddBlankRuleCommand.Execute(null);

        vm.Rules.Single().DeleteCommand.Execute(null);

        Assert.Empty(settings.Rules);
        Assert.Empty(vm.Rules);
    }

    [Fact]
    public void A_group_can_be_made_and_removed()
    {
        var (vm, settings) = Build();
        vm.NewAvatarGroupName = "Streaming";

        vm.AddAvatarGroupCommand.Execute(null);

        AvatarGroup group = Assert.Single(settings.AvatarGroups);
        Assert.Equal("Streaming", group.Name);
        Assert.Empty(group.SafeAvatarIds);

        vm.DeleteAvatarGroupCommand.Execute(group);
        Assert.Empty(settings.AvatarGroups);
    }

    [Fact]
    public void Adding_the_worn_avatar_with_nothing_worn_says_so_rather_than_adding_an_empty_id()
    {
        var (vm, settings) = Build();
        vm.AddAvatarGroupCommand.Execute(null);

        vm.AddWornAvatarToCommand.Execute(settings.AvatarGroups.Single());

        Assert.Empty(settings.AvatarGroups.Single().SafeAvatarIds);
        Assert.Contains("Put an avatar on", vm.Status);
    }

    [Fact]
    public void Adding_the_current_world_with_none_joined_says_so()
    {
        var (vm, settings) = Build();
        vm.AddWorldGroupCommand.Execute(null);

        vm.AddCurrentWorldToCommand.Execute(settings.WorldGroups.Single());

        Assert.Empty(settings.WorldGroups.Single().SafeWorldIds);
        Assert.Contains("Join a world", vm.Status);
    }

    [Fact]
    public void Switching_the_whole_system_off_persists()
    {
        var (vm, settings) = Build();

        vm.ScopeEnabled = false;

        Assert.False(settings.Enabled);
    }

    [Fact]
    public void Every_integration_can_be_guarded()
    {
        var (vm, _) = Build();

        foreach (IntegrationTile tile in IntegrationTileCatalog.Tiles)
            Assert.Contains(vm.Targets, t => t.Target.Kind == ScopeTargetKind.Integration && t.Target.Key == tile.Key);

        Assert.Contains(vm.Targets, t => t.Target.Kind == ScopeTargetKind.Sending);
    }

    [Fact]
    public void A_reload_shows_what_is_on_disk()
    {
        var (vm, settings) = Build();
        settings.Rules.Add(ScopeRule.For("r9", "From disk", ScopeTarget.Sending, ScopeGroup.Always));

        vm.Reload();

        Assert.Equal("From disk", vm.Rules.Single().Name);
    }
}
