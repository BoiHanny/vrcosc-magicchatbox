namespace MagicChatbox.Vocabulary;

/// <summary>
/// The one pattern syntax over keys: prefix matching, deliberately not regex.
/// </summary>
/// <remarks>
/// <b>The syntax, pinned so nobody has to guess it.</b> A pattern is one of exactly three shapes:
/// <list type="bullet">
/// <item><description><c>*</c> — every key.</description></item>
/// <item><description>
/// <c>module.music.*</c> — every key strictly beneath <c>module.music</c>. The dot is part of the
/// prefix, so this matches <c>module.music.title</c> and <c>module.music.album.art</c>, and does
/// <b>not</b> match <c>module.musicplayer.title</c>. It does not match the bare stem
/// <c>module.music</c> either, which cannot exist anyway: the grammar requires two segments.
/// </description></item>
/// <item><description><c>module.music.title</c> — that key and nothing else, compared ordinally.</description></item>
/// </list>
/// <para>
/// Regex was rejected. A grant is a security-adjacent decision evaluated on every write, and regex
/// brings catastrophic backtracking, a syntax users get subtly wrong, and — the one that decides it —
/// no way to answer "which keys does this source own" without enumerating the key space. Eviction on
/// <c>Unregister</c> and the Sources screen's key count both need that answer.
/// </para>
/// <para>
/// A <c>*</c> anywhere other than as the whole pattern or as the final segment is not a wildcard; it
/// is a character that cannot appear in a key, so such a pattern matches nothing. That is the safe
/// direction to fail.
/// </para>
/// <para>
/// <b>It sits in the leaf rather than beside <c>GrantSet</c>, which is where it was written.</b>
/// <c>ModuleManifest.ValidateReads</c> compares against <see cref="Everything"/>, and the manifest now
/// lives in <c>MagicChatbox.Modules.Abstractions</c>, whose one permitted reference is this assembly —
/// so a module author would otherwise have to name the kernel to say what a read pattern is. It is
/// stateless string matching over <see cref="SignalKey"/> and referenced nothing outside this assembly
/// even when it lived in the kernel, which is the same test <c>SOLUTION-STRUCTURE.md</c> decision 6
/// applied the first time a helper moved down. <c>GrantSet</c> keeps using it from here.
/// </para>
/// </remarks>
public static class KeyPattern
{
    /// <summary>The pattern that matches every key.</summary>
    public const string Everything = "*";

    /// <summary>True when <paramref name="pattern"/> covers <paramref name="key"/>.</summary>
    public static bool Matches(string pattern, SignalKey key) => Matches(pattern, key.Value);

    /// <summary>
    /// True when <paramref name="pattern"/> covers the already-normalized <paramref name="keyValue"/>.
    /// </summary>
    public static bool Matches(string pattern, string keyValue)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        if (pattern.Length == 1 && pattern[0] == '*')
        {
            return true;
        }

        if (pattern.EndsWith(".*", StringComparison.Ordinal))
        {
            // The trailing '*' goes; the '.' stays, which is what stops module.music.* from reaching
            // module.musicplayer.title.
            var prefix = pattern.AsSpan(0, pattern.Length - 1);
            return keyValue.AsSpan().StartsWith(prefix, StringComparison.Ordinal);
        }

        return string.Equals(pattern, keyValue, StringComparison.Ordinal);
    }

    /// <summary>
    /// The pattern granting a source everything under its own id: <c>module.&lt;id&gt;.*</c>.
    /// </summary>
    /// <exception cref="ArgumentException">The id is not a legal source id.</exception>
    public static string ForModule(string sourceId)
    {
        if (!SignalKey.TryNormalizeSourceId(sourceId, out var normalized))
        {
            throw new ArgumentException($"'{sourceId}' is not a valid source id.", nameof(sourceId));
        }

        return $"module.{normalized}.*";
    }
}
