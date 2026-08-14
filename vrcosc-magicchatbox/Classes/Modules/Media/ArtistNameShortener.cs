using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace vrcosc_magicchatbox.Classes.Modules.Media;

/// <summary>
/// Turns a credit line into progressively shorter renderings, longest first, so a crowded line can
/// give up spare artist names instead of being dropped whole. Browsers report every contributor in
/// one string joined by ", " with the main artist first, so cutting on the commas sheds the least
/// important credits.
/// </summary>
/// <remarks>
/// A band with a comma in its own name is read as several credits, which is wrong. It only happens
/// on a line that was about to disappear anyway, and the leading name still survives.
/// </remarks>
public static class ArtistNameShortener
{
    private static readonly Regex TopicSuffix = new(
        @"\s*-\s*Topic\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // Featured guests are the first thing a human would drop, so they go before any credit is cut.
    // The \b matters more than it looks: without it "ft" matches inside "Daft Punk" and the band
    // gets shortened to "Da".
    private static readonly Regex FeaturedTail = new(
        @"\s*[\(\[]?\s*\b(?:feat\.?|ft\.?|featuring)\s+.*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AmpersandJoin = new(
        @"\s+&\s+",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly char[] CreditSeparators = [',', ';'];

    /// <summary>Strips the decoration YouTube adds to a channel name.</summary>
    public static string Clean(string? artist)
    {
        if (string.IsNullOrWhiteSpace(artist))
            return string.Empty;

        return TopicSuffix.Replace(artist.Trim(), string.Empty).Trim();
    }

    /// <summary>
    /// Splits a credit line into individual artists, main artist first.
    /// </summary>
    public static IReadOnlyList<string> SplitCredits(string? artist)
    {
        string cleaned = Clean(artist);
        if (cleaned.Length == 0)
            return [];

        var parts = cleaned
            .Split(CreditSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        // "A, B & C" is one list with an "and" before the last name. A bare "A & B" is left whole,
        // since duos that own their ampersand are commoner than two-artist collaborations.
        if (parts.Count > 1)
        {
            string[] tail = AmpersandJoin.Split(parts[^1]);
            if (tail.Length > 1)
            {
                parts.RemoveAt(parts.Count - 1);
                parts.AddRange(tail.Select(t => t.Trim()).Where(t => t.Length > 0));
            }
        }

        return parts;
    }

    /// <summary>
    /// Every rendering worth trying, longest first; the caller walks it until one fits. Dropped
    /// credits are counted rather than lost, so "A, B, C, D" becomes "A, B +2".
    /// </summary>
    public static IReadOnlyList<string> Ladder(string? artist)
    {
        string cleaned = Clean(artist);
        if (cleaned.Length == 0)
            return [];

        var rungs = new List<string>();

        void Add(string value)
        {
            if (value.Length > 0 && !rungs.Contains(value))
                rungs.Add(value);
        }

        Add(cleaned);

        string withoutFeat = FeaturedTail.Replace(cleaned, string.Empty)
            .Trim()
            .TrimEnd(',', ';', '(', '[')
            .Trim();
        Add(withoutFeat);

        var credits = SplitCredits(withoutFeat.Length > 0 ? withoutFeat : cleaned);
        for (int keep = credits.Count - 1; keep >= 1; keep--)
            Add($"{string.Join(", ", credits.Take(keep))} +{credits.Count - keep}");

        // The bare headline name, once the "+N" marker itself is too expensive to carry.
        if (credits.Count > 1)
            Add(credits[0]);

        return rungs;
    }
}
