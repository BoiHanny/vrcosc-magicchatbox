using System.Text;

namespace MagicChatbox.Vocabulary;

/// <summary>
/// A bounded intern table for <see cref="SignalKey"/>: the same address arriving 2,700 times a second
/// costs one string, once.
/// </summary>
/// <remarks>
/// The cap is a <b>memory</b> bound, not a functionality bound. Past it the table stops growing and
/// every subsequent key for an unseen address is parsed and allocated afresh — correct, equal, and
/// slower. There is deliberately no eviction policy, because eviction is a policy that can be got
/// wrong and a bound cannot.
/// <para>
/// D10: that degradation is invisible from the outside — the only symptom is GC pressure somebody
/// eventually profiles, a long way from the cause (one avatar with a wildcard parameter family).
/// <see cref="Stats"/> exists so the host can alarm on a sustained miss rate instead: above ~1% at
/// 2,700/sec that is 27 allocations a second that should be zero.
/// </para>
/// <para>
/// The table is an instance rather than a static so that a test can exercise cap behaviour at a
/// capacity of four instead of 4,096, and so a future per-store table needs no redesign.
/// <see cref="Shared"/> is what production uses.
/// </para>
/// </remarks>
public sealed class SignalKeyInternTable
{
    /// <summary>4,096 against roughly 250 real keys — headroom until one avatar changes that.</summary>
    /// <remarks>
    /// One avatar did change that. A real library of 154 avatars holds 6,362 distinct
    /// <c>avatar.param.*</c> keys, so a process-wide table with no eviction fills after a median of ~82
    /// avatar swaps — and because the oldest entries are never released, the avatar actually being worn
    /// can end up entirely absent from it. That is why the avatar ingress path uses its OWN table,
    /// replaced on every swap, rather than <see cref="Shared"/>. See <c>VrcAvatarIngress</c>.
    /// </remarks>
    public const int DefaultCapacity = 4096;

    /// <summary>Chars below this lower on the stack. Mirrors <see cref="SignalKey"/>'s own limit.</summary>
    private const int StackLoweringLimit = 256;

    private readonly Lock _gate = new();
    private readonly Dictionary<string, SignalKey> _entries;
    private readonly Dictionary<string, SignalKey>.AlternateLookup<ReadOnlySpan<char>> _lookup;

    private long _attempts;
    private long _misses;

    /// <summary>Creates a table with its own cap. Prefer <see cref="Shared"/> outside tests.</summary>
    public SignalKeyInternTable(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);

        Capacity = capacity;

        // OrdinalIgnoreCase is what makes an un-normalized span hit a normalized entry without
        // allocating the lowered string first. Safe only because the grammar is ASCII-only.
        _entries = new Dictionary<string, SignalKey>(StringComparer.OrdinalIgnoreCase);
        _lookup = _entries.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    /// <summary>The process-wide table. Every <see cref="SignalKey.Intern(ReadOnlySpan{char})"/> goes here.</summary>
    public static SignalKeyInternTable Shared { get; } = new(DefaultCapacity);

    /// <summary>The entry cap. Reaching it degrades allocation, never correctness.</summary>
    public int Capacity { get; }

    /// <summary>A point-in-time reading for the miss-rate alarm.</summary>
    public SignalKeyInternStats Stats
    {
        get
        {
            lock (_gate)
            {
                return new SignalKeyInternStats(_attempts, _misses, _entries.Count, Capacity);
            }
        }
    }

    /// <summary>Interns, or throws if the grammar rejects the input.</summary>
    /// <exception cref="FormatException">The input is not a valid signal key.</exception>
    public SignalKey Intern(ReadOnlySpan<char> raw)
    {
        if (!TryIntern(raw, out var key))
        {
            throw new FormatException($"'{raw}' is not a valid signal key.");
        }

        return key;
    }

    /// <summary>
    /// Interns without throwing. A hit costs one dictionary probe and allocates nothing; a miss costs
    /// a parse, one string, and a counter the host can alarm on.
    /// </summary>
    public bool TryIntern(ReadOnlySpan<char> raw, out SignalKey key)
    {
        var trimmed = raw.Trim();

        lock (_gate)
        {
            _attempts++;
            if (_lookup.TryGetValue(trimmed, out key))
            {
                return true;
            }

            _misses++;
        }

        if (!SignalKey.TryParse(trimmed, out key))
        {
            return false;
        }

        lock (_gate)
        {
            if (_entries.Count >= Capacity)
            {
                // Past the cap: the caller still gets a correct key, it just owns its own string.
                return true;
            }

            if (!_entries.TryAdd(key.Value, key))
            {
                // Another thread won the race. Hand back its instance so identity stays single-valued.
                key = _entries[key.Value];
            }

            return true;
        }
    }

    /// <summary>
    /// Interns from UTF-8 bytes, which is how an OSC address arrives — no intermediate string.
    /// </summary>
    /// <remarks>
    /// The instance counterpart of <see cref="SignalKey.TryInternUtf8"/>, which always uses
    /// <see cref="Shared"/>. A caller whose key population is bounded and short-lived — one avatar's
    /// parameters, say — wants its own table instead, so that population cannot accumulate in the
    /// process-wide one.
    /// </remarks>
    public bool TryInternUtf8(ReadOnlySpan<byte> raw, out SignalKey key)
    {
        var charCount = Encoding.UTF8.GetCharCount(raw);
        Span<char> chars = charCount <= StackLoweringLimit
            ? stackalloc char[StackLoweringLimit]
            : new char[charCount];

        var written = Encoding.UTF8.GetChars(raw, chars);
        return TryIntern(chars[..written], out key);
    }
}

/// <summary>
/// What the intern table is costing right now.
/// </summary>
/// <param name="Attempts">Every intern call, hit or miss.</param>
/// <param name="Misses">Calls not served from the table — each one allocated.</param>
/// <param name="Count">Entries currently interned.</param>
/// <param name="Capacity">The cap. <paramref name="Count"/> never exceeds it.</param>
/// <remarks>
/// A healthy process reaches a low, flat miss count within seconds of startup and then stops moving.
/// A miss rate that keeps climbing is the D10 symptom: either the cap is exhausted or something is
/// generating keys instead of naming them.
/// </remarks>
public readonly record struct SignalKeyInternStats(long Attempts, long Misses, int Count, int Capacity)
{
    /// <summary>Zero on an untouched table. Alarm above ~0.01 sustained.</summary>
    public double MissRate => Attempts == 0 ? 0d : (double)Misses / Attempts;

    /// <summary>True once the table has stopped growing and every unseen key allocates.</summary>
    public bool IsAtCapacity => Count >= Capacity;
}
