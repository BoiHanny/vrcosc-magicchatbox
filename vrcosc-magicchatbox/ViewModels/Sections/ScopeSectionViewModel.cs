using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MagicChatbox.Scope;
using MagicChatbox.Vocabulary;
using MagicChatbox.Vrc;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.Vrc;
using vrcosc_magicchatbox.Services.Scope;

namespace vrcosc_magicchatbox.ViewModels.Sections;

public sealed record ScopeTargetChoice(string Label, ScopeTarget Target);

public sealed record ScopeFactChoice(string Label, ScopeFactKey Key, bool IsGroup);

public sealed record ScopeOperatorChoice(string Label, ScopeOperator Op);

public partial class ScopePredicateRowViewModel : ObservableObject
{
    private readonly Action _changed;

    public ScopePredicateRowViewModel(ScopePredicate predicate, IReadOnlyList<ScopeFactChoice> facts, Action changed)
    {
        _changed = changed;
        Facts = facts;

        _fact = facts.FirstOrDefault(f => f.Key == predicate.Key) ?? facts.FirstOrDefault();
        _operator = Operators.FirstOrDefault(o => o.Op == predicate.Op) ?? Operators[0];
        _text = predicate.Value.Kind == SignalKind.Text
            ? predicate.Value.AsText()
            : Describe(predicate.Value);
    }

    public IReadOnlyList<ScopeFactChoice> Facts { get; }

    public static IReadOnlyList<ScopeOperatorChoice> Operators { get; } = new ScopeOperatorChoice[]
    {
        new("is", ScopeOperator.Equals),
        new("is not", ScopeOperator.NotEquals),
        new("contains", ScopeOperator.Contains),
        new("is above", ScopeOperator.GreaterThan),
        new("is below", ScopeOperator.LessThan),
        new("is known", ScopeOperator.IsLive),
        new("is not known", ScopeOperator.IsNotLive),
        new("is one of", ScopeOperator.InGroup),
    };

    [ObservableProperty] private ScopeFactChoice _fact;
    [ObservableProperty] private ScopeOperatorChoice _operator;
    [ObservableProperty] private string _text = string.Empty;

    partial void OnFactChanged(ScopeFactChoice value) => _changed();

    partial void OnOperatorChanged(ScopeOperatorChoice value) => _changed();

    partial void OnTextChanged(string value) => _changed();

    public bool NeedsValue => Operator.Op is not (ScopeOperator.IsLive or ScopeOperator.IsNotLive);

    public ScopePredicate ToPredicate() =>
        new(Fact?.Key ?? ScopeFactKey.AvatarId, Operator.Op, ValueFor(Text));

    private static SignalValue ValueFor(string text)
    {
        string trimmed = (text ?? string.Empty).Trim();

        if (bool.TryParse(trimmed, out bool flag))
            return SignalValue.Bool(flag);

        if (string.Equals(trimmed, "on", StringComparison.OrdinalIgnoreCase))
            return SignalValue.Bool(true);

        if (string.Equals(trimmed, "off", StringComparison.OrdinalIgnoreCase))
            return SignalValue.Bool(false);

        if (long.TryParse(trimmed, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out long whole))
        {
            return SignalValue.Int(whole);
        }

        if (double.TryParse(trimmed, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double number))
        {
            return SignalValue.Float((float)number);
        }

        return SignalValue.Text(trimmed);
    }

    private static string Describe(SignalValue value) => value.Kind switch
    {
        SignalKind.Bool => value.AsBool() ? "on" : "off",
        SignalKind.Int => value.AsInt().ToString(System.Globalization.CultureInfo.InvariantCulture),
        SignalKind.Float => value.AsFloat().ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
        _ => value.AsText(),
    };
}

public partial class ScopeRuleRowViewModel : ObservableObject
{
    private readonly ScopeSectionViewModel _owner;
    private bool _loading;

