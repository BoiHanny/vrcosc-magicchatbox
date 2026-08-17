using MagicChatbox.Scope;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Services.Scope;

public enum ScopeVerdict
{
    NoRule = 0,
    Allowed = 1,
    Blocked = 2,
    Settling = 3,
}

public sealed record ScopeDecision(
    string RuleId,
    string RuleName,
    ScopeTarget Target,
    ScopeVerdict Verdict,
    ScopeOutcome Outcome,
    ScopeBlock Block,
    string Sentence,
    DateTime AtUtc)
{
    public bool Permits => Verdict != ScopeVerdict.Blocked;
}

public sealed class ScopeRuntime
{
    private sealed class RuleState
    {
        public ScopeHold Hold;
        public bool Committed;
        public bool CommittedAllows;
        public bool Pending;
        public string GuardKey = string.Empty;
    }

    private readonly ISettingsProvider<ScopeSettings> _settingsProvider;
    private readonly ScopeFactSource _facts;
    private readonly Func<long> _ticks;
    private readonly Action<Action> _marshal;
    private readonly object _evaluating = new();

    private readonly object _gate = new();
    private readonly Dictionary<string, RuleState> _states = new(StringComparer.Ordinal);
    private IReadOnlyList<ScopeDecision> _decisions = Array.Empty<ScopeDecision>();
    private Dictionary<string, ScopeDecision> _byIntegration = new(StringComparer.OrdinalIgnoreCase);
    private ScopeDecision? _sending;

    public ScopeRuntime(
        ISettingsProvider<ScopeSettings> settingsProvider,
        ScopeFactSource facts,
        Func<long>? ticks = null,
        Action<Action>? marshal = null)
    {
        _settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
        _facts = facts ?? throw new ArgumentNullException(nameof(facts));
        _ticks = ticks ?? MonotonicTicks;
        _marshal = marshal ?? (action => action());

        _facts.FactsChanged += OnFactsChanged;
    }

    private static long MonotonicTicks() =>
        (long)(Stopwatch.GetTimestamp() * ((double)TimeSpan.TicksPerSecond / Stopwatch.Frequency));

    public event Action? DecisionsChanged;

    public IReadOnlyList<ScopeDecision> Decisions
    {
        get { lock (_gate) return _decisions; }
    }

    public bool IsUnsettled
    {
        get
        {
            lock (_gate)
            {
                return _decisions.Any(d => d.Verdict == ScopeVerdict.Settling)
                       || _states.Values.Any(s => s.Pending);
            }
        }
    }

    public ScopeSettings Settings => _settingsProvider.Value;

    public void SyncGroups() => _facts.SetGroups(Settings.AvatarGroups, Settings.WorldGroups);

    public bool PermitsIntegration(string tileKey)
    {
        if (string.IsNullOrEmpty(tileKey))
            return true;

        lock (_gate)
            return !_byIntegration.TryGetValue(tileKey, out ScopeDecision decision) || decision.Permits;
    }

    public bool PermitsSending()
    {
        lock (_gate) return _sending is null || _sending.Permits;
    }

    public bool TryDescribeIntegration(string tileKey, out ScopeDecision decision)
    {
        lock (_gate)
            return _byIntegration.TryGetValue(tileKey ?? string.Empty, out decision!);
    }

    public IReadOnlyList<ScopeDecision> PresetEdges()
    {
        lock (_gate)
            return _decisions.Where(d => d.Target.Kind == ScopeTargetKind.AvatarPreset).ToList();
    }

    public void Evaluate()
    {
        lock (_evaluating)
            EvaluateOnce();
    }

    private void EvaluateOnce()
    {
        ScopeSettings settings = Settings;
        ScopeFacts facts = _facts.Current;
        long now = _ticks();
        DateTime stamp = DateTime.UtcNow;

        var decisions = new List<ScopeDecision>();
        var byIntegration = new Dictionary<string, ScopeDecision>(StringComparer.OrdinalIgnoreCase);
        ScopeDecision? sending = null;

        if (settings.Enabled)
        {
            foreach (ScopeRule rule in settings.Rules.ToList())
            {
                if (rule is null || !rule.Enabled || rule.Target is null)
                    continue;

                ScopeOutcome outcome = ScopeEvaluator.Evaluate(rule.SafeWhen, facts, out ScopeBlock block);
                ScopeVerdict verdict = Settle(rule, outcome, now);

                var decision = new ScopeDecision(
                    rule.Id,
                    rule.Name,
                    rule.Target,
                    verdict,
                    outcome,
                    block,
                    ScopeMirror.Canonical(rule.SafeWhen),
                    stamp);
                decisions.Add(decision);

                switch (rule.Target.Kind)
                {
                    case ScopeTargetKind.Integration:
                        Tighten(byIntegration, rule.Target.Key, decision);
                        break;
                    case ScopeTargetKind.Sending:
                        sending = Tightest(sending, decision);
                        break;
                }
            }
        }

        lock (_gate)
        {
            _decisions = decisions;
            _byIntegration = byIntegration;
            _sending = sending;

            if (!settings.Enabled)
                _states.Clear();
            else
                Forget(_states, decisions);
        }

        _marshal(() => DecisionsChanged?.Invoke());
    }

    private static void Forget(Dictionary<string, RuleState> states, List<ScopeDecision> decided)
    {
        if (states.Count == decided.Count)
            return;

        var alive = new HashSet<string>(decided.Select(d => d.RuleId), StringComparer.Ordinal);

        foreach (string id in states.Keys.Where(id => !alive.Contains(id)).ToList())
            states.Remove(id);
    }

    private static void Tighten(Dictionary<string, ScopeDecision> map, string key, ScopeDecision decision)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        map[key] = map.TryGetValue(key, out ScopeDecision existing) ? Tightest(existing, decision)! : decision;
    }

    private static ScopeDecision? Tightest(ScopeDecision? left, ScopeDecision right)
    {
        if (left is null)
            return right;

        return left.Permits ? (right.Permits ? left : right) : left;
    }

    private ScopeVerdict Settle(ScopeRule rule, ScopeOutcome outcome, long now)
    {
        lock (_gate)
        {
            if (!_states.TryGetValue(rule.Id, out RuleState? state))
            {
                state = new RuleState();
                _states[rule.Id] = state;
            }

            string guardKey = ScopeMirror.Canonical(rule.SafeWhen);
            if (!string.Equals(state.GuardKey, guardKey, StringComparison.Ordinal))
            {
                state.GuardKey = guardKey;
                state.Hold.Reset();
                state.Committed = false;
                state.CommittedAllows = false;
            }

            state.Hold.Observe(outcome, now);

            if (outcome == ScopeOutcome.Unknown)
            {
                state.Pending = false;

                if (state.Committed)
                    return state.CommittedAllows ? ScopeVerdict.Allowed : ScopeVerdict.Blocked;

                return rule.BlockWhileUnknown ? ScopeVerdict.Blocked : ScopeVerdict.Allowed;
            }

            if (!state.Hold.HasHeldFor(outcome, rule.Dwell, now))
            {
                state.Pending = true;

                if (state.Committed)
                    return state.CommittedAllows ? ScopeVerdict.Allowed : ScopeVerdict.Blocked;

                return ScopeVerdict.Settling;
            }

            state.Pending = false;
            state.Committed = true;
            state.CommittedAllows = outcome == ScopeOutcome.True;
            return state.CommittedAllows ? ScopeVerdict.Allowed : ScopeVerdict.Blocked;
        }
    }

    private void OnFactsChanged(ScopeFacts facts) => Evaluate();
}
