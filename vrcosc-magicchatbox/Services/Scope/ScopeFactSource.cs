using MagicChatbox.Scope;
using MagicChatbox.Vocabulary;
using MagicChatbox.Vrc;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Vrc;

namespace vrcosc_magicchatbox.Services.Scope;

public sealed class ScopeFactSource
{
    private readonly Func<AvatarIdentity> _identity;
    private readonly Func<AvatarSchemaSnapshot> _schema;
    private readonly Func<IReadOnlyList<AvatarSense>> _senses;
    private readonly Func<bool> _avatarSourceRunning;
    private readonly Func<VrcInstance> _instance;
    private readonly Func<string> _worldName;
    private readonly Func<int> _headcount;
    private readonly Func<bool> _worldSourceRunning;
    private readonly Func<bool> _isVr;
    private readonly Action<Action> _marshal;

    private readonly object _gate = new();
    private ScopeFacts _facts = ScopeFacts.Empty;
    private VrcCrowd _crowd = VrcCrowd.Unknown;
    private IReadOnlyList<AvatarGroup> _avatarGroups = Array.Empty<AvatarGroup>();
    private IReadOnlyList<WorldGroup> _worldGroups = Array.Empty<WorldGroup>();
    private int _publishing;

    public ScopeFactSource(
        Func<AvatarIdentity> identity,
        Func<AvatarSchemaSnapshot> schema,
        Func<IReadOnlyList<AvatarSense>> senses,
        Func<bool> avatarSourceRunning,
        Func<VrcInstance> instance,
        Func<string> worldName,
        Func<int> headcount,
        Func<bool> worldSourceRunning,
        Func<bool> isVr,
        Action<Action>? marshal = null)
    {
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        _senses = senses ?? throw new ArgumentNullException(nameof(senses));
        _avatarSourceRunning = avatarSourceRunning ?? throw new ArgumentNullException(nameof(avatarSourceRunning));
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        _worldName = worldName ?? throw new ArgumentNullException(nameof(worldName));
        _headcount = headcount ?? throw new ArgumentNullException(nameof(headcount));
        _worldSourceRunning = worldSourceRunning ?? throw new ArgumentNullException(nameof(worldSourceRunning));
        _isVr = isVr ?? throw new ArgumentNullException(nameof(isVr));
        _marshal = marshal ?? (action => action());
    }

    public event Action<ScopeFacts>? FactsChanged;

    public ScopeFacts Current
    {
        get { lock (_gate) return _facts; }
    }

    public void SetGroups(IReadOnlyList<AvatarGroup>? avatarGroups, IReadOnlyList<WorldGroup>? worldGroups)
    {
        lock (_gate)
        {
            _avatarGroups = avatarGroups ?? Array.Empty<AvatarGroup>();
            _worldGroups = worldGroups ?? Array.Empty<WorldGroup>();
        }

        Refresh();
    }

    public void Refresh()
    {
        if (Interlocked.Exchange(ref _publishing, 1) == 1)
            return;

        try
        {
            ScopeFacts next = Build();

            lock (_gate)
            {
                if (Same(_facts, next))
                    return;

                _facts = next;
            }

            _marshal(() => FactsChanged?.Invoke(next));
        }
        catch (Exception ex)
        {
            Classes.DataAndSecurity.Logging.WriteException(ex, MSGBox: false);
        }
        finally
        {
            Interlocked.Exchange(ref _publishing, 0);
        }
    }

    private ScopeFacts Build()
    {
        var cells = ImmutableDictionary.CreateBuilder<string, ScopeCell>(StringComparer.Ordinal);

        bool avatarLive = _avatarSourceRunning();
        AvatarIdentity identity = avatarLive ? _identity() : AvatarIdentity.Unknown;

        string avatarId = identity.Id ?? string.Empty;
        bool avatarKnown = avatarLive && avatarId.Length > 0;

        cells[ScopeFactKey.AvatarId.Value] = new ScopeCell(SignalValue.Text(avatarId), avatarKnown);
        cells[ScopeFactKey.AvatarName.Value] = new ScopeCell(
            SignalValue.Text(identity.DisplayName ?? string.Empty),
            avatarLive && identity.IsKnown);

        AddParameters(cells, avatarLive);

        bool worldLive = _worldSourceRunning();
        VrcInstance instance = worldLive ? _instance() : VrcInstance.None;
        bool worldKnown = worldLive && instance.IsKnown;

        cells[ScopeFactKey.WorldId.Value] = new ScopeCell(
            SignalValue.Text(VrcInstanceKey.BaseWorldId(instance.WorldId)),
            worldKnown);

        string worldName = worldLive ? _worldName() ?? string.Empty : string.Empty;
        cells[ScopeFactKey.WorldName.Value] = new ScopeCell(
            SignalValue.Text(worldName),
            worldKnown && worldName.Length > 0);

        cells[ScopeFactKey.InstanceType.Value] = new ScopeCell(
            SignalValue.Text(instance.AccessName),
            worldKnown && instance.Access != VrcInstanceAccess.Unknown);

        cells[ScopeFactKey.InstanceRegion.Value] = new ScopeCell(
            SignalValue.Text(instance.Region ?? string.Empty),
            worldKnown && !string.IsNullOrEmpty(instance.Region));

        cells[ScopeFactKey.InstanceCrowd.Value] = new ScopeCell(
            SignalValue.Text(VrcCrowdBuckets.NameOf(NextCrowd(worldKnown))),
            worldKnown);

        cells[ScopeFactKey.AppMode.Value] = ScopeCell.Live(SignalValue.Text(_isVr() ? "VR" : "Desktop"));

        return new ScopeFacts(
            cells.ToImmutable(),
            ResolveAvatarGroups(avatarKnown ? avatarId : string.Empty),
            ResolveWorldGroups(worldKnown ? instance.WorldId : string.Empty));
    }

