using System.Collections.Frozen;
using System.Collections.Immutable;
using MagicChatbox.Vocabulary;

namespace MagicChatbox.Kernel;

/// <summary>
/// Every descriptor, layered by <see cref="DescriptorSource"/> rank, resolved to one effective
/// descriptor per key.
/// </summary>
/// <remarks>
/// <b>Flat and keyed by <see cref="SignalKey"/>, with a source rank.</b> That flatness is a deliberate
/// simplification of v2's 610-line registry, and it has one consequence worth stating out loud because
/// a fence downstream depends on it: <b>the registry has no notion of which source owns a key.</b> A
/// <see cref="DescriptorSource.User"/> upsert can install a descriptor over a key an observe-path
/// source declared, and no registration-time guard can see that. This is why D7's text fence is a
/// runtime branch in <see cref="SignalStore.Observe"/> and not a registration check.
/// <para>
/// Conflicts are <b>rejected</b>, not logged. v2 only logged them
/// (<c>DescriptorConflictDetector.cs</c>), which is why v2 has keys whose declared type disagrees with
/// their received type in production.
/// </para>
/// <para>
/// <see cref="Effective"/> is a <see cref="FrozenDictionary{TKey,TValue}"/> rebuilt on change and read
/// roughly 3,000 times a second on the ingress path. <see cref="BeginBatch"/> collapses an avatar load
/// into one rebuild instead of one per parameter.
/// </para>
/// </remarks>
public sealed class DescriptorRegistry
{
    /// <summary>How many layers a key can hold — one per <see cref="DescriptorSource"/>.</summary>
    /// <remarks>
    /// Public because a caller that installs layers also has to remove them, and iterating the ranks is
    /// the only way to remove every layer it wrote without guessing at the enum's shape.
    /// </remarks>
    public const int LayerCount = (int)DescriptorSource.User + 1;

    private readonly Lock _gate = new();
    private readonly Dictionary<SignalKey, SignalDescriptor?[]> _layers = new();
    private readonly IOccurrenceRecorder? _recorder;

    private FrozenDictionary<SignalKey, SignalDescriptor> _effective =
        FrozenDictionary<SignalKey, SignalDescriptor>.Empty;

    private int _batchDepth;
    private bool _dirty;
    private int _version;

    /// <summary>
    /// Creates a registry. The recorder, when supplied, receives a <c>DescriptorRejected</c>
    /// occurrence for every illegal declaration, so a module that misdeclares a key shows up on the
    /// Audit screen instead of in a log nobody reads.
    /// </summary>
    public DescriptorRegistry(IOccurrenceRecorder? recorder = null) => _recorder = recorder;

    /// <summary>
    /// The resolved descriptor per key: the highest-ranked layer that declared one.
    /// </summary>
    /// <remarks>
    /// Read on the hot path. The reference is swapped whole on rebuild, so a reader either sees the
    /// old table or the new one and never a half-built one.
    /// </remarks>
    public FrozenDictionary<SignalKey, SignalDescriptor> Effective => Volatile.Read(ref _effective);

    /// <summary>
    /// Increments whenever <see cref="Effective"/> is rebuilt.
    /// </summary>
    /// <remarks>
    /// The store watches this to know when to rebuild its staleness scan table. A version counter
    /// rather than an event, because an event would let a subscriber run arbitrary code on whatever
    /// thread happened to load an avatar.
    /// </remarks>
    public int Version => Volatile.Read(ref _version);

