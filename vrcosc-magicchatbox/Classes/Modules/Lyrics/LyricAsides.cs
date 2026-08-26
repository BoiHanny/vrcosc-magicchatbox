using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using vrcosc_magicchatbox.Classes.Utilities;

namespace vrcosc_magicchatbox.Classes.Modules.Lyrics;

public static partial class LyricAsides
{
    [GeneratedRegex(@"\(([^()]+)\)", RegexOptions.CultureInvariant)]
    private static partial Regex Aside();

    private const double MinimumRaisedShare = 0.7;

    private static readonly HashSet<char> Dropped = ['¿', '¡'];

    public static string Apply(string? text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('(') < 0)
            return text ?? string.Empty;

        string source = text;

        return Aside().Replace(source, match =>
        {
            string inner = match.Groups[1].Value.Trim();
            if (!TryRaise(inner, out string raised))
                return match.Value;

            int after = match.Index + match.Length;
            bool padStart = match.Index > 0 && char.IsLetterOrDigit(source[match.Index - 1]);
            bool padEnd = after < source.Length && char.IsLetterOrDigit(source[after]);

            return (padStart ? " " : string.Empty) + raised + (padEnd ? " " : string.Empty);
        });
    }

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