    private void AddParameters(ImmutableDictionary<string, ScopeCell>.Builder cells, bool avatarLive)
    {
        if (!avatarLive)
            return;

        AvatarSchemaSnapshot schema = _schema();
        var declared = new HashSet<string>(StringComparer.Ordinal);
        foreach (VrcParameterDeclaration declaration in schema.Parameters)
            declared.Add(declaration.Name);

        foreach (AvatarSense sense in _senses())
        {
            string name = NameOf(sense.Key);
            if (name.Length == 0)
                continue;

            cells[ScopeFactKey.Parameter(name).Value] =
                new ScopeCell(ValueOf(sense), declared.Contains(name));
        }
    }

    private static string NameOf(string key) =>
        key.StartsWith(AvatarSenseStore.ParameterKeyPrefix, StringComparison.Ordinal)
            ? key[AvatarSenseStore.ParameterKeyPrefix.Length..]
            : string.Empty;

    private static SignalValue ValueOf(AvatarSense sense) => sense.Kind switch
    {
        SignalKind.Bool => SignalValue.Bool(sense.Value != 0d),
        SignalKind.Int => SignalValue.Int((long)sense.Value),
        SignalKind.Float => SignalValue.Float((float)sense.Value),
        SignalKind.Text => SignalValue.Text(sense.Text ?? string.Empty),
        _ => SignalValue.Float((float)sense.Value),
    };

    private VrcCrowd NextCrowd(bool worldKnown)
    {
        lock (_gate)
        {
            _crowd = worldKnown ? VrcCrowdBuckets.Classify(_crowd, _headcount()) : VrcCrowd.Unknown;
            return _crowd;
        }
    }

    private ImmutableHashSet<string> ResolveAvatarGroups(string avatarId)
    {
        if (avatarId.Length == 0)
            return ImmutableHashSet<string>.Empty;

        IReadOnlyList<AvatarGroup> groups;
        lock (_gate) groups = _avatarGroups;

        var matched = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        foreach (AvatarGroup group in groups)
        {
            foreach (string member in group.SafeAvatarIds)
            {
                if (string.Equals(member, avatarId, StringComparison.OrdinalIgnoreCase))
                {
                    matched.Add(group.Id);
                    break;
                }
            }
        }

        return matched.ToImmutable();
    }

    private ImmutableHashSet<string> ResolveWorldGroups(string worldId)
    {
        string folded = VrcInstanceKey.BaseWorldId(worldId);
        if (folded.Length == 0)
            return ImmutableHashSet<string>.Empty;

        IReadOnlyList<WorldGroup> groups;
        lock (_gate) groups = _worldGroups;

        var matched = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        foreach (WorldGroup group in groups)
        {
            foreach (string member in group.SafeWorldIds)
            {
                if (VrcInstanceKey.BaseWorldId(member) == folded)
                {
                    matched.Add(group.Id);
                    break;
                }
            }
        }

        return matched.ToImmutable();
    }

    private static bool Same(ScopeFacts left, ScopeFacts right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left.Cells.Count != right.Cells.Count
            || !left.AvatarGroupIds.SetEquals(right.AvatarGroupIds)
            || !left.WorldGroupIds.SetEquals(right.WorldGroupIds))
        {
            return false;
        }

        foreach (KeyValuePair<string, ScopeCell> entry in left.Cells)
        {
            if (!right.Cells.TryGetValue(entry.Key, out ScopeCell other) || other != entry.Value)
                return false;
        }

        return true;
    }
}