    /// <summary>Number of keys with at least one declared layer.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _layers.Count;
            }
        }
    }

    /// <summary>
    /// Declares or replaces one layer's descriptor for a key.
    /// </summary>
    /// <remarks>
    /// Hard-fails, returning <c>Applied = false</c> and emitting a <c>DescriptorRejected</c>
    /// occurrence, on:
    /// <list type="bullet">
    /// <item><description>
    /// <see cref="Temperament.Continuous"/> on a <c>Bool</c> or <c>Text</c> descriptor — a momentary
    /// pulse must never be coalescible.
    /// </description></item>
    /// <item><description>
    /// a <see cref="SignalKind"/> that disagrees with an equal-or-higher-ranked layer — a hostile or
    /// careless module must not be able to redeclare a key's type and poison coercion for everyone.
    /// </description></item>
    /// </list>
    /// A lower-ranked layer disagreeing on kind is <i>also</i> rejected rather than stored-and-ignored:
    /// storing it would mean that removing the higher layer silently changes the key's type later.
    /// </remarks>
    public UpsertResult Upsert(DescriptorSource source, in SignalDescriptor descriptor)
    {
        if (descriptor.Temperament == Temperament.Continuous
            && descriptor.Kind is SignalKind.Bool or SignalKind.Text)
        {
            return Reject(
                descriptor.Key,
                ReasonCode.KindMismatch,
                $"Temperament.Continuous is legal only on Float and Int; '{descriptor.Key}' is {descriptor.Kind}.");
        }

        lock (_gate)
        {
            if (_layers.TryGetValue(descriptor.Key, out var layers))
            {
                for (var rank = LayerCount - 1; rank >= 0; rank--)
                {
                    // A source may correct its own declaration; it may not contradict anyone else's.
                    if (rank == (int)source || layers[rank] is not { } existing)
                    {
                        continue;
                    }

                    if (existing.Kind != descriptor.Kind)
                    {
                        return Reject(
                            descriptor.Key,
                            ReasonCode.KindMismatch,
                            $"'{descriptor.Key}' is already declared {existing.Kind} by " +
                            $"{(DescriptorSource)rank}; {source} declared {descriptor.Kind}.");
                    }
                }
            }
            else
            {
                layers = new SignalDescriptor?[LayerCount];
                _layers[descriptor.Key] = layers;
            }

            layers[(int)source] = descriptor;
            var winner = WinnerOf(layers);
            MarkDirty();

            return new UpsertResult(winner == source, winner, winner == source ? ReasonCode.Ok : ReasonCode.NoContent);
        }
    }

    /// <summary>Removes one layer's declaration, promoting whatever was underneath it.</summary>
    public bool Remove(DescriptorSource source, SignalKey key)
    {
        lock (_gate)
        {
            if (!_layers.TryGetValue(key, out var layers) || layers[(int)source] is null)
            {
                return false;
            }

            layers[(int)source] = null;
            if (Array.TrueForAll(layers, d => d is null))
            {
                _layers.Remove(key);
            }

            MarkDirty();
            return true;
        }
    }

    /// <summary>The effective descriptor for a key, if any layer declared one.</summary>
    public bool TryGet(SignalKey key, out SignalDescriptor descriptor) =>
        Effective.TryGetValue(key, out descriptor);

    /// <summary>
    /// Which layer's declaration is the one <see cref="Effective"/> is currently serving for a key.
    /// </summary>
    /// <remarks>
    /// A catalog has to be able to say "the avatar's uploaded config claims this, the running client
    /// never advertised it" — <see cref="DescriptorSource.AvatarConfig"/> against
    /// <see cref="DescriptorSource.OscQuery"/> is the difference between a parameter that will answer a
    /// write and one that will not, and <see cref="Effective"/> throws the rank away when it resolves.
    /// <para>
    /// Deliberately a per-key lookup under the lock rather than a second frozen table built beside
    /// <see cref="Effective"/>: the rebuild is on the avatar-load path and pays for every read of the hot
    /// dictionary, while the only caller of this is a channel that is fetched on mount and never polled.
    /// Spending an allocation of ~700 entries per avatar load to save a lock on a once-per-session read
    /// is the wrong way round.
    /// </para>
    /// </remarks>
    public bool TryGetSource(SignalKey key, out DescriptorSource source)
    {
        lock (_gate)
        {
            if (_layers.TryGetValue(key, out var layers))
            {
                source = WinnerOf(layers);
                return true;
            }
        }

        source = DescriptorSource.Default;
        return false;
    }

    /// <summary>
    /// Suppresses rebuilds until disposed. An avatar load declares hundreds of parameters and should
    /// cost one <see cref="FrozenDictionary{TKey,TValue}"/> build, not hundreds.
    /// </summary>
    public IDisposable BeginBatch()
    {
        lock (_gate)
        {
            _batchDepth++;
        }

        return new BatchScope(this);
    }

    private static DescriptorSource WinnerOf(SignalDescriptor?[] layers)
    {
        for (var rank = LayerCount - 1; rank >= 0; rank--)
        {
            if (layers[rank] is not null)
            {
                return (DescriptorSource)rank;
            }
        }

        return DescriptorSource.Default;
    }

    private UpsertResult Reject(SignalKey key, ReasonCode reason, string detail)
    {
        _recorder?.Record(
            OccurrenceKind.DescriptorRejected,
            KernelActor.Kernel,
            Correlation.For("kernel.descriptor.upsert"),
            reason,
            detail);

        return new UpsertResult(false, DescriptorSource.Default, reason);
    }

    /// <summary>Caller holds <see cref="_gate"/>.</summary>
    private void MarkDirty()
    {
        if (_batchDepth > 0)
        {
            _dirty = true;
            return;
        }

        Rebuild();
    }

    /// <summary>Caller holds <see cref="_gate"/>.</summary>
    private void Rebuild()
    {
        var builder = new Dictionary<SignalKey, SignalDescriptor>(_layers.Count);
        foreach (var (key, layers) in _layers)
        {
            var winner = WinnerOf(layers);
            if (layers[(int)winner] is { } descriptor)
            {
                builder[key] = descriptor;
            }
        }

        Volatile.Write(ref _effective, builder.ToFrozenDictionary());
        Interlocked.Increment(ref _version);
        _dirty = false;
    }

    private void EndBatch()
    {
        lock (_gate)
        {
            if (--_batchDepth > 0 || !_dirty)
            {
                return;
            }

            Rebuild();
        }
    }

    private sealed class BatchScope(DescriptorRegistry owner) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            owner.EndBatch();
        }
    }
}
