using System.Collections.Immutable;
using MagicChatbox.Vocabulary;

namespace MagicChatbox.Kernel;

/// <summary>
/// Which keys a holder may read and which it may write.
/// </summary>
/// <remarks>
/// <para>
/// <b>GRANTS PREVENT ACCIDENT AND MAKE VIOLATIONS DETECTABLE. THEY ARE NOT A SANDBOX.</b>
/// </para>
/// <para>
/// Everything here is in-process, full trust, one AppDomain. A determined module can use reflection,
/// resolve the <see cref="SignalStore"/> out of the container, or ask a first-party service to read on
/// its behalf. Nothing in this file stops any of that, and given this project's recorded trust-honesty
/// gap, overstating it would be worse than not having it at all. What grants do achieve, completely, is
/// preventing <i>accidental</i> cross-source access and making deliberate bypass <i>attributable</i>.
/// </para>
/// <para>
/// Filtering happens <b>in the kernel</b>, on reads, snapshots, subscriptions and writes alike. There
/// is deliberately no unfiltered stream a caller can subscribe to and filter itself: client-side
/// filtering of a live feed is exactly the alias-as-grant consent gap this project has already shipped
/// once.
/// </para>
/// <para>
/// Patterns use <see cref="KeyPattern"/> — prefix matching, not regex. See that type for the syntax and
/// for why regex was rejected.
/// </para>
/// </remarks>
public sealed record GrantSet
{
    /// <summary>
    /// The one namespace a rule may write, as a pattern rather than as a
    /// <see cref="SignalNamespace"/> comparison, because a grant is text and
    /// <see cref="KeyPattern.Matches"/> is the only thing that reads one. <c>GrantSetTests</c> pins this
    /// string against all six namespaces so that widening it becomes a failing test rather than a quiet
    /// edit.
    /// </summary>
    private const string RuleWritable = "app.*";

    private volatile bool _revoked;

    /// <summary>Creates a grant set from explicit read and write patterns.</summary>
    public GrantSet(ImmutableArray<string> readPatterns, ImmutableArray<string> writePatterns)
    {
        ReadPatterns = readPatterns.IsDefault ? ImmutableArray<string>.Empty : readPatterns;
        WritePatterns = writePatterns.IsDefault ? ImmutableArray<string>.Empty : writePatterns;
    }

    /// <summary>
    /// Everything, unrevocable. First-party subsystems hold this; sources never do, not even
    /// first-party ones, because "which keys does this source own" has to be true by construction for
    /// eviction and the Sources screen to be honest.
    /// </summary>
    public static GrantSet Unrestricted { get; } = new(
        [KeyPattern.Everything], [KeyPattern.Everything]) { IsUnrestricted = true };

    /// <summary>Nothing. The safe default for a holder whose grant has not been decided yet.</summary>
    public static GrantSet None { get; } = new(ImmutableArray<string>.Empty, ImmutableArray<string>.Empty);

    /// <summary>Patterns whose keys may be read, snapshotted and subscribed to.</summary>
    public ImmutableArray<string> ReadPatterns { get; }

    /// <summary>Patterns whose keys may be written.</summary>
    public ImmutableArray<string> WritePatterns { get; }

    /// <summary>True for <see cref="Unrestricted"/> only, which short-circuits every check.</summary>
    public bool IsUnrestricted { get; private init; }

    /// <summary>
    /// True once <see cref="Revoke"/> has run. Checked on every operation, so a scope a module
    /// captured before it was unloaded cannot zombie-write afterwards.
    /// </summary>
    public bool IsRevoked => _revoked;

    /// <summary>A source's own prefix: read and write under <c>module.&lt;id&gt;.*</c> and nothing else.</summary>
    public static GrantSet ForModule(string sourceId)
    {
        var pattern = KeyPattern.ForModule(sourceId);
        return new GrantSet([pattern], [pattern]);
    }

    /// <summary>
    /// What one automation rule reaches: read anything, write under <c>app.</c> and nowhere else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A rule is more trusted than a module and less trusted than a person, and this is where that
    /// sits.</b> <see cref="ForModule"/> confines a module to the one prefix it owns, because a module is
    /// third-party code nobody read. A rule is a sentence the user wrote, so it may write the whole
    /// <c>app.</c> namespace — "remember this for later" is what the <c>signal.set</c> action is for, and
    /// the user picks the key from the same catalog the editor's signal picker shows them. It is still
    /// not a person: the other five namespaces are either observed external truth (<c>avatar.</c>,
    /// <c>input.</c>, <c>system.</c>, <c>vrc.</c>) or another owner's facts (<c>module.</c>), and a rule
    /// asserting one of those would make the store claim something nobody confirmed. Reaching VRChat is
    /// not this path at all — that goes through <c>IVrcEgress</c>, which has no address parameter.
    /// </para>
    /// <para>
    /// <b>This is half the answer.</b> <see cref="WritePolicy"/> holds the other half: the
    /// <see cref="WriteSafety.Privileged"/> keys that live inside <c>app.</c> — the session clock's own
    /// arithmetic, the app's vitals — stay closed to a rule no matter what this grant says, because an
    /// unattended automation has nobody to ask at the moment it fires. Widening this pattern does not
    /// reopen that, and narrowing that check does not close this.
    /// </para>
    /// <para>
    /// Reads are unrestricted because a rule's conditions may name any key the picker offers, which is
    /// the entire descriptor catalog. A condition that reads is not a claim about anything.
    /// </para>
    /// <para>
    /// A fresh instance per call, deliberately, and the same reason <see cref="ForModule"/> is: the
    /// rulebook revokes a rule's grant when that rule is deleted, and a shared instance would take every
    /// other rule down with it.
    /// </para>
    /// </remarks>
    public static GrantSet ForRule() => new([KeyPattern.Everything], [RuleWritable]);

    /// <summary>A grant over one prefix or key, readable and writable.</summary>
    public static GrantSet For(params string[] patterns) =>
        new([.. patterns], [.. patterns]);

    /// <summary>Read-only over the given patterns.</summary>
    public static GrantSet ReadOnly(params string[] patterns) =>
        new([.. patterns], ImmutableArray<string>.Empty);

    /// <summary>
    /// Revokes this grant at module unload. Idempotent.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// <see cref="Unrestricted"/> is a process-wide singleton; revoking it would disable the host.
    /// </exception>
    public void Revoke()
    {
        if (IsUnrestricted)
        {
            throw new InvalidOperationException("GrantSet.Unrestricted is shared process-wide and cannot be revoked.");
        }

        _revoked = true;
    }

    /// <summary>True when this grant covers reading <paramref name="key"/>.</summary>
    public bool CanRead(SignalKey key) => Covers(ReadPatterns, key);

    /// <summary>True when this grant covers writing <paramref name="key"/>.</summary>
    public bool CanWrite(SignalKey key) => Covers(WritePatterns, key);

    private bool Covers(ImmutableArray<string> patterns, SignalKey key)
    {
        if (_revoked)
        {
            return false;
        }

        if (IsUnrestricted)
        {
            return true;
        }

        foreach (var pattern in patterns)
        {
            if (KeyPattern.Matches(pattern, key))
            {
                return true;
            }
        }

        return false;
    }
}
