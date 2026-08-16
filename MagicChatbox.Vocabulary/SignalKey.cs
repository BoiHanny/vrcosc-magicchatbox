using System.Text;

namespace MagicChatbox.Vocabulary;

/// <summary>
/// The name of one fact: two or more dot-separated segments whose first segment is a
/// <see cref="SignalNamespace"/>.
/// </summary>
/// <remarks>
/// The grammar has two tiers, and normalization remains trim plus <c>ToLowerInvariant</c> and nothing
/// else. A key that does not match is <b>rejected</b>, never repaired: v2 repaired keys (stripping
/// slashes, inserting dots) and the repaired form silently diverged from the form the descriptor was
/// registered under, so a fact existed under two names and the composer read the empty one.
/// <para>
/// <b>First segment:</b> <c>[a-z0-9]+</c>, and it must equal one of the five reserved namespace words.
/// <b>Tail segments:</b> printable ASCII except <c>ReservedInTail</c>, with <c>.</c> separating them and
/// no segment allowed to be empty.
/// </para>
/// <para>
/// <b>The tail is wide on purpose.</b> Tail segments carry names this application does not author — a
/// VRChat avatar parameter is addressed <c>/avatar/parameters/Toggles/Ring Left</c>, and an
/// alphanumerics-only tail rejected the slash. Measured against one real 155-avatar library that
/// silently dropped 58% of address suffixes, and 89% on the worst single avatar: a key that cannot be
/// spelled is a parameter that does not exist. Repairing those names was considered and rejected — it
/// recovers 37 more occurrences out of 21,717, removes no collisions, and reintroduces exactly the
/// two-spellings divergence described above.
/// </para>
/// <para>
/// Only ASCII is accepted, in both tiers. Enforcing that before lowering matters: Unicode simple case
/// folding maps U+0131 (dotless i) onto <c>I</c>, so a case-insensitive comparison against the reserved
/// word <c>input</c> would otherwise admit a sixth namespace through the back door. The first segment is
/// where that comparison happens, and it keeps the narrow charset.
/// </para>
/// <para>
/// <b>A settings key is not one of these, however alike they read.</b>
/// <c>[SettingsKey("app.appearance")]</c> in <c>MagicChatbox.Persistence</c> is also lowercase and
/// dot-separated, and it is a different type with a different grammar living in a different store — a
/// settings key never appears in <c>SignalStore</c> and a signal key is never a row in <c>settings</c>.
/// The behavioural difference is the one this type's first paragraph is about: a signal key is trimmed
/// and lowered because it arrives from outside this repository, spelled by VRChat or by an avatar
/// author, so refusing the input would mean refusing a fact that genuinely exists. A settings key is
/// authored here, in an attribute, by us — so there is nothing to be tolerant of, and it is rejected
/// rather than repaired. Written on both types because the collision is in the ear rather than in the
/// compiler, and only one of the two readings ends with somebody's preferences filed under a name
/// nothing will ask for again.
/// </para>
/// <para>
/// <c>default(SignalKey)</c> is the "no key" sentinel. Its <see cref="Value"/> is empty and
/// <see cref="IsDefault"/> is true; every <c>TryParse</c> failure leaves it there.
/// </para>
/// </remarks>
public readonly struct SignalKey : IEquatable<SignalKey>
{
    /// <summary>Keys shorter than this lower on the stack; longer ones rent nothing and allocate once.</summary>
    private const int StackLoweringLimit = 256;

    private readonly string? _value;

    private SignalKey(string value, SignalNamespace signalNamespace)
    {
        _value = value;
        Namespace = signalNamespace;
        Hash = string.GetHashCode(value, StringComparison.Ordinal);
    }

    /// <summary>Normalized, invariant-lower, validated. Empty only on <c>default(SignalKey)</c>.</summary>
    public string Value => _value ?? string.Empty;

    /// <summary>Always the first segment. One of exactly five values, forever.</summary>
    public SignalNamespace Namespace { get; }

    /// <summary>
    /// Ordinal hash computed once at construction. Exposed because the store selects a stripe from it
    /// and must not pay for a rehash on every write.
    /// </summary>
    public int Hash { get; }

    /// <summary>True for the "no key" sentinel — the value every failed parse leaves behind.</summary>
    public bool IsDefault => _value is null;

    public static bool operator ==(SignalKey left, SignalKey right) => left.Equals(right);

    public static bool operator !=(SignalKey left, SignalKey right) => !left.Equals(right);

    /// <summary>
    /// Normalizes and validates. Returns false and leaves <paramref name="key"/> at its default for
    /// anything the grammar does not accept, including a first segment that is not a reserved word.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> raw, out SignalKey key)
    {
        key = default;

        var trimmed = raw.Trim();
        if (!TryValidate(trimmed, out var signalNamespace))
        {
            return false;
        }

        key = new SignalKey(ToLowered(trimmed), signalNamespace);
        return true;
    }

    /// <summary>
    /// Interns through the shared table. Throws on a key the grammar rejects, because a caller that
    /// interns is naming a key it authored; boundary code that parses attacker- or config-supplied
    /// text uses <see cref="TryIntern"/> or <see cref="TryParse"/> instead.
    /// </summary>
    public static SignalKey Intern(ReadOnlySpan<char> raw) => SignalKeyInternTable.Shared.Intern(raw);

    /// <summary>Interns through the shared table without throwing.</summary>
    public static bool TryIntern(ReadOnlySpan<char> raw, out SignalKey key) =>
        SignalKeyInternTable.Shared.TryIntern(raw, out key);

    /// <summary>
    /// Interns from UTF-8 bytes, which is how an OSC address arrives — no intermediate string.
    /// </summary>
    public static SignalKey InternUtf8(ReadOnlySpan<byte> raw)
    {
        if (!TryInternUtf8(raw, out var key))
        {
            throw new FormatException("Not a valid signal key.");
        }

        return key;
    }

    /// <summary>Interns from UTF-8 bytes without throwing.</summary>
    public static bool TryInternUtf8(ReadOnlySpan<byte> raw, out SignalKey key)
    {
        var charCount = Encoding.UTF8.GetCharCount(raw);
        Span<char> chars = charCount <= StackLoweringLimit
            ? stackalloc char[StackLoweringLimit]
            : new char[charCount];

        var written = Encoding.UTF8.GetChars(raw, chars);
        return TryIntern(chars[..written], out key);
    }

    /// <summary>
    /// Validates a source id against <c>^[a-z0-9][a-z0-9-]*$</c> after the same normalization keys get.
    /// </summary>
    /// <remarks>
    /// Stricter than a key's tail segment — no underscore, no leading hyphen — because the source id is
    /// simultaneously the key prefix, the grant prefix and the eviction prefix for <c>Unregister</c>.
    /// Anything that could be mistaken for a segment boundary makes all three ambiguous.
    /// <para>
    /// Registration itself lives in the store, not here; this is the shared predicate so the store and
    /// the module loader cannot drift apart on what a source id is.
    /// </para>
    /// </remarks>
    public static bool IsValidSourceId(ReadOnlySpan<char> sourceId)
    {
        var trimmed = sourceId.Trim();
        if (trimmed.IsEmpty)
        {
            return false;
        }

        if (!IsLowerableAlphanumeric(trimmed[0]))
        {
            return false;
        }

        foreach (var c in trimmed[1..])
        {
            if (!IsLowerableAlphanumeric(c) && c != '-')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Validates and returns the canonical form. Registration must store this rather than what the
    /// manifest said, or a source declared as <c>MyModule</c> would never match the lowered keys it
    /// goes on to produce.
    /// </summary>
    public static bool TryNormalizeSourceId(ReadOnlySpan<char> raw, out string normalized)
    {
        if (!IsValidSourceId(raw))
        {
            normalized = string.Empty;
            return false;
        }

        normalized = ToLowered(raw.Trim());
        return true;
    }

    public bool Equals(SignalKey other) =>
        Hash == other.Hash && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is SignalKey other && Equals(other);

    public override int GetHashCode() => Hash;

    public override string ToString() => Value;

    /// <summary>
    /// The one place the grammar is expressed. Case-insensitive by construction: the ASCII gate runs
    /// first, so accepting <c>A-Z</c> here is equivalent to validating the lowered form.
    /// </summary>
    private static bool TryValidate(ReadOnlySpan<char> s, out SignalNamespace signalNamespace)
    {
        signalNamespace = default;

        if (s.IsEmpty)
        {
            return false;
        }

        var dot = s.IndexOf('.');
        if (dot <= 0)
        {
            return false;
        }

        // The first segment keeps the narrow charset. This is where the ASCII/dotless-i argument in the
        // type's remarks actually bites — it is the segment compared against the five reserved words —
        // and it never sees the wider set below.
        foreach (var c in s[..dot])
        {
            if (!IsLowerableAlphanumeric(c))
            {
                return false;
            }
        }

        // The tail is wide, because it carries names this application does not author. A VRChat avatar
        // parameter address is /avatar/parameters/Toggles/Ring Left, and the narrow charset rejected the
        // slash — which silently dropped 58% of the address suffixes on one real 155-avatar library, and
        // 89% on the worst single avatar. A key that cannot be spelled is a parameter that does not
        // exist, so the charset is the feature here, not a guard.
        foreach (var c in s[(dot + 1)..])
        {
            if (!IsLegalInTail(c))
            {
                return false;
            }
        }

        // The five reserved words are all [a-z]+, so matching them also enforces the first segment's
        // narrower charset — no underscore, no hyphen, no leading digit rule to state separately.
        if (!TryMatchNamespace(s[..dot], out signalNamespace))
        {
            return false;
        }

        var segmentLength = 0;
        foreach (var c in s[(dot + 1)..])
        {
            if (c == '.')
            {
                if (segmentLength == 0)
                {
                    return false;
                }

                segmentLength = 0;
                continue;
            }

            segmentLength++;
        }

        return segmentLength > 0;
    }

    /// <summary>
    /// The characters a tail segment may not contain, each excluded for a stated reason.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a reserved list, not an allow list, because the names in a tail segment are authored by
    /// other people — avatar creators, module authors — and enumerating what they are permitted to type
    /// would be a losing game. The question is only "what would this character break here".
    /// </para>
    /// <list type="bullet">
    /// <item><c>*</c> — <c>KeyPattern</c>'s wildcard. Its own remarks note that a <c>*</c> anywhere but
    /// the final position is safe to treat literally <i>only because no key can contain one</i>.</item>
    /// <item><c>space | ! : @ ' " { }</c> — the composer's token lexer. A key has to be spellable inside
    /// <c>{…}</c>, <c>{a|b|'literal'}</c>, <c>{key!upper!max:24}</c> and <c>{key@variant}</c>, and the
    /// conditional form is whitespace-lexed. A key containing any of these could be registered and then
    /// never referenced from a template.</item>
    /// </list>
    /// <para>
    /// Excluding them costs 37 occurrences in 21,717 measured against a real avatar library. If the
    /// composer ever quotes or escapes token paths, this set can shrink — it is coupled to that grammar
    /// on purpose, and COMPOSER-SPEC §5 records the coupling from the other side.
    /// </para>
    /// </remarks>
    private const string ReservedInTail = " |!:@'\"{}*";

    /// <summary>Printable ASCII, minus <see cref="ReservedInTail"/>. <c>.</c> keeps its separator role.</summary>
    private static bool IsLegalInTail(char c) => c is >= ' ' and <= '~' && !ReservedInTail.Contains(c);

    private static bool TryMatchNamespace(ReadOnlySpan<char> segment, out SignalNamespace signalNamespace)
    {
        if (segment.Equals("avatar", StringComparison.OrdinalIgnoreCase))
        {
            signalNamespace = SignalNamespace.Avatar;
            return true;
        }

        if (segment.Equals("input", StringComparison.OrdinalIgnoreCase))
        {
            signalNamespace = SignalNamespace.Input;
            return true;
        }

        if (segment.Equals("system", StringComparison.OrdinalIgnoreCase))
        {
            signalNamespace = SignalNamespace.System;
            return true;
        }

        if (segment.Equals("module", StringComparison.OrdinalIgnoreCase))
        {
            signalNamespace = SignalNamespace.Module;
            return true;
        }

        if (segment.Equals("app", StringComparison.OrdinalIgnoreCase))
        {
            signalNamespace = SignalNamespace.App;
            return true;
        }

        if (segment.Equals("vrc", StringComparison.OrdinalIgnoreCase))
        {
            signalNamespace = SignalNamespace.Vrc;
            return true;
        }

        signalNamespace = default;
        return false;
    }

    private static bool IsLowerableAlphanumeric(char c) =>
        c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';

    private static string ToLowered(ReadOnlySpan<char> s)
    {
        Span<char> buffer = s.Length <= StackLoweringLimit
            ? stackalloc char[StackLoweringLimit]
            : new char[s.Length];

        var written = s.ToLowerInvariant(buffer[..s.Length]);
        return new string(buffer[..written]);
    }
}
