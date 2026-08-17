using MagicChatbox.Scope;
using MagicChatbox.Vocabulary;
using System;
using System.IO;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.Vrc;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.Services.Scope;
using vrcosc_magicchatbox.ViewModels.Sections;
using Xunit;

namespace MagicChatbox.Tests.Scope;

// Each of these pins something an adversarial review found after the first pass shipped green. They are
// grouped because they share one lesson: a guard that is wrong is worse than a guard that is absent,
// because the screen goes on saying it is working.
public class ScopeReviewRegressionTests
{
    private sealed class Provider<T> : ISettingsProvider<T> where T : class, new()
    {
        public Provider(T value) => Value = value;

        public T Value { get; }

        public event EventHandler SettingsChanged;

        public void Save() => SettingsChanged?.Invoke(this, EventArgs.Empty);

        public void FlushPendingSave() { }

        public void Reload() { }
    }

    private sealed class Environment : IEnvironmentService
    {
        public Environment(string path) => DataPath = path;

        public string DataPath { get; }

        public string LogPath => Path.Combine(DataPath, "logs");

        public string VrcPath => DataPath;

        public void SetCustomProfile(int profileNumber) => throw new NotSupportedException();
    }

    private static ScopeFactSource DarkFacts() => new(
        () => AvatarIdentity.Unknown,
        () => AvatarSchemaSnapshot.Empty,
        () => Array.Empty<AvatarSense>(),
        () => false,
        () => MagicChatbox.Vrc.VrcInstance.None,
        () => string.Empty,
        () => 0,
        () => false,
        () => false);

    private sealed class Worn
    {
        public AvatarIdentity Identity = AvatarIdentity.Unknown;

        public ScopeFactSource Source() => new(
            () => Identity,
            () => AvatarSchemaSnapshot.Empty,
            () => Array.Empty<AvatarSense>(),
            () => true,
            () => MagicChatbox.Vrc.VrcInstance.None,
            () => string.Empty,
            () => 0,
            () => false,
            () => false);
    }

    private static AvatarIdentity Avatar(string id) => new(id, id, AvatarIdSource.AvatarChange);

