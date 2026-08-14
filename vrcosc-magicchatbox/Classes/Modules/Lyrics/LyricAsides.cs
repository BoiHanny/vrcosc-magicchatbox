using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using vrcosc_magicchatbox.Classes.Utilities;

namespace vrcosc_magicchatbox.Classes.Modules.Lyrics;

/// <summary>
/// Raises the bracketed asides in a lyric - backing vocals, ad-libs - and drops the brackets.
/// Raised letters are single BMP characters, so an aside costs no more than it did.
/// </summary>
public static class LyricAsides
{
    private static readonly Regex Aside = new(
        @"\(([^()]+)\)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // How much of a group must be raisable before raising it. High enough that another script keeps
    // its brackets, low enough that one stubborn letter does not cost the whole aside.
    private const double MinimumRaisedShare = 0.7;

    // Inverted marks have no raised form and no partner to pair with once the sentence is raised.
    private static readonly HashSet<char> Dropped = ['¿', '¡'];

    public static string Apply(string? text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('(') < 0)
            return text ?? string.Empty;

        string source = text;

        return Aside.Replace(source, match =>
        {
            string inner = match.Groups[1].Value.Trim();
            if (!TryRaise(inner, out string raised))
                return match.Value;

            // The brackets used to hold the aside off its neighbours. With them gone, a full-size
            // word touching raised letters reads as one broken word, so keep them apart. Trailing
            // punctuation still hugs, the way it would after any word.
            int after = match.Index + match.Length;
            bool padStart = match.Index > 0 && char.IsLetterOrDigit(source[match.Index - 1]);
            bool padEnd = after < source.Length && char.IsLetterOrDigit(source[after]);

            return (padStart ? " " : string.Empty) + raised + (padEnd ? " " : string.Empty);
        });
    }

    /// <summary>True when enough of the group can be raised to be worth doing.</summary>
    public static bool TryRaise(string value, out string raised)
    {
        raised = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var builder = new StringBuilder(value.Length);
        int words = 0;
        int lifted = 0;

        foreach (char c in value)
        {
            if (Dropped.Contains(c))
                continue;

            bool isWord = char.IsLetterOrDigit(c);

            if (SuperscriptText.TryMap(char.ToLowerInvariant(c), out char mapped))
            {
                builder.Append(mapped);
                if (isWord)
                {
                    words++;
                    lifted++;
                }

                continue;
            }

            // Punctuation stays as it was. A raised comma is an apostrophe, which changes the words.
            builder.Append(c);
            if (isWord)
                words++;
        }

        if (lifted == 0 || lifted < words * MinimumRaisedShare)
            return false;

        raised = builder.ToString();
        return true;
    }
}
