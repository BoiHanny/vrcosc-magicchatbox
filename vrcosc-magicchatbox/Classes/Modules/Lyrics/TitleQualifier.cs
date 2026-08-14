using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace vrcosc_magicchatbox.Classes.Modules.Lyrics;

/// <summary>
/// Splits a track title into the song and whatever trails it naming a version. Anything in brackets
/// or after a dash counts, so remixes, edits, live takes and guest credits are all covered without a
/// word list to maintain.
/// </summary>
public static class TitleQualifier
{
    private static readonly Regex BracketTail = new(
        @"\s*[\(\[]([^\(\)\[\]]*)[\)\]]\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DashTail = new(
        @"\s+[-–—]\s+(.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>The song, and everything trailing it that names a particular version.</summary>
    public static (string Base, string Qualifier) Split(string? title)
    {
        string working = (title ?? string.Empty).Trim();
        if (working.Length == 0)
            return (string.Empty, string.Empty);

        var qualifiers = new List<string>();

        // One at a time, so "Song (Live) (Remastered)" gives up both.
        while (true)
        {
            Match bracket = BracketTail.Match(working);
            if (!bracket.Success)
                break;

            string remainder = working[..bracket.Index].Trim();

            // A title that is only a bracket keeps it, or nothing is left to search for.
            if (remainder.Length == 0)
                break;

            qualifiers.Insert(0, bracket.Groups[1].Value.Trim());
            working = remainder;
        }

        Match dash = DashTail.Match(working);
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

    /// <summary>The song without the version, for a search that already failed with it.</summary>
    public static string BaseTitle(string? title) => Split(title).Base;

    /// <summary>
    /// The first name in a credit list. Collaborations are filed under several spellings of their
    /// credits; the lead name is the one they all share.
    /// </summary>
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