    [Fact]
    public void Reading_the_settings_from_inside_their_own_changed_event_does_not_recurse_forever()
    {
        // A subscriber that reads Value from the handler used to re-enter the lazy load, because _loaded
        // was set only after Reload() had already raised the event. It stack-overflowed the app on
        // startup, which is uncatchable.
        string dir = Path.Combine(Path.GetTempPath(), "mcb-scope-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        try
        {
            using var provider = new JsonSettingsProvider<ScopeSettings>(new Environment(dir));
            int reads = 0;

            provider.SettingsChanged += (_, _) =>
            {
                reads++;
                _ = provider.Value;
            };

            ScopeSettings settings = provider.Value;

            Assert.NotNull(settings);
            Assert.Equal(1, reads);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public void A_guard_naming_a_parameter_the_picker_has_never_heard_of_keeps_its_key()
    {
        // The picker only lists the worn avatar's parameters. A ready-made guard over MCB/Cfg/* used to
        // fall back to the first entry -- avatar.id -- the moment somebody switched it on, silently
        // rewriting the rule into one that could never fire.
        var settings = new ScopeSettings();
        var provider = new Provider<ScopeSettings>(settings);
        var runtime = new ScopeRuntime(provider, DarkFacts());
        var vm = new ScopeSectionViewModel(provider, runtime, new Lazy<IModuleHost>(() => null));

        vm.SelectedReadyMade = vm.ReadyMade.First(g => g.Name.Contains("heart rate", StringComparison.OrdinalIgnoreCase));
        vm.AddReadyMadeCommand.Execute(null);

        ScopeRuleRowViewModel row = vm.Rules.Single();
        row.Enabled = true;

        ScopeRule stored = settings.Rules.Single();

        Assert.True(stored.Enabled);
        Assert.All(
            stored.SafeWhen.SafePredicates,
            p => Assert.StartsWith(ScopeFactKey.ParameterPrefix, p.Key.Value, StringComparison.Ordinal));
    }

    [Fact]
    public void A_group_test_stores_the_group_id_while_the_user_only_ever_sees_the_name()
    {
        // Membership is resolved against ids. Storing the typed name made every group guard fail to
        // match while reading back as a correct sentence.
        var settings = new ScopeSettings();
        var provider = new Provider<ScopeSettings>(settings);
        var runtime = new ScopeRuntime(provider, DarkFacts());
        var vm = new ScopeSectionViewModel(provider, runtime, new Lazy<IModuleHost>(() => null));

        vm.NewAvatarGroupName = "Streaming";
        vm.AddAvatarGroupCommand.Execute(null);
        string groupId = settings.AvatarGroups.Single().Id;

        vm.AddBlankRuleCommand.Execute(null);
        ScopeRuleRowViewModel row = vm.Rules.Single();
        ScopePredicateRowViewModel test = row.Predicates.Single();
        test.Fact = vm.Facts.First(f => f.Key == ScopeFactKey.AvatarGroup);
        test.Operator = ScopePredicateRowViewModel.Operators.First(o => o.Op == ScopeOperator.InGroup);
        test.Text = "Streaming";

        ScopePredicate stored = settings.Rules.Single().SafeWhen.SafePredicates.Single();

        Assert.Equal(groupId, stored.ValueText);
        Assert.NotEqual("Streaming", stored.ValueText);
        Assert.Contains("Streaming", row.Sentence, StringComparison.Ordinal);
        Assert.Empty(row.Problems);
    }

    [Fact]
    public void A_group_test_naming_nothing_that_exists_says_so()
    {
        var settings = new ScopeSettings();
        var provider = new Provider<ScopeSettings>(settings);
        var runtime = new ScopeRuntime(provider, DarkFacts());
        var vm = new ScopeSectionViewModel(provider, runtime, new Lazy<IModuleHost>(() => null));

        vm.AddBlankRuleCommand.Execute(null);
        ScopeRuleRowViewModel row = vm.Rules.Single();
        ScopePredicateRowViewModel test = row.Predicates.Single();
        test.Fact = vm.Facts.First(f => f.Key == ScopeFactKey.AvatarGroup);
        test.Operator = ScopePredicateRowViewModel.Operators.First(o => o.Op == ScopeOperator.InGroup);
        test.Text = "Nothing I made";

        Assert.Contains("group that exists", row.Problems, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Digits_typed_against_an_avatar_id_stay_text()
    {
        // "1234" against a Text fact used to become an Int, and comparing Int to Text is Unknown -- so
        // the guard was permanently unreadable rather than simply false.
        var avatarId = new ScopeFactChoice("The avatar", ScopeFactKey.AvatarId, false);

        SignalValue value = ScopePredicateRowViewModel.ValueFor("1234", avatarId);

        Assert.Equal(SignalKind.Text, value.Kind);
    }

    [Fact]
    public void A_value_longer_than_a_cell_can_hold_is_trimmed_rather_than_thrown()
    {
        SignalValue value = ScopePredicateRowViewModel.ValueFor(new string('x', 4000), null);

        Assert.Equal(SignalKind.Text, value.Kind);
    }

    [Fact]
    public void The_value_box_stops_being_needed_the_moment_the_operator_stops_needing_one()
    {
        var settings = new ScopeSettings();
        var provider = new Provider<ScopeSettings>(settings);
        var runtime = new ScopeRuntime(provider, DarkFacts());
        var vm = new ScopeSectionViewModel(provider, runtime, new Lazy<IModuleHost>(() => null));

        vm.AddBlankRuleCommand.Execute(null);
        ScopePredicateRowViewModel test = vm.Rules.Single().Predicates.Single();

        bool raised = false;
        test.PropertyChanged += (_, e) => raised |= e.PropertyName == nameof(ScopePredicateRowViewModel.NeedsValue);

        test.Operator = ScopePredicateRowViewModel.Operators.First(o => o.Op == ScopeOperator.IsLive);

        Assert.True(raised);
        Assert.False(test.NeedsValue);
    }

    [Fact]
    public void Editing_a_guard_takes_effect_at_once_rather_than_inheriting_the_old_sentence()
    {
        // An edit is somebody saying what they want, not a fact flickering, so there is nothing to damp.
        // What must not happen is the new sentence inheriting the commitment made about the old one.
        var settings = new ScopeSettings();
        var worn = new Worn { Identity = Avatar("avtr_one") };
        ScopeFactSource facts = worn.Source();
        var runtime = new ScopeRuntime(new Provider<ScopeSettings>(settings), facts, () => 0);

        settings.Rules.Add(ScopeRule.For("r1", "A", ScopeTarget.Sending, ScopeGroup.Always) with
        {
            Enabled = true,
            DwellMs = 60_000,
        });

        facts.Refresh();
        runtime.Evaluate();
        Assert.True(runtime.PermitsSending());

        settings.Rules[0] = settings.Rules[0] with
        {
            When = ScopeGroup.All(ScopePredicate.Is(ScopeFactKey.AvatarId, "avtr_other")),
        };

        runtime.Evaluate();

        Assert.False(runtime.PermitsSending());
    }

    [Fact]
    public void A_rule_waiting_out_its_dwell_keeps_the_runtime_unsettled_so_something_re_evaluates_it()
    {
        // Nothing re-evaluates on a timer unless IsUnsettled says to. It used to report only rules that
        // had never committed, so a committed rule whose answer flipped froze in its old decision until
        // some unrelated fact happened to move.
        var settings = new ScopeSettings();
        var worn = new Worn { Identity = Avatar("avtr_one") };
        ScopeFactSource facts = worn.Source();
        long clock = 0;
        var runtime = new ScopeRuntime(new Provider<ScopeSettings>(settings), facts, () => clock);

        settings.Rules.Add(
            ScopeRule.For("r1", "A", ScopeTarget.Sending,
                ScopeGroup.All(ScopePredicate.Is(ScopeFactKey.AvatarId, "avtr_one"))) with
            {
                Enabled = true,
                DwellMs = 2000,
            });

        facts.Refresh();
        runtime.Evaluate();
        Assert.True(runtime.PermitsSending());
        Assert.False(runtime.IsUnsettled);

        worn.Identity = Avatar("avtr_other");
        facts.Refresh();
        runtime.Evaluate();

        Assert.True(runtime.PermitsSending());
        Assert.True(runtime.IsUnsettled);

        clock += TimeSpan.FromSeconds(3).Ticks;
        runtime.Evaluate();

        Assert.False(runtime.IsUnsettled);
        Assert.False(runtime.PermitsSending());
    }

    [Fact]
    public void A_guard_told_to_stay_shut_while_unsure_does_not_swing_open_while_it_settles()
    {
        // The leak: blocked while Unknown, then the fact arrives and says no. The answer changed, so the
        // dwell restarts -- and the pre-commitment fallback used to permit, handing out a full dwell of
        // access to the one guard whose whole point is not doing that.
        var settings = new ScopeSettings();
        var worn = new Worn();
        ScopeFactSource facts = worn.Source();
        long clock = 0;
        var runtime = new ScopeRuntime(new Provider<ScopeSettings>(settings), facts, () => clock);

        settings.Rules.Add(
            ScopeRule.For("r1", "Quiet unless sure", ScopeTarget.Sending,
                ScopeGroup.All(ScopePredicate.Is(ScopeFactKey.AvatarId, "avtr_one"))) with
            {
                Enabled = true,
                DwellMs = 2000,
                BlockWhileUnknown = true,
            });

        facts.Refresh();
        runtime.Evaluate();
        Assert.False(runtime.PermitsSending());

        worn.Identity = Avatar("avtr_other");
        facts.Refresh();
        runtime.Evaluate();

        Assert.False(runtime.PermitsSending());

        clock += TimeSpan.FromSeconds(3).Ticks;
        runtime.Evaluate();

        Assert.False(runtime.PermitsSending());
    }

    [Fact]
    public void Lyrics_can_be_guarded_even_though_it_has_no_tile_of_its_own()
    {
        // It rides on the media integration but is a separate provider, so a guard over media never
        // reached it -- the track title went quiet and the lyric line of the same song did not.
        var settings = new ScopeSettings();
        var provider = new Provider<ScopeSettings>(settings);
        var runtime = new ScopeRuntime(provider, DarkFacts());
        var vm = new ScopeSectionViewModel(provider, runtime, new Lazy<IModuleHost>(() => null));

        Assert.Contains(vm.Targets, t => t.Target.Key == "Lyrics");
    }

    [Fact]
    public void An_any_of_guard_with_nothing_in_it_is_reported_rather_than_silently_holding_everything_off()
    {
        var rule = ScopeRule.For("r1", "A", ScopeTarget.Sending, ScopeGroup.Any());

        Assert.Contains(rule.Validate(), p => p.Code == ScopeProblemCode.NothingCanSatisfyIt);
    }

    [Fact]
    public void A_deleted_rule_does_not_leave_its_decision_behind()
    {
        var settings = new ScopeSettings();
        var runtime = new ScopeRuntime(new Provider<ScopeSettings>(settings), DarkFacts(), () => 0);

        settings.Rules.Add(ScopeRule.For("r1", "A", ScopeTarget.Sending, ScopeGroup.Any()) with
        {
            Enabled = true,
            DwellMs = 0,
        });

        runtime.Evaluate();
        Assert.False(runtime.PermitsSending());

        settings.Rules.Clear();
        runtime.Evaluate();

        Assert.True(runtime.PermitsSending());
        Assert.False(runtime.IsUnsettled);
    }

    [Fact]
    public void The_dwell_is_measured_in_the_same_units_the_rule_states_it_in()
    {
        // Stopwatch timestamps are in Stopwatch.Frequency units, not TimeSpan's 100 ns ticks. They match
        // on most Windows machines and do not have to.
        var settings = new ScopeSettings();
        var worn = new Worn { Identity = Avatar("avtr_one") };
        ScopeFactSource facts = worn.Source();
        long clock = 0;
        var runtime = new ScopeRuntime(new Provider<ScopeSettings>(settings), facts, () => clock);

        settings.Rules.Add(
            ScopeRule.For("r1", "A", ScopeTarget.Sending,
                ScopeGroup.All(ScopePredicate.Is(ScopeFactKey.AvatarId, "avtr_one"))) with
            {
                Enabled = true,
                DwellMs = 1000,
            });

        facts.Refresh();
        runtime.Evaluate();

        worn.Identity = Avatar("avtr_other");
        facts.Refresh();
        runtime.Evaluate();

        clock += TimeSpan.FromMilliseconds(999).Ticks;
        runtime.Evaluate();
        Assert.True(runtime.PermitsSending());

        clock += TimeSpan.FromMilliseconds(2).Ticks;
        runtime.Evaluate();
        Assert.False(runtime.PermitsSending());
    }
}
