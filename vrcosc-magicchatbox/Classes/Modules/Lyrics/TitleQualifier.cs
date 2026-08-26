using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace vrcosc_magicchatbox.Classes.Modules.Lyrics;

public static partial class TitleQualifier
{
    [GeneratedRegex(@"\s*[\(\[]([^\(\)\[\]]*)[\)\]]\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex BracketTail();

    [GeneratedRegex(@"\s+[-–—]\s+(.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex DashTail();

    public static (string Base, string Qualifier) Split(string? title)
    {
        string working = (title ?? string.Empty).Trim();
        if (working.Length == 0)
            return (string.Empty, string.Empty);

        var qualifiers = new List<string>();

        while (true)
        {
            Match bracket = BracketTail().Match(working);
            if (!bracket.Success)
                break;

            string remainder = working[..bracket.Index].Trim();

            if (remainder.Length == 0)
                break;

            qualifiers.Insert(0, bracket.Groups[1].Value.Trim());
            working = remainder;
        }

        Match dash = DashTail().Match(working);
        if (dash.Success)
        {
            string remainder = working[..dash.Index].Trim();
            if (remainder.Length > 0)
            {
                qualifiers.Insert(0, dash.Groups[1].Value.Trim());
                working = remainder;
            }
        }

        return (working, string.Join(" ", qualifiers).Trim());
    }

    public static string BaseTitle(string? title) => Split(title).Base;

    public static string PrimaryArtist(string? artist)
    {
        string working = (artist ?? string.Empty).Trim();
        if (working.Length == 0)
            return string.Empty;

        int cut = working.IndexOfAny([',', ';']);
        if (cut > 0)
            working = working[..cut];

        int amp = working.IndexOf(" & ", StringComparison.Ordinal);
        if (amp > 0)
            working = working[..amp];

        return working.Trim();
    }
}