    public ScopeRuleRowViewModel(ScopeRule rule, ScopeSectionViewModel owner)
    {
        _owner = owner;
        Id = rule.Id;

        _loading = true;
        _name = rule.Name;
        _enabled = rule.Enabled;
        _dwellSeconds = Math.Round(rule.DwellMs / 1000d, 1);
        _blockWhileUnknown = rule.BlockWhileUnknown;
        _target = owner.Targets.FirstOrDefault(t => t.Target == rule.Target) ?? owner.Targets[0];
        _join = rule.SafeWhen.Join;

        foreach (ScopePredicate predicate in rule.SafeWhen.SafePredicates)
            Predicates.Add(new ScopePredicateRowViewModel(predicate, owner.Facts, Changed));

        _loading = false;
    }

    public string Id { get; }

    public ObservableCollection<ScopePredicateRowViewModel> Predicates { get; } = new();

    public IReadOnlyList<ScopeTargetChoice> Targets => _owner.Targets;

    public static IReadOnlyList<ScopeJoin> Joins { get; } = new[] { ScopeJoin.All, ScopeJoin.Any, ScopeJoin.None };

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private double _dwellSeconds;
    [ObservableProperty] private bool _blockWhileUnknown;
    [ObservableProperty] private ScopeTargetChoice _target;
    [ObservableProperty] private ScopeJoin _join;
    [ObservableProperty] private string _sentence = string.Empty;
    [ObservableProperty] private string _liveVerdict = string.Empty;
    [ObservableProperty] private string _problems = string.Empty;

    partial void OnNameChanged(string value) => Changed();

    partial void OnEnabledChanged(bool value) => Changed();

    partial void OnDwellSecondsChanged(double value) => Changed();

    partial void OnBlockWhileUnknownChanged(bool value) => Changed();

    partial void OnTargetChanged(ScopeTargetChoice value) => Changed();

    partial void OnJoinChanged(ScopeJoin value) => Changed();

    [RelayCommand]
    private void AddTest()
    {
        Predicates.Add(new ScopePredicateRowViewModel(
            ScopePredicate.Is(ScopeFactKey.AvatarId, string.Empty), _owner.Facts, Changed));

        Changed();
    }

    [RelayCommand]
    private void RemoveTest(ScopePredicateRowViewModel row)
    {
        if (row != null && Predicates.Remove(row))
            Changed();
    }

    [RelayCommand]
    private void Delete() => _owner.DeleteRule(this);

    [RelayCommand]
    private void UseCurrentAvatar() => _owner.FillWithCurrentAvatar(this);

    [RelayCommand]
    private void UseCurrentWorld() => _owner.FillWithCurrentWorld(this);

    internal void Changed()
    {
        if (_loading)
            return;

        _owner.Save(this);
    }

    internal void Load(ScopeRule rule)
    {
        _loading = true;

        Name = rule.Name;
        Enabled = rule.Enabled;
        DwellSeconds = Math.Round(rule.DwellMs / 1000d, 1);
        BlockWhileUnknown = rule.BlockWhileUnknown;
        Target = _owner.Targets.FirstOrDefault(t => t.Target == rule.Target) ?? _owner.Targets[0];
        Join = rule.SafeWhen.Join;

        Predicates.Clear();
        foreach (ScopePredicate predicate in rule.SafeWhen.SafePredicates)
            Predicates.Add(new ScopePredicateRowViewModel(predicate, _owner.Facts, Changed));

        _loading = false;
    }

    public ScopeRule ToRule() => new(
        Id,
        Name,
        Enabled,
        Target?.Target ?? ScopeTarget.Sending,
        new ScopeGroup(
            Join,
            [.. Predicates.Select(p => p.ToPredicate())],
            System.Collections.Immutable.ImmutableArray<ScopeGroup>.Empty),
        (int)Math.Round(Math.Clamp(DwellSeconds, 0, ScopeRule.MaxDwellMs / 1000d) * 1000),
        string.Empty)
    {
        BlockWhileUnknown = BlockWhileUnknown,
    };
}

public partial class ScopeSectionViewModel : ObservableObject
{
    private readonly ISettingsProvider<ScopeSettings> _settingsProvider;
    private readonly ScopeRuntime _runtime;
    private readonly Lazy<IModuleHost> _modules;
    private readonly LocalAvatarDataReader _library;
    private bool _suppressReload;

    public ScopeSectionViewModel(
        ISettingsProvider<ScopeSettings> settingsProvider,
        ScopeRuntime runtime,
        Lazy<IModuleHost> modules,
        LocalAvatarDataReader library = null)
    {
        _settingsProvider = settingsProvider;
        _runtime = runtime;
        _modules = modules;
        _library = library ?? new LocalAvatarDataReader();

        Targets = BuildTargets();
        Facts = BuildFacts();

        Reload();

        if (_runtime != null)
            _runtime.DecisionsChanged += RefreshVerdicts;
    }

    public ScopeSettings Settings => _settingsProvider.Value;

    public IReadOnlyList<ScopeTargetChoice> Targets { get; }

    public IReadOnlyList<ScopeFactChoice> Facts { get; }

    public IReadOnlyList<ScopeStarterGuard> ReadyMade => ScopeStarterGuards.All;

    public ObservableCollection<ScopeRuleRowViewModel> Rules { get; } = new();

    public ObservableCollection<AvatarGroup> AvatarGroups { get; } = new();

    public ObservableCollection<WorldGroup> WorldGroups { get; } = new();

    [ObservableProperty] private ScopeStarterGuard _selectedReadyMade;
    [ObservableProperty] private string _newAvatarGroupName = string.Empty;
    [ObservableProperty] private string _newWorldGroupName = string.Empty;
    [ObservableProperty] private string _status = string.Empty;

    public bool ScopeEnabled
    {
        get => Settings.Enabled;
        set
        {
            if (Settings.Enabled == value)
                return;

            Settings.Enabled = value;
            _settingsProvider.Save();
            OnPropertyChanged();
            _runtime?.Evaluate();
        }
    }

    private static IReadOnlyList<ScopeTargetChoice> BuildTargets()
    {
        var choices = new List<ScopeTargetChoice>
        {
            new("Everything this app sends", ScopeTarget.Sending),
        };

        foreach (IntegrationTile tile in IntegrationTileCatalog.Tiles)
            choices.Add(new ScopeTargetChoice(tile.DisplayName, ScopeTarget.Integration(tile.Key)));

        return choices;
    }

    private IReadOnlyList<ScopeFactChoice> BuildFacts()
    {
        var choices = new List<ScopeFactChoice>
        {
            new("The avatar I'm wearing", ScopeFactKey.AvatarId, false),
            new("The avatar's name", ScopeFactKey.AvatarName, false),
            new("The avatar is in group", ScopeFactKey.AvatarGroup, true),
            new("The world I'm in", ScopeFactKey.WorldId, false),
            new("The world's name", ScopeFactKey.WorldName, false),
            new("The world is in group", ScopeFactKey.WorldGroup, true),
            new("Who can join", ScopeFactKey.InstanceType, false),
            new("Region", ScopeFactKey.InstanceRegion, false),
            new("How busy it is", ScopeFactKey.InstanceCrowd, false),
            new("VR or desktop", ScopeFactKey.AppMode, false),
        };

        foreach (VrcParameterDeclaration declaration in CurrentSchema().Parameters.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
            choices.Add(new ScopeFactChoice(declaration.Name, ScopeFactKey.Parameter(declaration.Name), false));

        return choices;
    }

    private AvatarSchemaSnapshot CurrentSchema()
    {
        try
        {
            return _modules?.Value?.VrcBridge?.Schema.Current ?? AvatarSchemaSnapshot.Empty;
        }
        catch
        {
            return AvatarSchemaSnapshot.Empty;
        }
    }

    public void Reload()
    {
        Rules.Clear();
        foreach (ScopeRule rule in Settings.Rules.Where(r => r != null))
            Rules.Add(new ScopeRuleRowViewModel(rule, this));

        AvatarGroups.Clear();
        foreach (AvatarGroup group in Settings.AvatarGroups.Where(g => g != null))
            AvatarGroups.Add(group);

        WorldGroups.Clear();
        foreach (WorldGroup group in Settings.WorldGroups.Where(g => g != null))
            WorldGroups.Add(group);

        RefreshVerdicts();
    }

    internal void Save(ScopeRuleRowViewModel row)
    {
        if (_suppressReload)
            return;

        ScopeRule updated = row.ToRule();
        IReadOnlyList<ScopeProblem> problems = updated.Validate();

        row.Sentence = ScopeMirror.Canonical(updated.SafeWhen);
        row.Problems = problems.Count == 0
            ? string.Empty
            : string.Join("  ", problems.Select(p => p.Detail).Distinct());

        for (int i = 0; i < Settings.Rules.Count; i++)
        {
            if (Settings.Rules[i]?.Id == row.Id)
            {
                Settings.Rules[i] = updated;
                _settingsProvider.Save();
                _runtime?.SyncGroups();
                _runtime?.Evaluate();
                return;
            }
        }
    }

    internal void DeleteRule(ScopeRuleRowViewModel row)
    {
        for (int i = Settings.Rules.Count - 1; i >= 0; i--)
        {
            if (Settings.Rules[i]?.Id == row.Id)
                Settings.Rules.RemoveAt(i);
        }

        Rules.Remove(row);
        _settingsProvider.Save();
        _runtime?.Evaluate();
        Status = $"Removed \"{row.Name}\".";
    }

    [RelayCommand]
    private void AddReadyMade()
    {
        ScopeStarterGuard chosen = SelectedReadyMade ?? ReadyMade.FirstOrDefault();
        if (chosen == null)
            return;

        ScopeRule rule = chosen.Adopt(ScopeStarterGuards.NextId(Settings.Rules));
        Settings.Rules.Add(rule);
        _settingsProvider.Save();

        Rules.Add(new ScopeRuleRowViewModel(rule, this));
        RefreshVerdicts();

        Status = $"Added \"{rule.Name}\". It is off until you switch it on.";
    }

    [RelayCommand]
    private void AddBlankRule()
    {
        var rule = ScopeRule.For(
            ScopeStarterGuards.NextId(Settings.Rules),
            "New guard",
            ScopeTarget.Sending,
            ScopeGroup.All(ScopePredicate.Is(ScopeFactKey.AvatarId, string.Empty)));

        Settings.Rules.Add(rule);
        _settingsProvider.Save();

        Rules.Add(new ScopeRuleRowViewModel(rule, this));
        Status = "Added a guard. It is off until you switch it on.";
    }

    [RelayCommand]
    private void AddAvatarGroup()
    {
        string name = string.IsNullOrWhiteSpace(NewAvatarGroupName) ? "Avatars" : NewAvatarGroupName.Trim();
        var group = new AvatarGroup(NextGroupId(Settings.AvatarGroups.Select(g => g?.Id)), name, []);

        Settings.AvatarGroups.Add(group);
        AvatarGroups.Add(group);
        NewAvatarGroupName = string.Empty;

        Persist();
        Status = $"Added the group \"{name}\".";
    }

    [RelayCommand]
    private void AddWorldGroup()
    {
        string name = string.IsNullOrWhiteSpace(NewWorldGroupName) ? "Worlds" : NewWorldGroupName.Trim();
        var group = new WorldGroup(NextGroupId(Settings.WorldGroups.Select(g => g?.Id)), name, []);

        Settings.WorldGroups.Add(group);
        WorldGroups.Add(group);
        NewWorldGroupName = string.Empty;

        Persist();
        Status = $"Added the group \"{name}\".";
    }

    [RelayCommand]
    private void AddWornAvatarTo(AvatarGroup group)
    {
        string avatarId = CurrentAvatarId();

        if (group == null || avatarId.Length == 0)
        {
            Status = "Put an avatar on first — this adds the one you are wearing.";
            return;
        }

        if (group.SafeAvatarIds.Any(id => string.Equals(id, avatarId, StringComparison.OrdinalIgnoreCase)))
        {
            Status = "That avatar is already in this group.";
            return;
        }

        Replace(Settings.AvatarGroups, AvatarGroups, group, group with { AvatarIds = group.SafeAvatarIds.Add(avatarId) });
        Persist();
        Status = $"Added the avatar you are wearing to \"{group.Name}\".";
    }

    [RelayCommand]
    private void AddCurrentWorldTo(WorldGroup group)
    {
        string worldId = CurrentWorldId();

        if (group == null || worldId.Length == 0)
        {
            Status = "Join a world first — this adds the one you are in.";
            return;
        }

        if (group.SafeWorldIds.Any(id => MagicChatbox.Vrc.VrcInstanceKey.BaseWorldId(id) == worldId))
        {
            Status = "That world is already in this group.";
            return;
        }

        Replace(Settings.WorldGroups, WorldGroups, group, group with { WorldIds = group.SafeWorldIds.Add(worldId) });
        Persist();
        Status = $"Added the world you are in to \"{group.Name}\".";
    }

    [RelayCommand]
    private void DeleteAvatarGroup(AvatarGroup group)
    {
        if (group == null)
            return;

        Settings.AvatarGroups.Remove(group);
        AvatarGroups.Remove(group);
        Persist();
    }

    [RelayCommand]
    private void DeleteWorldGroup(WorldGroup group)
    {
        if (group == null)
            return;

        Settings.WorldGroups.Remove(group);
        WorldGroups.Remove(group);
        Persist();
    }

    internal void FillWithCurrentAvatar(ScopeRuleRowViewModel row)
    {
        string avatarId = CurrentAvatarId();

        if (row == null || avatarId.Length == 0)
        {
            Status = "Put an avatar on first.";
            return;
        }

        ScopePredicateRowViewModel target = row.Predicates
            .FirstOrDefault(p => p.Fact?.Key == ScopeFactKey.AvatarId);

        if (target == null)
        {
            row.AddTestCommand.Execute(null);
            target = row.Predicates.Last();
            target.Fact = Facts.First(f => f.Key == ScopeFactKey.AvatarId);
        }

        target.Text = avatarId;
        Status = "Filled in the avatar you are wearing.";
    }

    internal void FillWithCurrentWorld(ScopeRuleRowViewModel row)
    {
        string worldId = CurrentWorldId();

        if (row == null || worldId.Length == 0)
        {
            Status = "Join a world first.";
            return;
        }

        ScopePredicateRowViewModel target = row.Predicates
            .FirstOrDefault(p => p.Fact?.Key == ScopeFactKey.WorldId);

        if (target == null)
        {
            row.AddTestCommand.Execute(null);
            target = row.Predicates.Last();
            target.Fact = Facts.First(f => f.Key == ScopeFactKey.WorldId);
        }

        target.Text = worldId;
        Status = "Filled in the world you are in.";
    }

    private void RefreshVerdicts()
    {
        if (_runtime == null)
            return;

        Dictionary<string, ScopeDecision> byId = _runtime.Decisions
            .GroupBy(d => d.RuleId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        foreach (ScopeRuleRowViewModel row in Rules)
        {
            if (!byId.TryGetValue(row.Id, out ScopeDecision decision))
            {
                row.LiveVerdict = row.Enabled ? "waiting" : "off";
                continue;
            }

            row.LiveVerdict = decision.Verdict switch
            {
                ScopeVerdict.Blocked => "holding it off",
                ScopeVerdict.Settling => "settling",
                _ => "allowing it",
            };

            row.Sentence = decision.Sentence;
        }
    }

    private string CurrentAvatarId()
    {
        try
        {
            return _modules?.Value?.VrcBridge?.CurrentAvatarId ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private string CurrentWorldId()
    {
        try
        {
            return MagicChatbox.Vrc.VrcInstanceKey.BaseWorldId(
                _modules?.Value?.VrcRadar?.CurrentInstance.WorldId ?? string.Empty);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void Replace<T>(
        ObservableCollection<T> stored, ObservableCollection<T> shown, T before, T after)
    {
        for (int i = 0; i < stored.Count; i++)
        {
            if (Equals(stored[i], before))
            {
                stored[i] = after;
                break;
            }
        }

        for (int i = 0; i < shown.Count; i++)
        {
            if (Equals(shown[i], before))
            {
                shown[i] = after;
                break;
            }
        }
    }

    private void Persist()
    {
        _settingsProvider.Save();
        _runtime?.SyncGroups();
        _runtime?.Evaluate();
    }

    private static string NextGroupId(IEnumerable<string> existing)
    {
        var used = new HashSet<string>(existing.Where(id => id != null), StringComparer.Ordinal);

        for (int n = 1; ; n++)
        {
            string candidate = "group-" + n.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (used.Add(candidate))
                return candidate;
        }
    }
}
